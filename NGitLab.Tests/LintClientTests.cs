using System.Threading;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests;

public class LintClientTests
{
    private const string ValidCIYaml = """
        variables:
          CI_DEBUG_TRACE: "true"
        build-job:
          stage: build
          tags:
            - Runner-Build
          before_script:
            - echo before start
            - echo before end
          script:
            - echo test start
            - echo test end
          after_script:
            - echo after start
            - echo after end
          when: always
          allow_failure: true
        """;

    private const string InvalidCIYaml = """
        variables:
          CI_DEBUG_TRACE: "true"
        build-job:
          script:
            - echo test
          this_key_should_not_exist:
            - this should fail the linting
        """;

    [Test]
    [NGitLabRetry]
    public async Task LintValidCIYaml()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var lintClient = context.Client.Lint;

        var result = await context.Client.Lint.ValidateCIYamlContentAsync(project.Id.ToString(), ValidCIYaml, new(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valid, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task LintValidCIYamlWithJobs()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var lintClient = context.Client.Lint;

        var result = await context.Client.Lint.ValidateCIYamlContentAsync(project.Id.ToString(), ValidCIYaml, new() { IncludeJobs = true }, CancellationToken.None);

        Assert.That(result.Jobs, Has.Length.EqualTo(1));
        var job = result.Jobs[0];
        Assert.Multiple(() =>
        {
            Assert.That(job.Name, Is.EqualTo("build-job"));
            Assert.That(job.Stage, Is.EqualTo("build"));
            Assert.That(job.BeforeScript, Is.EqualTo(["echo before start", "echo before end"]));
            Assert.That(job.Script, Is.EqualTo(["echo test start", "echo test end"]));
            Assert.That(job.AfterScript, Is.EqualTo(["echo after start", "echo after end"]));
            Assert.That(job.TagList, Is.EqualTo(["Runner-Build"]));
            Assert.That(job.Environment, Is.Null);
            Assert.That(job.When, Is.EqualTo("always"));
            Assert.That(job.AllowFailure, Is.True);
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task LintInvalidCIYaml()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var lintClient = context.Client.Lint;

        var result = await context.Client.Lint.ValidateCIYamlContentAsync(project.Id.ToString(), InvalidCIYaml, new(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task LintValidCIProjectYaml()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var lintClient = context.Client.Lint;

        context.Client.GetRepository(project.Id).Files.Create(new FileUpsert
        {
            Branch = project.DefaultBranch,
            CommitMessage = "test",
            Path = ".gitlab-ci.yml",
            Content = ValidCIYaml,
        });

        var result = await context.Client.Lint.ValidateProjectCIConfigurationAsync(project.Id.ToString(), new(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valid, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.Warnings, Is.Empty);
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task LintInvalidProjectCIYaml()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var lintClient = context.Client.Lint;

        context.Client.GetRepository(project.Id).Files.Create(new FileUpsert
        {
            Branch = project.DefaultBranch,
            CommitMessage = "test",
            Path = ".gitlab-ci.yml",
            Content = InvalidCIYaml,
        });

        var result = await context.Client.Lint.ValidateProjectCIConfigurationAsync(project.Id.ToString(), new(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Valid, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
            Assert.That(result.Warnings, Is.Empty);
        });
    }
}
