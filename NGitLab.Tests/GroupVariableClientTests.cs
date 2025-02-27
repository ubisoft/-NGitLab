using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NuGet.Versioning;
using NUnit.Framework;

namespace NGitLab.Tests;

public class GroupVariableClientTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_group_variables()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var group = context.CreateGroup();
        var groupVariableClient = context.Client.GetGroupVariableClient(group.Id);

        // Create
        var variable = groupVariableClient.Create(new VariableCreate
        {
            Key = "My_Key",
            Value = "My value",
            Protected = true,
        });

        Assert.That(variable.Key, Is.EqualTo("My_Key"));
        Assert.That(variable.Value, Is.EqualTo("My value"));
        Assert.That(variable.Protected, Is.EqualTo(true));

        // Update
        variable = groupVariableClient.Update(variable.Key, new VariableUpdate
        {
            Value = "My value edited",
            Protected = false,
        });

        Assert.That(variable.Key, Is.EqualTo("My_Key"));
        Assert.That(variable.Value, Is.EqualTo("My value edited"));
        Assert.That(variable.Protected, Is.EqualTo(false));

        // Delete
        groupVariableClient.Delete(variable.Key);

        var variables = groupVariableClient.All.ToList();
        Assert.That(variables, Is.Empty);

        // All
        groupVariableClient.Create(new VariableCreate { Key = "Variable1", Value = "test" });
        groupVariableClient.Create(new VariableCreate { Key = "Variable2", Value = "test" });
        groupVariableClient.Create(new VariableCreate { Key = "Variable3", Value = "test" });
        variables = groupVariableClient.All.ToList();
        Assert.That(variables, Has.Count.EqualTo(3));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_group_variables_with_complete_members()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var group = context.CreateGroup();
        var groupVariableClient = context.Client.GetGroupVariableClient(group.Id);

        // Create
        var variable = groupVariableClient.Create(new VariableCreate
        {
            Key = "My_Key",
            Value = "My value",
            Description = "Some important variable",
            Protected = true,
            Type = VariableType.Variable,
            Masked = false,
            Raw = false,
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

        // Update
        var newScope = "integration/*";
        variable = groupVariableClient.Update(variable.Key, variable.EnvironmentScope, new VariableUpdate
        {
            Value = "My value edited",
            Protected = false,
            EnvironmentScope = newScope,
        });

        Assert.That(variable.Key, Is.EqualTo("My_Key"));
        Assert.That(variable.Value, Is.EqualTo("My value edited"));
        Assert.That(variable.Protected, Is.EqualTo(false));

        // Delete
        var ex = Assert.Throws<GitLabException>(() => groupVariableClient.Delete(variable.Key, "wrongScope"));
        Assert.That(ex!.StatusCode == HttpStatusCode.NotFound);

        groupVariableClient.Delete(variable.Key);

        var variables = groupVariableClient.All.ToList();
        Assert.That(variables, Is.Empty);

        // All
        groupVariableClient.Create(new VariableCreate { Key = "Variable1", Value = "test", EnvironmentScope = "test/*" });
        groupVariableClient.Create(new VariableCreate { Key = "Variable2", Value = "test", EnvironmentScope = "integration" });
        groupVariableClient.Create(new VariableCreate { Key = "Variable3", Value = "test", EnvironmentScope = "*" });
        variables = groupVariableClient.All.ToList();
        Assert.That(variables, Has.Count.EqualTo(3));
    }
}
