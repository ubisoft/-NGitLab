using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NuGet.Versioning;
using NUnit.Framework;
using Polly;

namespace NGitLab.Tests;

[Timeout(240_000)]
public class PipelineTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_can_list_the_pipelines()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);

        var pipelines = await GitLabTestContext.RetryUntilAsync(() => pipelineClient.All, p => p.Any(), TimeSpan.FromSeconds(120));
        Assert.That(pipelines, Is.Not.Empty);
        foreach (var pipeline in pipelines)
        {
            Assert.That(pipeline.ProjectId, Is.EqualTo(project.Id));
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_get_coverage()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);
        PipelineBasic pipeline;
        using (await context.StartRunnerForOneJobAsync(project.Id))
        {
            pipeline = await GitLabTestContext.RetryUntilAsync(() => pipelineClient.All.FirstOrDefault(), p => p != null, TimeSpan.FromSeconds(120));
        }

        context.Client.GetCommitStatus(project.Id).AddOrUpdate(new CommitStatusCreate
        {
            CommitSha = pipeline.Sha.ToString(),
            Ref = pipeline.Ref,
            PipelineId = pipeline.Id,
            Name = "test",
            Coverage = 50,
            Description = "descr",
            State = "success",
            TargetUrl = "https://example.com",
        });

        var pipelineFull = pipelineClient[pipeline.Id];
        Assert.That(pipelineFull.Coverage, Is.EqualTo(50));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_list_all_jobs_from_project()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);

        var allJobs = await GitLabTestContext.RetryUntilAsync(() => pipelineClient.AllJobs.ToList(), p => p.Count != 0, TimeSpan.FromSeconds(120));
        Assert.That(allJobs.Count != 0);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_list_jobs_from_pipeline()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);

        var pipelines = await GitLabTestContext.RetryUntilAsync(() => pipelineClient.All, p => p.Any(), TimeSpan.FromSeconds(120));
        var pipeline = pipelines.First();

        var pipelineJobQuery = new PipelineJobQuery
        {
            PipelineId = pipeline.Id,
            Scope = new[] { "success", "pending" },
        };
        var allJobs = await GitLabTestContext.RetryUntilAsync(() => pipelineClient.GetJobsAsync(pipelineJobQuery).ToList(), p => p.Count != 0, TimeSpan.FromSeconds(120));
        Assert.That(allJobs.Count != 0);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_search_for_pipeline()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);

        var query = new PipelineQuery
        {
            Ref = project.DefaultBranch,
        };
        var pipelinesFromQuery = await GitLabTestContext.RetryUntilAsync(() => pipelineClient.Search(query).ToList(), p => p.Count != 0, TimeSpan.FromSeconds(120));

        Assert.That(pipelinesFromQuery.Count != 0, Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_delete_pipeline()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);
        var pipeline = await GitLabTestContext.RetryUntilAsync(() => pipelineClient.All.FirstOrDefault(), p => p != null, TimeSpan.FromSeconds(120));

        Policy
            .Handle<GitLabException>(ex => ex.StatusCode == HttpStatusCode.Conflict)
            .Retry(10)
            .Execute(() => pipelineClient.Delete(pipeline.Id));

        await GitLabTestContext.RetryUntilAsync(() => pipelineClient.All.FirstOrDefault(), p => p == null, TimeSpan.FromSeconds(120));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_create_pipeline_with_variables()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);

        // Arrange/Act
        var pipeline = pipelineClient.Create(new PipelineCreate
        {
            Ref = project.DefaultBranch,
            Variables =
            {
                { "Var1", "Value1" },
                { "Var2", "+4+" },
            },
        });

        // Assert
        var variables = pipelineClient.GetVariables(pipeline.Id);

        var var1 = variables.SingleOrDefault(v => v.Key.Equals("Var1", StringComparison.Ordinal));
        Assert.That(var1, Is.Not.Null);

        var var2 = variables.SingleOrDefault(v => v.Key.Equals("Var2", StringComparison.Ordinal));
        Assert.That(var2, Is.Not.Null);

        Assert.That(var1.Value, Is.EqualTo("Value1"));
        Assert.That(var2.Value, Is.EqualTo("+4+"));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_create_pipeline_with_testreports()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);

        // Arrange/Act
        var pipeline = pipelineClient.Create(new PipelineCreate
        {
            Ref = project.DefaultBranch,
        });

        // Assert
        var testReports = pipelineClient.GetTestReports(pipeline.Id);
        var summary = pipelineClient.GetTestReportsSummary(pipeline.Id);

        Assert.That(testReports, Is.Not.Null);
        Assert.That(summary, Is.Not.Null);

        Assert.That(testReports.TotalTime, Is.EqualTo(0));
        Assert.That(summary.Total.Time, Is.EqualTo(0));

        Assert.That(testReports.TotalCount, Is.EqualTo(0));
        Assert.That(summary.Total.Count, Is.EqualTo(0));

        Assert.That(testReports.SuccessCount, Is.EqualTo(0));
        Assert.That(summary.Total.Success, Is.EqualTo(0));

        Assert.That(testReports.FailedCount, Is.EqualTo(0));
        Assert.That(summary.Total.Failed, Is.EqualTo(0));

        Assert.That(testReports.SkippedCount, Is.EqualTo(0));
        Assert.That(summary.Total.Skipped, Is.EqualTo(0));

        Assert.That(testReports.ErrorCount, Is.EqualTo(0));
        Assert.That(summary.Total.Error, Is.EqualTo(0));

        Assert.That(testReports.TestSuites, Is.Empty);
        Assert.That(summary.TestSuites, Is.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_triggered_pipeline_variables()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);

        var triggers = context.Client.GetTriggers(project.Id);
        var trigger = triggers.Create("Test Trigger");
        var ciJobToken = trigger.Token;

        var pipeline = pipelineClient.CreatePipelineWithTrigger(ciJobToken, project.DefaultBranch, new Dictionary<string, string>(StringComparer.Ordinal) { { "Test", "HelloWorld" } });

        var variables = pipelineClient.GetVariables(pipeline.Id);

        Assert.That(variables.Any(v =>
            v.Key.Equals("Test", StringComparison.Ordinal) &&
            v.Value.Equals("HelloWorld", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_retry()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);

        using (await context.StartRunnerForOneJobAsync(project.Id))
        {
            JobTests.AddGitLabCiFile(context.Client, project, pipelineSucceeds: false);
            var pipeline = await GitLabTestContext.RetryUntilAsync(() => pipelineClient.All.FirstOrDefault(), pipeline =>
            {
                if (pipeline != null)
                {
                    TestContext.Out.WriteLine("Pipeline status: " + pipeline.Status);
                    return pipeline.Status is JobStatus.Failed;
                }

                return false;
            }, TimeSpan.FromMinutes(2));

            var retriedPipeline = await pipelineClient.RetryAsync(pipeline.Id);
            Assert.That(retriedPipeline.Status, Is.Not.EqualTo(JobStatus.Failed)); // Should be created or running
        }
    }


    [Test]
    [NGitLabRetry]
    public async Task Test_update_pipeline_metadata()
    {
        using var context = await GitLabTestContext.CreateAsync();

        // The "Update pipeline metadata" was added in GitLab 16, the earliest available docs that include the API is for version 16.11
        context.ReportTestAsInconclusiveIfGitLabVersionOutOfRange(VersionRange.Parse("[16.11,)"));

        var project = context.CreateProject();
        var pipelineClient = context.Client.GetPipelines(project.Id);
        JobTests.AddGitLabCiFile(context.Client, project);


        var pipeline = await pipelineClient.CreateAsync(new PipelineCreate
        {
            Ref = project.DefaultBranch,
        });


        var updatedPipeline = await pipelineClient.UpdateMetadataAsync(pipeline.Id, new PipelineMetadataUpdate() { Name = "Updated Name" });

        Assert.That(updatedPipeline.Name, Is.EqualTo("Updated Name"));
    }
}
