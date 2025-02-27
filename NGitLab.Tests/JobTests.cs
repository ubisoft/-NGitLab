using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests;

public class JobTests
{
    internal static void AddGitLabCiFile(IGitLabClient client, Project project, int jobCount = 1, bool manualAction = false, string branch = null, bool pipelineSucceeds = true)
    {
        var content = """
            variables:
              CI_DEBUG_TRACE: "true"
            
            """;

        for (var i = 0; i < jobCount; i++)
        {
            content += $"""
                build{i.ToString(CultureInfo.InvariantCulture)}:
                  script:
                    - echo test
                    - echo test > file{i.ToString(CultureInfo.InvariantCulture)}.txt
                    - exit {(pipelineSucceeds ? "0" : "1")}
                  artifacts:
                    paths:
                      - '*.txt'
                
                """;

            if (manualAction)
            {
                content += """
                      when: manual
                    
                    """;
            }
        }

        client.GetRepository(project.Id).Files.Create(new FileUpsert
        {
            Branch = branch ?? project.DefaultBranch,
            CommitMessage = "test",
            Path = ".gitlab-ci.yml",
            Content = content,
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_getjobs_all()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        AddGitLabCiFile(context.Client, project);
        var jobs = await GitLabTestContext.RetryUntilAsync(() => context.Client.GetJobs(project.Id).GetJobs(JobScopeMask.Pending), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
        Assert.That(jobs.First().Status, Is.EqualTo(JobStatus.Pending));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_getjobs_scope()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        AddGitLabCiFile(context.Client, project, manualAction: true);
        var jobs = await GitLabTestContext.RetryUntilAsync(() => context.Client.GetJobs(project.Id).GetJobs(JobScopeMask.Manual), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
        Assert.That(jobs.First().Status, Is.EqualTo(JobStatus.Manual));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_getjobs_multiple_scopes()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var jobsClient = context.Client.GetJobs(project.Id);

        AddGitLabCiFile(context.Client, project, 2);
        var jobs = await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.Pending), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
        var job = jobs.First();

        jobsClient.RunAction(job.Id, JobAction.Cancel);
        jobs = await GitLabTestContext.RetryUntilAsync(() => context.Client.GetJobs(project.Id).GetJobs(
            new JobQuery
            {
                Scope = JobScopeMask.Pending |
                        JobScopeMask.Canceled,
            }),
            jobs => jobs.Any(), TimeSpan.FromMinutes(2));

        Assert.That(jobs.First().Status, Is.EqualTo(JobStatus.Canceled));
        Assert.That(jobs.Last().Status, Is.EqualTo(JobStatus.Pending));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_run_action_play()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var jobsClient = context.Client.GetJobs(project.Id);
        AddGitLabCiFile(context.Client, project, manualAction: true);
        var jobs = await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.Manual), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
        var job = jobs.Single();
        Assert.That(job.Status, Is.EqualTo(JobStatus.Manual));

        var playedJob = jobsClient.RunAction(job.Id, JobAction.Play);

        Assert.That(playedJob.Id, Is.EqualTo(job.Id));
        Assert.That(playedJob.Pipeline.Id, Is.EqualTo(job.Pipeline.Id));
        Assert.That(playedJob.Commit.Id, Is.EqualTo(job.Commit.Id));
        Assert.That(playedJob.Status, Is.EqualTo(JobStatus.Pending));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_run_action_retry()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var jobsClient = context.Client.GetJobs(project.Id);

        AddGitLabCiFile(context.Client, project);
        var jobs = await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.Pending), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
        var job = jobs.Single();

        jobsClient.RunAction(job.Id, JobAction.Cancel);
        await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.Canceled), jobs => jobs.Any(), TimeSpan.FromMinutes(2));

        var retriedJob = jobsClient.RunAction(job.Id, JobAction.Retry);

        Assert.That(retriedJob.Id, Is.Not.EqualTo(job.Id));
        Assert.That(retriedJob.Pipeline.Id, Is.EqualTo(job.Pipeline.Id));
        Assert.That(retriedJob.Commit.Id, Is.EqualTo(job.Commit.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_job_from_id()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var jobsClient = context.Client.GetJobs(project.Id);
        AddGitLabCiFile(context.Client, project);
        var jobs = await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.All), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
        var job = jobs.Single();

        var job2 = jobsClient.Get(job.Id);

        Assert.That(job2.Id, Is.EqualTo(job.Id)); // Same Job
        Assert.That(job2.Pipeline.Id, Is.EqualTo(job.Pipeline.Id)); // Same Pipeline
        Assert.That(job2.Commit.Id, Is.EqualTo(job.Commit.Id)); // Same Commit
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_job_trace()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var jobsClient = context.Client.GetJobs(project.Id);
        using (await context.StartRunnerForOneJobAsync(project.Id))
        {
            AddGitLabCiFile(context.Client, project);
            var jobs = await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.All), jobs =>
            {
                var job = jobs.FirstOrDefault();
                if (jobs.Any())
                {
                    TestContext.Out.WriteLine("Job status: " + job.Status);
                    return job.Status == JobStatus.Success || job.Status == JobStatus.Failed;
                }

                return false;
            }, TimeSpan.FromMinutes(2));

            var job = jobs.Single();
            var trace = jobsClient.GetTrace(job.Id);

            Assert.That(trace, Does.Contain("Running with gitlab-runner"));
            Assert.That(trace, Does.Contain("Job succeeded"));
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_job_artifacts()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var jobsClient = context.Client.GetJobs(project.Id);
        using (await context.StartRunnerForOneJobAsync(project.Id))
        {
            AddGitLabCiFile(context.Client, project);
            var jobs = await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.Success), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
            var job = jobs.Single();
            Assert.That(job.Status, Is.EqualTo(JobStatus.Success));

            var artifacts = jobsClient.GetJobArtifacts(job.Id);

            Assert.That(artifacts, Is.Not.Empty);
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_job_artifact()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var jobsClient = context.Client.GetJobs(project.Id);
        using (await context.StartRunnerForOneJobAsync(project.Id))
        {
            AddGitLabCiFile(context.Client, project);
            var jobs = await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.Success), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
            var job = jobs.Single();
            Assert.That(job.Status, Is.EqualTo(JobStatus.Success));

            var artifact = jobsClient.GetJobArtifact(job.Id, "file0.txt");
            Assert.That(artifact, Is.Not.Empty);

            var content = Encoding.ASCII.GetString(artifact).Trim();
            Assert.That(content, Is.EqualTo("test"));
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_job_artifact_query()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var jobsClient = context.Client.GetJobs(project.Id);
        using (await context.StartRunnerForOneJobAsync(project.Id))
        {
            AddGitLabCiFile(context.Client, project);
            var jobs = await GitLabTestContext.RetryUntilAsync(() => jobsClient.GetJobs(JobScopeMask.Success), jobs => jobs.Any(), TimeSpan.FromMinutes(2));
            var job = jobs.Single();
            Assert.That(job.Status, Is.EqualTo(JobStatus.Success));

            var query = new JobArtifactQuery
            {
                RefName = project.DefaultBranch,
                JobName = job.Name,
                ArtifactPath = "file0.txt",
            };

            var artifact = jobsClient.GetJobArtifact(query);
            Assert.That(artifact, Is.Not.Empty);

            var content = Encoding.ASCII.GetString(artifact).Trim();
            Assert.That(content, Is.EqualTo("test"));
        }
    }
}
