﻿using System.Linq;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests
{
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
        public async Task Test_can_find_a_runner_on_a_project()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var project = context.CreateProject(initializeWithCommits: true);
            var project2 = context.CreateProject(initializeWithCommits: true);
            var runnersClient = context.Client.Runners;
            var runner = runnersClient.Register(new RunnerRegister { Token = project.RunnersToken });
            runnersClient.EnableRunner(project2.Id, new RunnerId(runner.Id));

            var result = runnersClient.OfProject(project.Id).ToList();
            Assert.That(result.Any(r => r.Id == runner.Id), Is.True);

            runnersClient.DisableRunner(project.Id, new RunnerId(runner.Id));
            result = runnersClient.OfProject(project.Id).ToList();
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
    }
}
