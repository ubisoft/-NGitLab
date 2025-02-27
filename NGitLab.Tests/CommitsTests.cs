using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests;

public class CommitsTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_can_get_commit()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);

        var commit = context.Client.GetCommits(project.Id).GetCommit(project.DefaultBranch);
        Assert.That(commit.Message, Is.Not.Null);
        Assert.That(commit.ShortId, Is.Not.Null);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_get_stats_in_commit()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);
        context.Client.GetRepository(project.Id).Files.Create(new FileUpsert
        {
            Branch = project.DefaultBranch,
            CommitMessage = "file to be updated",
            Path = "CommitStats.txt",
            RawContent = "I'm defective and i need to be fixeddddddddddddddd",
        });

        context.Client.GetRepository(project.Id).Files.Update(new FileUpsert
        {
            Branch = project.DefaultBranch,
            CommitMessage = "fixing the file",
            Path = "CommitStats.txt",
            RawContent = "I'm no longer defective and i have been fixed\n\n\r\n\r\rEnjoy",
        });

        var commit = context.Client.GetCommits(project.Id).GetCommit(project.DefaultBranch);
        Assert.That(commit.Stats.Additions, Is.EqualTo(4));
        Assert.That(commit.Stats.Deletions, Is.EqualTo(1));
        Assert.That(commit.Stats.Total, Is.EqualTo(5));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_get_merge_request_associated_to_commit()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();

        context.Client.GetRepository(project.Id).Branches.Create(new BranchCreate { Name = "test-mr", Ref = project.DefaultBranch });

        var commit = context.Client.GetCommits(project.Id).Create(new CommitCreate
        {
            Branch = "test-mr",
            CommitMessage = "Test to retrieve MR from commit sha",
        });

        var mergeRequestTitle = "Test to retrieve MR from commit sha";
        context.Client.GetMergeRequest(project.Id).Create(new MergeRequestCreate
        {
            SourceBranch = "test-mr",
            TargetBranch = project.DefaultBranch,
            Title = mergeRequestTitle,
        });

        var mergeRequests = await GitLabTestContext.RetryUntilAsync(
            () => context.Client.GetCommits(project.Id).GetRelatedMergeRequestsAsync(new RelatedMergeRequestsQuery { Sha = commit.Id }),
            mergeRequests => mergeRequests.Any(),
            TimeSpan.FromSeconds(10));

        var mergeRequest = mergeRequests.Single();
        Assert.That(mergeRequest.Title, Is.EqualTo(mergeRequestTitle));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_cherry_pick_commit()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var repository = context.Client.GetRepository(project.Id);
        var commitClient = context.Client.GetCommits(project.Id);

        repository.Branches.Create(new BranchCreate { Name = "test-cherry-pick", Ref = project.DefaultBranch });

        var commit = commitClient.Create(new CommitCreate
        {
            Branch = "test-cherry-pick",
            CommitMessage = "Test to cherry-pick",
            Actions =
            [
                new()
                {
                    Action = "update",
                    Content = "Test to cherry-pick",
                    FilePath = "README.md",
                },
            ],
        });

        var cherryPickedCommit = commitClient.CherryPick(new CommitCherryPick
        {
            Branch = project.DefaultBranch,
            Sha = commit.Id,
        });

        var latestCommit = commitClient.GetCommit(project.DefaultBranch);
        Assert.That(latestCommit.Id, Is.EqualTo(cherryPickedCommit.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_revert_commit()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();

        var testBranchName = "revert-test";

        var repositoryClient = context.Client.GetRepository(project.Id);
        repositoryClient.Branches.Create(new BranchCreate
        {
            Name = testBranchName,
            Ref = project.DefaultBranch,
        });

        var commitClient = context.Client.GetCommits(project.Id);
        var testCommit = commitClient.Create(new CommitCreate
        {
            Branch = testBranchName,
            CommitMessage = "This commit will be reverted",
            Actions =
            [
                new()
                {
                    Action = "update",
                    Content = "Testing commit revert",
                    FilePath = "README.md",
                },
            ],
        });

        var compareResults = repositoryClient.Compare(new CompareQuery(project.DefaultBranch, testBranchName));
        Assert.That(compareResults.Diff, Has.Length.EqualTo(1));

        // Act
        var revertedCommit = commitClient.Revert(new CommitRevert
        {
            Branch = testBranchName,
            Sha = testCommit.Id,
        });

        // Assert
        compareResults = repositoryClient.Compare(new CompareQuery(project.DefaultBranch, testBranchName));
        Assert.That(compareResults.Diff, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    [NGitLabRetry]
    public async Task Test_create_commit_in_new_branch_can_create_branch(bool useBranchName)
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);

        var commitClient = context.Client.GetCommits(project.Id);

        var startBranch = project.DefaultBranch;
        var startCommit = commitClient.GetCommit(startBranch);

        // Act
        var commit = commitClient.Create(new CommitCreate
        {
            Branch = "new-branch",
            StartBranch = useBranchName ? startBranch : null,
            StartSha = useBranchName ? null : startCommit.Id.ToString().ToLowerInvariant(),
            CommitMessage = "First commit in new branch",
            Actions =
            [
                new()
                {
                    Action = "update",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        });

        // Assert
        Assert.That(commit.Id, Is.Not.EqualTo(startCommit.Id));
        Assert.That(commit.Parents, Has.Length.EqualTo(1));
        Assert.That(commit.Parents[0], Is.EqualTo(startCommit.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_create_commit_in_new_branch_fails_if_both_start_branch_and_sha_specified()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);

        var commitClient = context.Client.GetCommits(project.Id);

        var startBranch = project.DefaultBranch;
        var startCommit = commitClient.GetCommit(startBranch);

        // Act/Assert
        Assert.That(() => commitClient.Create(new CommitCreate
        {
            Branch = "new-branch",
            StartBranch = startBranch,
            StartSha = startCommit.Id.ToString().ToLowerInvariant(),
            CommitMessage = "First commit in new branch",
            Actions =
            [
                new()
                {
                    Action = "update",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        }), Throws.TypeOf<GitLabException>()
                  .With.Property(nameof(GitLabException.StatusCode)).EqualTo(HttpStatusCode.BadRequest)
                  .With.Message.Contains("start_branch, start_sha are mutually exclusive."));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_create_a_new_commit_with_start_branch_fails_if_branch_already_exists()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);

        var repoClient = context.Client.GetRepository(project.Id);
        var commitClient = context.Client.GetCommits(project.Id);

        var startBranch = project.DefaultBranch;

        repoClient.Branches.Create(new BranchCreate { Name = "new-branch", Ref = startBranch });

        // Act/Assert
        Assert.That(() => commitClient.Create(new CommitCreate
        {
            Branch = "new-branch",
            StartBranch = startBranch,
            CommitMessage = "First commit in new branch",
            Actions =
            [
                new()
                {
                    Action = "update",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        }), Throws.TypeOf<GitLabException>()
                  .With.Property(nameof(GitLabException.StatusCode)).EqualTo(HttpStatusCode.BadRequest)
                  .With.Message.Contains("A branch called 'new-branch' already exists."));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_create_a_new_commit_with_start_sha_fails_if_branch_already_exists()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);

        var repoClient = context.Client.GetRepository(project.Id);
        var commitClient = context.Client.GetCommits(project.Id);
        var startCommit = context.Client.GetCommits(project.Id).GetCommit(project.DefaultBranch);

        var startBranch = project.DefaultBranch;
        var startSha = startCommit.Id;

        repoClient.Branches.Create(new BranchCreate { Name = "new-branch", Ref = startBranch });

        // Act/Assert
        Assert.That(() => commitClient.Create(new CommitCreate
        {
            Branch = "new-branch",
            StartSha = startSha.ToString().ToLowerInvariant(),
            CommitMessage = "First commit in new branch",
            Actions =
            [
                new()
                {
                    Action = "update",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        }), Throws.TypeOf<GitLabException>()
                  .With.Property(nameof(GitLabException.StatusCode)).EqualTo(HttpStatusCode.BadRequest)
                  .With.Message.Contains("A branch called 'new-branch' already exists."));
    }

    [Test]
    public async Task Test_create_commit_on_nonexistent_branch_fails_when_neither_start_branch_nor_start_sha_specified()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);

        var commitClient = context.Client.GetCommits(project.Id);

        // Act/Assert
        Assert.That(() => commitClient.Create(new CommitCreate
        {
            Branch = "new-branch",
            CommitMessage = "First commit in new branch",
            Actions =
            [
                new()
                {
                    Action = "update",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        }), Throws.TypeOf<GitLabException>()
                  .With.Property(nameof(GitLabException.StatusCode)).EqualTo(HttpStatusCode.BadRequest)
                  .With.Message.Contains("You can only create or edit files when you are on a branch"));
    }

    [Test]
    public async Task Test_create_commit_on_empty_repo()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();

        var commitClient = context.Client.GetCommits(project.Id);

        // Act
        var commit = commitClient.Create(new CommitCreate
        {
            Branch = "main",
            CommitMessage = "Initial commit",
            Actions =
            [
                new()
                {
                    Action = "create",
                    Content = "my wonderful readme",
                    FilePath = "README.md",
                },
            ],
        });

        // Assert
        Assert.That(commit.Id, Is.Not.EqualTo(default(Sha1)));
        Assert.That(commit.Parents, Is.Empty);
    }

    [Test]
    public async Task Test_commit_can_be_created_from_sha()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);
        var commitClient = context.Client.GetCommits(project.Id);

        var commit = commitClient.GetCommit("main");
        Assert.That(commit, Is.Not.Null);

        var newCommit = commitClient.Create(new CommitCreate
        {
            Branch = "test-start-sha",
            CommitMessage = "New commit",
            StartSha = commit.Id.ToString().ToLowerInvariant(),
            Actions =
            [
                new()
                {
                    Action = "create",
                    FilePath = "file.txt",
                    Content = "content",
                },
            ],
        });
        Assert.That(newCommit, Is.Not.Null);
    }

    [Test]
    public async Task Test_commit_can_set_executable_flag()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);
        var commitClient = context.Client.GetCommits(project.Id);

        var commit = commitClient.GetCommit("main");
        Assert.That(commit, Is.Not.Null);

        var newCommit = commitClient.Create(new CommitCreate
        {
            Branch = "test-set-executable-flag",
            CommitMessage = "New commit",
            StartBranch = project.DefaultBranch,
            Actions =
            [
                new()
                {
                    Action = "create",
                    FilePath = "script.sh",
                    Content = "echo 'Hello, world!'",
                },
                new()
                {
                    Action = "chmod",
                    FilePath = "script.sh",
                    IsExecutable = true,
                },
            ],
        });
        Assert.That(newCommit, Is.Not.Null);

        var diff = context.Client.GetRepository(project.Id).GetCommitDiff(newCommit.Id).Single();
        Assert.That(diff.BMode, Is.EqualTo("100755"));
    }
}
