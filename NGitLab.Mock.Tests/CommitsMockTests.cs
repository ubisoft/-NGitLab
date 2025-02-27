using System.Linq;
using NGitLab.Mock.Config;
using NGitLab.Models;
using NUnit.Framework;

namespace NGitLab.Mock.Tests;

public class CommitsMockTests
{
    [Test]
    public void Test_commits_added_can_be_found()
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, configure: project => project
                .WithCommit("Initial commit")
                .WithCommit("Create branch", sourceBranch: "branch-01"))
            .BuildServer();

        var client = server.CreateClient();
        var commit = client.GetCommits(1).GetCommit("branch-01");

        Assert.That(commit.Message.TrimEnd('\r', '\n'), Is.EqualTo("Create branch"));
    }

    [Test]
    public void Test_commits_with_tags_can_be_found()
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, configure: project => project
                .WithCommit("Initial commit")
                .WithCommit("Changes with tag", tags: new[] { "1.0.0" }))
            .BuildServer();

        var client = server.CreateClient();
        var commit = client.GetCommits(1).GetCommit("1.0.0");

        Assert.That(commit.Message.TrimEnd('\r', '\n'), Is.EqualTo("Changes with tag"));
    }

    [Test]
    public void Test_tags_from_commit_can_be_found()
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, addDefaultUserAsMaintainer: true, configure: project => project
                .WithCommit("Initial commit")
                .WithCommit("Changes with tag", tags: new[] { "1.0.0" }))
            .BuildServer();

        var client = server.CreateClient();
        var tags = client.GetRepository(1).Tags.All.ToArray();

        Assert.That(tags, Has.One.Items);
        Assert.That(tags[0].Name, Is.EqualTo("1.0.0"));
    }

    [Test]
    public void Test_two_branches_can_be_created_from_same_commit()
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, addDefaultUserAsMaintainer: true, defaultBranch: "main", configure: project => project
                .WithCommit("Initial commit")
                .WithCommit("Commit for branch_1", sourceBranch: "branch_1")
                .WithCommit("Commit for branch_2", sourceBranch: "branch_2", fromBranch: "main"))
            .BuildServer();

        var client = server.CreateClient();
        var repository = client.GetRepository(1);
        var commitFromBranch1 = repository.GetCommits("branch_1").FirstOrDefault();
        var commitFromBranch2 = repository.GetCommits("branch_2").FirstOrDefault();

        Assert.That(commitFromBranch1, Is.Not.Null);
        Assert.That(commitFromBranch2, Is.Not.Null);
        Assert.That(commitFromBranch1.Parents, Is.Not.Empty);
        Assert.That(commitFromBranch2.Parents, Is.Not.Empty);
        Assert.That(commitFromBranch2.Parents[0], Is.EqualTo(commitFromBranch1.Parents[0]));
    }

    [Test]
    public void Test_GetCommitsBetweenTwoRefs()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, addDefaultUserAsMaintainer: true, defaultBranch: "main", configure: project => project
                .WithCommit("Initial commit")
                .WithCommit("Commit for branch_1", sourceBranch: "branch_1")
                .WithCommit("Yet another commit for branch_1"))
            .BuildServer();

        var client = server.CreateClient();
        var repository = client.GetRepository(1);

        // Act
        var intermediateCommits = repository.GetCommits("main..branch_1");

        // Assert
        Assert.That(intermediateCommits.Select(c => c.Title), Is.EqualTo(new[]
        {
            "Yet another commit for branch_1",
            "Commit for branch_1",
        }).AsCollection);
    }

    [Test]
    public void Test_commits_can_be_cherry_pick()
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, addDefaultUserAsMaintainer: true, configure: project => project
                .WithCommit("Initial commit")
                .WithCommit("Changes with tag", sourceBranch: "branch_1"))
            .BuildServer();

        var client = server.CreateClient();
        var repository = client.GetRepository(1);
        var commitFromBranch1 = repository.GetCommits("branch_1").FirstOrDefault();
        Assert.That(commitFromBranch1, Is.Not.Null);

        var cherryPicked = client.GetCommits(1).CherryPick(new CommitCherryPick
        {
            Sha = commitFromBranch1.Id,
            Branch = "main",
        });
        Assert.That(cherryPicked, Is.Not.Null);
    }

    [Test]
    public void Test_commit_with_file_in_subdirectory()
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, addDefaultUserAsMaintainer: true, configure: project => project
                .WithCommit("Initial commit"))
            .BuildServer();

        var client = server.CreateClient();
        var commits = client.GetCommits(1);
        var newCommit = commits.Create(new CommitCreate
        {
            CommitMessage = "Commit with file in subdirectory",
            Branch = "test-branch",
            StartSha = commits.GetCommit("main").Id.ToString().ToLowerInvariant(),
            Actions = new[]
            {
                new CreateCommitAction
                {
                    Action = "create",
                    FilePath = "subdirectory/file.txt",
                    Content = "Hello, World!",
                },
            },
        });
        Assert.That(newCommit, Is.Not.Null);
    }

    [Test]
    public void Test_create_commit_with_start_branch_and_start_sha()
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, addDefaultUserAsMaintainer: true, configure: project => project
                .WithCommit("Initial commit"))
            .BuildServer();

        var client = server.CreateClient();
        var commits = client.GetCommits(1);
        var handler = () => commits.Create(new CommitCreate
        {
            CommitMessage = "Commit with file in subdirectory",
            Branch = "test-branch",
            StartBranch = "main",
            StartSha = commits.GetCommit("main").Id.ToString().ToLowerInvariant(),
            Actions = new[]
            {
                new CreateCommitAction
                {
                    Action = "create",
                    FilePath = "subdirectory/file.txt",
                    Content = "Hello, World!",
                },
            },
        });

        Assert.That(handler, Throws.TypeOf<GitLabException>()
            .With.Message.Contains("start_branch, start_sha are mutually exclusive."));
    }

    [Test]
    public void Test_create_commit_with_existing_branch()
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, addDefaultUserAsMaintainer: true, configure: project => project
                .WithCommit("Initial commit"))
            .BuildServer();

        var client = server.CreateClient();

        var startBranch = client.Projects[1].DefaultBranch;
        client.GetRepository(1).Branches.Create(new() { Name = "test-branch", Ref = startBranch });

        var commits = client.GetCommits(1);
        var handler = () => commits.Create(new CommitCreate
        {
            CommitMessage = "Commit with file in subdirectory",
            Branch = "test-branch",
            StartBranch = startBranch,
            Actions = new[]
            {
                new CreateCommitAction
                {
                    Action = "create",
                    FilePath = "subdirectory/file.txt",
                    Content = "Hello, World!",
                },
            },
        });

        Assert.That(handler, Throws.TypeOf<GitLabException>()
            .With.Message.Contains("A branch called 'test-branch' already exists."));
    }

    [TestCase("main")]
    [TestCase("other-than-main")]
    public void Test_create_commit_on_empty_repo(string branch)
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithProject("test-project", id: 1, addDefaultUserAsMaintainer: true)
            .BuildServer();

        var client = server.CreateClient();
        var commits = client.GetCommits(1);
        var newCommit = commits.Create(new CommitCreate
        {
            CommitMessage = "Initial commit",
            Branch = branch,
            Actions = new[]
            {
                new CreateCommitAction
                {
                    Action = "create",
                    FilePath = "readme.md",
                    Content = "my wonderful readme",
                },
            },
        });

        Assert.That(newCommit, Is.Not.Null);
        var branches = client.GetRepository(1).Branches.All.Select(b => b.Name).ToList();
        Assert.That(branches, Contains.Item(branch));
    }
}
