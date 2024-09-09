﻿using System;
using System.Collections.Generic;
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
    public async Task Test_can_get_commit_async()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);

        var commit = await context.Client.GetCommits(project.Id).GetCommitAsync(project.DefaultBranch);
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
    public async Task Test_can_get_stats_in_commit_async()
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

        var commit = await context.Client.GetCommits(project.Id).GetCommitAsync(project.DefaultBranch);
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
            Actions = new List<CreateCommitAction>
            {
                new()
                {
                    Action = "update",
                    Content = "Test to cherry-pick",
                    FilePath = "README.md",
                },
            },
        });

        var cherryPickedCommit = commitClient.CherryPick(new CommitCherryPick
        {
            Branch = project.DefaultBranch,
            Sha = commit.Id,
        });

        var latestCommit = commitClient.GetCommit(project.DefaultBranch);
        Assert.That(latestCommit.Id, Is.EqualTo(cherryPickedCommit.Id));
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
            Actions = new List<CreateCommitAction>
            {
                new()
                {
                    Action = "update",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            },
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
            Actions = new List<CreateCommitAction>
            {
                new()
                {
                    Action = "update",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            },
        }), Throws.TypeOf<GitLabException>()
                  .With.Property(nameof(GitLabException.StatusCode)).EqualTo(HttpStatusCode.BadRequest)
                  .With.Message.Contains("start_branch, start_sha are mutually exclusive."));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_create_commit_in_so_called_new_branch_fails_if_branch_exists()
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
            Actions = new List<CreateCommitAction>
            {
                new()
                {
                    Action = "update",
                    Content = "This is in a new branch",
                    FilePath = "README.md",
                },
            },
        }), Throws.TypeOf<GitLabException>()
                  .With.Property(nameof(GitLabException.StatusCode)).EqualTo(HttpStatusCode.BadRequest)
                  .With.Message.Contains("A branch called 'new-branch' already exists."));
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
            Actions = new List<CreateCommitAction>
            {
                new()
                {
                    Action = "create",
                    FilePath = "file.txt",
                    Content = "content",
                },
            },
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
            Actions = new List<CreateCommitAction>
            {
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
            },
        });
        Assert.That(newCommit, Is.Not.Null);

        var diff = context.Client.GetRepository(project.Id).GetCommitDiff(newCommit.Id).Single();
        Assert.That(diff.BMode, Is.EqualTo("100755"));
    }
}
