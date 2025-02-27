using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;
using Polly;
using Polly.Retry;

namespace NGitLab.Tests;

public class RunnerTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_can_enable_and_disable_a_runner_on_a_project()
    {
        // We need 2 projects associated to a runner to disable a runner
        using var context = await GitLabTestContext.CreateAsync();
        var project1 = context.CreateProject(initializeWithCommits: true);
        var project2 = context.CreateProject(initializeWithCommits: true);

        var runnersClient = context.Client.Runners;
        var runner = runnersClient.Register(new RunnerRegister { Token = project1.RunnersToken });
        runnersClient.EnableRunner(project2.Id, new RunnerId(runner.Id));

        runnersClient.DisableRunner(project1.Id, new RunnerId(runner.Id));
        Assert.That(IsEnabled(), Is.False);

        runnersClient.EnableRunner(project1.Id, new RunnerId(runner.Id));
        Assert.That(IsEnabled(), Is.True);

        bool IsEnabled() => runnersClient[runner.Id].Projects.Any(x => x.Id == project1.Id);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_register_and_delete_a_runner_on_a_group()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var group1 = context.CreateGroup();

        // The runners token of a group is not contained in the API response
        var groupClient = context.Client.Groups;
        var createdGroup1 = groupClient.GetGroup(group1.Id);

        var runnersClient = context.Client.Runners;
        var runner = runnersClient.Register(new RunnerRegister { Token = createdGroup1.RunnersToken });
        Assert.That(IsRegistered(), Is.True);

        GetRetryPolicy().Execute(() => { runnersClient.Delete(runner.Id); });
        var ex = Assert.Throws<GitLabException>(() => IsRegistered());
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        bool IsRegistered() => GetRetryPolicy().Execute(() => runnersClient[runner.Id].Groups.Any(x => x.Id == createdGroup1.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_find_a_runner_on_a_project()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);
        var project2 = context.CreateProject(initializeWithCommits: true);
        var runnersClient = context.Client.Runners;
        var runner = runnersClient.Register(new RunnerRegister { Token = project.RunnersToken });
        runnersClient.EnableRunner(project2.Id, new RunnerId(runner.Id));

        var result = runnersClient.OfProject(project.Id).ToList();
        Assert.That(result.Exists(r => r.Id == runner.Id), Is.True);

        runnersClient.DisableRunner(project.Id, new RunnerId(runner.Id));
        result = runnersClient.OfProject(project.Id).ToList();
        Assert.That(result.TrueForAll(r => r.Id != runner.Id), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_find_a_runner_on_a_group()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var group1 = context.CreateGroup();

        // The runners token of a group is not contained in the API response
        var groupClient = context.Client.Groups;
        var createdGroup1 = groupClient.GetGroup(group1.Id);

        var runnersClient = context.Client.Runners;
        var runner = runnersClient.Register(new RunnerRegister { Token = createdGroup1.RunnersToken });

        var result = runnersClient.OfGroup(createdGroup1.Id).ToArray();
        Assert.That(result.Any(r => r.Id == runner.Id), Is.True);

        GetRetryPolicy().Execute(() => { runnersClient.Delete(runner.Id); });
        result = runnersClient.OfGroup(createdGroup1.Id).ToArray();
        Assert.That(result.All(r => r.Id != runner.Id), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_Runner_Can_Be_Locked_And_Unlocked()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);
        var runnersClient = context.Client.Runners;
        var runner = runnersClient.Register(new RunnerRegister { Token = project.RunnersToken, Locked = false });
        Assert.That(runner.Locked, Is.False, "Runner should not be locked.");

        runner = runnersClient.Update(runner.Id, new RunnerUpdate { Locked = true });
        Assert.That(runner.Locked, Is.True, "Runner should be locked.");

        runner = runnersClient.Update(runner.Id, new RunnerUpdate { Locked = false });
        Assert.That(runner.Locked, Is.False, "Runner should not be locked.");
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_Runner_Can_Update_RunUntagged_Flag()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);
        var runnersClient = context.Client.Runners;
        var runner = runnersClient.Register(new RunnerRegister { Token = project.RunnersToken, RunUntagged = false, TagList = new[] { "tag" } });
        Assert.That(runner.RunUntagged, Is.False);

        runner = runnersClient.Update(runner.Id, new RunnerUpdate { RunUntagged = true });
        Assert.That(runner.RunUntagged, Is.True);
    }

    private static RetryPolicy GetRetryPolicy()
    {
        return Policy
            .Handle<GitLabException>(ex => ex.StatusCode is HttpStatusCode.Forbidden)
            .WaitAndRetry(3, sleepDurationProvider: attempt => TimeSpan.FromSeconds(2 * attempt));
    }
}
