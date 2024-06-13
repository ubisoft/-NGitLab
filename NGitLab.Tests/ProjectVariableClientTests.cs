﻿using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Extensions;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NuGet.Versioning;
using NUnit.Framework;

namespace NGitLab.Tests;

public class ProjectVariableClientTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_project_variables()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectVariableClient = context.Client.GetProjectVariableClient(project.Id);

        // Create
        var variable = projectVariableClient.Create(new VariableCreate
        {
            Key = "My_Key",
            Value = "My value",
            Protected = true,
        });

        Assert.That(variable.Key, Is.EqualTo("My_Key"));
        Assert.That(variable.Value, Is.EqualTo("My value"));
        Assert.That(variable.Protected, Is.EqualTo(true));

        // Update
        variable = projectVariableClient.Update(variable.Key, new VariableUpdate
        {
            Value = "My value edited",
            Protected = false,
        });

        Assert.That(variable.Key, Is.EqualTo("My_Key"));
        Assert.That(variable.Value, Is.EqualTo("My value edited"));
        Assert.That(variable.Protected, Is.EqualTo(false));

        // Delete
        projectVariableClient.Delete(variable.Key);

        var variables = projectVariableClient.All.ToList();
        Assert.That(variables, Is.Empty);

        // All
        projectVariableClient.Create(new VariableCreate { Key = "Variable1", Value = "test" });
        projectVariableClient.Create(new VariableCreate { Key = "Variable2", Value = "test" });
        projectVariableClient.Create(new VariableCreate { Key = "Variable3", Value = "test" });
        variables = projectVariableClient.All.ToList();
        Assert.That(variables, Has.Count.EqualTo(3));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_project_variables_with_scope()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectVariableClient = context.Client.GetProjectVariableClient(project.Id);

        // Create
        var variable = projectVariableClient.Create(new Variable
        {
            Key = "My_Key",
            Value = "My value",
            Description = "Some important variable",
            Protected = true,
            Type = VariableType.Variable,
            Masked = false,
            Raw = false,
            EnvironmentScope = "test/*",
        });

        Assert.That(variable.Key, Is.EqualTo("My_Key"));
        Assert.That(variable.Value, Is.EqualTo("My value"));

        if (context.IsGitLabVersionInRange(VersionRange.Parse("[16.2,)"), out _))
        {
            Assert.That(variable.Description, Is.EqualTo("Some important variable"));
        }

        Assert.That(variable.Protected, Is.EqualTo(true));
        Assert.That(variable.Type, Is.EqualTo(VariableType.Variable));
        Assert.That(variable.Masked, Is.EqualTo(false));
        Assert.That(variable.Raw, Is.EqualTo(false));
        Assert.That(variable.EnvironmentScope, Is.EqualTo("test/*"));

        // Update
        var newScope = "integration/*";
        variable = projectVariableClient.Update(variable.Key, new Variable
        {
            Value = "My value edited",
            Protected = false,
            EnvironmentScope = newScope,
        },
        variable.EnvironmentScope);

        Assert.That(variable.Key, Is.EqualTo("My_Key"));
        Assert.That(variable.Value, Is.EqualTo("My value edited"));
        Assert.That(variable.Protected, Is.EqualTo(false));
        Assert.That(variable.EnvironmentScope, Is.EqualTo(newScope));

        // Delete
        var ex = Assert.Throws<GitLabException>(() => projectVariableClient.Delete(variable.Key, "wrongScope"));
        Assert.That(ex!.StatusCode == HttpStatusCode.NotFound);

        projectVariableClient.Delete(variable.Key, newScope);

        var variables = projectVariableClient.All.ToList();
        Assert.That(variables, Is.Empty);

        // All
        projectVariableClient.Create(new Variable { Key = "Variable1", Value = "test", EnvironmentScope = "test/*" });
        projectVariableClient.Create(new Variable { Key = "Variable2", Value = "test", EnvironmentScope = "integration" });
        projectVariableClient.Create(new Variable { Key = "Variable3", Value = "test", EnvironmentScope = "*" });
        variables = projectVariableClient.All.ToList();
        Assert.That(variables, Has.Count.EqualTo(3));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_matching_scoped_variables()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectVariableClient = context.Client.GetProjectVariableClient(project.Id);

        // Create variable for test environment
        projectVariableClient.Create(new Variable
        {
            Key = "My_Key",
            Value = "My value",
            Description = "Variable for test environments",
            Protected = true,
            Type = VariableType.Variable,
            Masked = false,
            Raw = false,
            EnvironmentScope = "test/*",
        });

        // Create variable for test environment
        var variableForIntegration = projectVariableClient.Create(new Variable
        {
            Key = "My_Key",
            Value = "My value",
            Description = "Variable for integration environments",
            Protected = true,
            Type = VariableType.Variable,
            Masked = false,
            Raw = false,
            EnvironmentScope = "integration/*",
        });

        var specificEnvironment = "integration/datacenter-105-left";
        var variablesForSpecificEnvironment = projectVariableClient.All.Where(v => v.IsMatchForEnvironment(specificEnvironment));

        Assert.That(variablesForSpecificEnvironment.Count(), Is.EqualTo(1));
    }

    [Test]
    [NGitLabRetry]
    public void Test_standalone_matching_scoped_variables()
    {
        var variable = new Variable
        {
            Key = "My_Key",
            Value = "My value",
            Description = "Some important variable",
            Protected = true,
            Type = VariableType.Variable,
            Masked = false,
            Raw = false,
            EnvironmentScope = "test/*",
        };

        Assert.That(variable.IsMatchForEnvironment("test/"), Is.True);
        Assert.That(variable.IsMatchForEnvironment("test/123"), Is.True);
        Assert.That(variable.IsMatchForEnvironment("integration"), Is.False);

        // Change to simple scope
        variable.EnvironmentScope = "test";

        Assert.That(variable.IsMatchForEnvironment("test"), Is.True);
        Assert.That(variable.IsMatchForEnvironment("integration"), Is.False);
        Assert.That(variable.IsMatchForEnvironment("integration/abc"), Is.False);
    }
}
