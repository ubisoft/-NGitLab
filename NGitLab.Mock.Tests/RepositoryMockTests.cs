using NGitLab.Models;
using NUnit.Framework;

namespace NGitLab.Mock.Tests;

public class RepositoryMockTests
{
    [Test]
    public void Test_create_commit_in_new_branch_fails_if_both_start_branch_and_sha_specified()
    {
        // Arrange
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew(project => project.Visibility = VisibilityLevel.Internal);
        var initCommit = project.Repository.Commit(user, "Initial commit");

        var startBranch = project.DefaultBranch;

        Assert.That(() => project.Repository.Commit(new()
        {
            Branch = "new-branch",
            StartBranch = startBranch,
            StartSha = initCommit.Sha,
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
                  .With.Message.Contains("GitLab server returned an error (BadRequest): start_branch, start_sha are mutually exclusive."));
    }

    [Test]
    public void Test_create_a_new_commit_with_start_branch_fails_if_branch_already_exists()
    {
        // Arrange
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew(project => project.Visibility = VisibilityLevel.Internal);
        var initCommit = project.Repository.Commit(user, "Initial commit");

        project.Repository.CreateBranch("new-branch", initCommit.Sha);

        var startBranch = project.DefaultBranch;
        var newBranch = "new-branch";

        // Act & Assert
        Assert.That(() => project.Repository.Commit(new()
        {
            Branch = newBranch,
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
                  .With.Message.Contains($"A branch called '{newBranch}' already exists."));
    }

    [Test]
    public void Test_create_a_new_commit_with_start_sha_fails_if_branch_already_exists()
    {
        // Arrange
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew(project => project.Visibility = VisibilityLevel.Internal);
        var initCommit = project.Repository.Commit(user, "Initial commit");

        project.Repository.CreateBranch("new-branch", initCommit.Sha);

        var startBranch = project.DefaultBranch;
        var newBranch = "new-branch";

        // Act & Assert
        Assert.That(() => project.Repository.Commit(new()
        {
            Branch = newBranch,
            StartSha = initCommit.Sha,
            CommitMessage = "First commit in new branch",
            Actions =
            [
                new()
                {
                    Action = "create",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        }), Throws.TypeOf<GitLabException>()
                  .With.Message.Contains($"A branch called '{newBranch}' already exists."));
    }

    [Test]
    public void Test_create_a_new_commit_on_new_branch_with_start_branch()
    {
        // Arrange
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew(project => project.Visibility = VisibilityLevel.Internal);
        var initCommit = project.Repository.Commit(user, "Initial commit");

        var startBranch = project.DefaultBranch;
        var newBranch = "new-branch";

        // Act & Assert
        var commitMessage = "First commit in new branch";
        var newCommit = project.Repository.Commit(new()
        {
            Branch = newBranch,
            StartBranch = startBranch,
            CommitMessage = commitMessage,
            Actions =
            [
                new()
                {
                    Action = "create",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        });
        Assert.That(newCommit.Message.Trim(), Is.EqualTo(commitMessage));
    }

    [Test]
    public void Test_create_a_new_commit_on_new_branch_with_start_sha()
    {
        // Arrange
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew(project => project.Visibility = VisibilityLevel.Internal);
        var initCommit = project.Repository.Commit(user, "Initial commit");

        var newBranch = "new-branch";

        // Act & Assert
        var commitMessage = "First commit in new branch";
        var newCommit = project.Repository.Commit(new()
        {
            Branch = newBranch,
            StartSha = initCommit.Sha,
            CommitMessage = commitMessage,
            Actions =
            [
                new()
                {
                    Action = "create",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        });
        Assert.That(newCommit.Message.Trim(), Is.EqualTo(commitMessage));
    }

    [Test]
    public void Test_create_a_new_commit_on_nonexistent_branch()
    {
        // Arrange
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew(project => project.Visibility = VisibilityLevel.Internal);
        var initCommit = project.Repository.Commit(user, "Initial commit");

        // Act & Assert
        var commitMessage = "First commit in non existing branch";
        Assert.That(() => project.Repository.Commit(new()
        {
            Branch = "new-branch",
            CommitMessage = commitMessage,
            Actions =
            [
                new()
                {
                    Action = "create",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            ],
        }), Throws.TypeOf<GitLabException>()
                  .With.Message.Contains("You can only create or edit files when you are on a branch."));
    }

    [Test]
    public void Test_create_a_new_commit_on_existing_branch()
    {
        // Arrange
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew(project => project.Visibility = VisibilityLevel.Internal);
        var initCommit = project.Repository.Commit(user, "Initial commit");

        // Act & Assert
        var commitMessage = "First commit in existing branch";
        var newCommit = project.Repository.Commit(new()
        {
            Branch = project.DefaultBranch,
            CommitMessage = commitMessage,
            Actions =
            [
                new()
                {
                    Action = "create",
                    Content = "This is in an existing branch",
                    FilePath = "README.md",
                },
            ],
        });
        Assert.That(newCommit.Message.Trim(), Is.EqualTo(commitMessage));
    }
}
