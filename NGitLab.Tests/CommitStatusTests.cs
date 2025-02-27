using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests;

public class CommitStatusTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_get_commit_status()
    {
        using var context = await CommitStatusTestContext.Create();
        var createdCommitStatus = context.AddOrUpdateCommitStatus();

        var commitStatus = context.CommitStatusClient.AllBySha(context.Commit.Id.ToString()).ToList();
        Assert.That(commitStatus.FirstOrDefault()?.Status, Is.Not.Null);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_post_commit_status_with_no_coverage()
    {
        using var context = await CommitStatusTestContext.Create();
        var createdCommitStatus = context.AddOrUpdateCommitStatus(coverage: null);

        Assert.That(createdCommitStatus.Coverage, Is.Null);
    }

    [Test]
    public async Task Test_AddingSameCommitStatusTwice_Throws()
    {
        // Arrange
        using var context = await CommitStatusTestContext.Create();
        var commitStatusCreate = context.SetUpCommitStatusCreate("running");

        _ = context.CommitStatusClient.AddOrUpdate(commitStatusCreate);

        // Act/Assert
        var ex = Assert.Throws<GitLabException>(() => _ = context.CommitStatusClient.AddOrUpdate(commitStatusCreate));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorMessage, Is.EqualTo("Cannot transition status via :run from :running (Reason(s): Status cannot transition via \"run\")"));
    }

    [Test]
    public async Task Test_AddingCommitStatusesWithDifferentNamesOnSameCommit_Succeeds()
    {
        // Arrange
        using var context = await CommitStatusTestContext.Create();
        var commitStatusCreate = context.SetUpCommitStatusCreate("running", "Status 1");

        var commitStatus1 = context.CommitStatusClient.AddOrUpdate(commitStatusCreate);

        // Act
        commitStatusCreate.Name = "Status 2";
        var commitStatus2 = context.CommitStatusClient.AddOrUpdate(commitStatusCreate);

        // Assert
        var properties = typeof(CommitStatusCreate).GetProperties();

        // All properties should be the same except 'Name' & 'Id'
        foreach (var property in properties)
        {
            var value1 = property.GetValue(commitStatus1);
            var value2 = property.GetValue(commitStatus2);

            if (string.Equals(property.Name, nameof(CommitStatus.Name), StringComparison.Ordinal) ||
                string.Equals(property.Name, nameof(CommitStatus.Id), StringComparison.Ordinal))
            {
                Assert.That(value1, Is.Not.EqualTo(value2));
            }
            else
            {
                Assert.That(value1, Is.EqualTo(value2));
            }
        }
    }

    [TestCase(["pending", "failed"])]
    [TestCase(["pending", "canceled"])]
    [TestCase(["pending", "success"])]
    [TestCase(["canceled", "pending"])]
    [TestCase(["success", "pending"])]
    [TestCase(["success", "failed"])]
    [TestCase(["success", "canceled"])]
    [TestCase(["pending", "running", "success"])]
    [TestCase(["pending", "running", "failed"])]
    [TestCase(["pending", "running", "canceled"])]
    public async Task Test_UpdatingCommitStatus_SucceedsIfTransitionSupported(params string[] successiveStates)
    {
        // Arrange
        using var context = await CommitStatusTestContext.Create();
        var commitStatusCreate = context.SetUpCommitStatusCreate("unknown");

        // Act/Assert
        foreach (var state in successiveStates)
        {
            commitStatusCreate.State = state;
            var commitStatus = context.CommitStatusClient.AddOrUpdate(commitStatusCreate);
            Assert.That(commitStatus.Status, Is.EqualTo(state));
        }
    }

    [TestCase(["whatever"])]
    [TestCase(["running", "pending"])]
    public async Task Test_UpdatingCommitStatus_FailsIfStateUnknownOrTransitionUnsupported(params string[] successiveStates)
    {
        // Arrange
        using var context = await CommitStatusTestContext.Create();
        var commitStatusCreate = context.SetUpCommitStatusCreate("unknown");

        for (var i = 0; i < successiveStates.Length - 1; i++)
        {
            var validState = successiveStates[i];
            commitStatusCreate.State = validState;
            var commitStatus = context.CommitStatusClient.AddOrUpdate(commitStatusCreate);
            Assert.That(commitStatus.Status, Is.EqualTo(validState));
        }

        // Act/Assert
        var invalidState = successiveStates[successiveStates.Length - 1];
        commitStatusCreate.State = invalidState;

        var ex = Assert.Throws<GitLabException>(() => _ = context.CommitStatusClient.AddOrUpdate(commitStatusCreate));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorMessage, Does.StartWith("state does not have a valid value").Or
                                         .StartWith("Cannot transition status via"));
    }

    [TestCase("whatever", false)]
    [TestCase(null, true)]  // Will set 'nameToLookUp' to a dynamically created name
    public async Task Test_QueryByName(string nameToLookUp, bool expectToFind)
    {
        // Arrange
        using var context = await CommitStatusTestContext.Create();

        string commitStatusName = null;
        for (var i = 0; i < 10; i++)
        {
            commitStatusName = $"Commit Status {Guid.NewGuid():N}";
            var commitStatusCreate = context.SetUpCommitStatusCreate("running", commitStatusName);
            _ = context.CommitStatusClient.AddOrUpdate(commitStatusCreate);
        }

        // If 'nameToLookUp' is null, use the latest commit status name instead
        nameToLookUp ??= commitStatusName;

        // Act
        var statuses = context.CommitStatusClient.GetAsync(context.Commit.Id.ToString(), new CommitStatusQuery
        {
            Name = nameToLookUp,
        }).ToArray();

        // Assert
        Assert.That(statuses.Count, Is.EqualTo(expectToFind ? 1 : 0));
    }

    private sealed class CommitStatusTestContext : IDisposable
    {
        public GitLabTestContext Context { get; }

        public Project Project { get; }

        public Commit Commit { get; }

        public ICommitStatusClient CommitStatusClient { get; }

        private CommitStatusTestContext(GitLabTestContext context, Project project, Commit commit, ICommitStatusClient commitStatusClient)
        {
            Context = context;
            Project = project;
            Commit = commit;
            CommitStatusClient = commitStatusClient;
        }

        public void Dispose()
        {
            Context.Dispose();
        }

        public static async Task<CommitStatusTestContext> Create()
        {
            var context = await GitLabTestContext.CreateAsync().ConfigureAwait(false);
            var project = context.CreateProject();

            var upsert = new FileUpsert
            {
                RawContent = "test",
                CommitMessage = "Commit for CommitStatusTests",
                Path = "CommitStatusTests.txt",
                Branch = project.DefaultBranch,
            };
            context.Client.GetRepository(project.Id).Files.Create(upsert);
            var commit = context.Client.GetCommits(project.Id).GetCommit(upsert.Branch);
            var client = context.Client.GetCommitStatus(project.Id);
            return new CommitStatusTestContext(context, project, commit, client);
        }

        public CommitStatusCreate AddOrUpdateCommitStatus(string state = "success", int? coverage = null)
        {
            var commitStatusCreate = SetUpCommitStatusCreate(state, coverage: coverage);

            var createdCommitStatus = CommitStatusClient.AddOrUpdate(commitStatusCreate);

            Assert.That(createdCommitStatus.Ref, Is.EqualTo(commitStatusCreate.Ref));
            Assert.That(createdCommitStatus.Coverage, Is.EqualTo(commitStatusCreate.Coverage));
            Assert.That(createdCommitStatus.Description, Is.EqualTo(commitStatusCreate.Description));
            Assert.That(createdCommitStatus.Status, Is.EqualTo(commitStatusCreate.State));
            Assert.That(createdCommitStatus.Name, Is.EqualTo(commitStatusCreate.Name));
            Assert.That(createdCommitStatus.TargetUrl, Is.EqualTo(commitStatusCreate.TargetUrl));
            Assert.That(createdCommitStatus.CommitSha, Is.EqualTo(commitStatusCreate.CommitSha).IgnoreCase);

            return createdCommitStatus;
        }

        public CommitStatusCreate SetUpCommitStatusCreate(string state, string name = null, int? coverage = 100)
        {
            name ??= "Some Commit Status";
            return new CommitStatusCreate
            {
                Ref = Project.DefaultBranch,
                CommitSha = Commit.Id.ToString(),
                Name = name,
                State = state,
                Description = "Description for this commit status",
                Coverage = coverage,
                TargetUrl = "https://google.ca/",
            };
        }
    }
}
