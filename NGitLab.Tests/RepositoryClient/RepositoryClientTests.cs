﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests.RepositoryClient;

public class RepositoryClientTests
{
    private sealed class RepositoryClientTestsContext : IDisposable
    {
        public const string SubfolderName = "subfolder";

        public RepositoryClientTestsContext(GitLabTestContext context, Project project, Commit[] commits, IRepositoryClient repositoryClient)
        {
            Context = context;
            Project = project;
            Commits = commits;
            RepositoryClient = repositoryClient;
        }

        public GitLabTestContext Context { get; }

        public Project Project { get; }

        public Commit[] Commits { get; }

        public IRepositoryClient RepositoryClient { get; }

        public void Dispose()
        {
            Context.Dispose();
        }

        public static async Task<RepositoryClientTestsContext> CreateAsync(int commitCount)
        {
            var context = await GitLabTestContext.CreateAsync().ConfigureAwait(false);
            var project = context.CreateProject();
            var repositoryClient = context.Client.GetRepository(project.Id);

            var commits = new Commit[commitCount];
            for (var i = 0; i < commits.Length; i++)
            {
                commits[i] = context.Client.GetCommits(project.Id).Create(new CommitCreate
                {
                    Branch = project.DefaultBranch,
                    CommitMessage = context.GetUniqueRandomString(),
                    AuthorEmail = "a@example.com",
                    AuthorName = "a",
                    Actions =
                    {
                        new CreateCommitAction
                        {
                            Action = "create",
                            Content = "test",
                            FilePath =  // Spread files among the root directory and its 'subfolder'
                                i % 2 == 0 ?
                                $"test{i.ToString(CultureInfo.InvariantCulture)}.md" :
                                $"{SubfolderName}/test{i.ToString(CultureInfo.InvariantCulture)}.md",
                        },
                    },
                });
            }

            return new RepositoryClientTestsContext(context, project, commits, repositoryClient);
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task GetAllCommits()
    {
        const int commitCount = 5;
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount);

        var commits = context.RepositoryClient.Commits.ToArray();

        Assert.That(commits, Has.Length.EqualTo(commitCount));
        Assert.That(commits.Select(c => c.Message), Is.EqualTo(context.Commits.Select(c => c.Message).Reverse()).AsCollection);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetCommitByBranchName()
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);
        var defaultBranch = context.Project.DefaultBranch;

        Assert.That(context.RepositoryClient.GetCommits(defaultBranch), Is.Not.Empty);
        Assert.That(context.RepositoryClient.GetCommits(defaultBranch, -1), Is.Not.Empty);

        var commits = context.RepositoryClient.GetCommits(defaultBranch, 1).ToArray();
        Assert.That(commits, Has.Length.EqualTo(1));
        Assert.That(commits[0].Message, Is.EqualTo(context.Commits[1].Message));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetCommitBySha1()
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);

        var sha1 = context.Commits[0].Id;
        var commit = context.RepositoryClient.GetCommit(sha1);
        Assert.That(commit.Id, Is.EqualTo(sha1));
        Assert.That(commit.Message, Is.EqualTo(context.Commits[0].Message));
        Assert.That(commit.WebUrl, Is.EqualTo(context.Commits[0].WebUrl));
        Assert.That(commit.WebUrl, Is.Not.Null);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetCommitBySha1Range()
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 5);

        var allCommits = context.RepositoryClient.Commits.Reverse().ToArray();
        var commitRequest = new GetCommitsRequest
        {
            RefName = $"{allCommits[1].Id}..{allCommits[3].Id}",
            FirstParent = true,
        };

        var commits = context.RepositoryClient.GetCommits(commitRequest).Reverse().ToArray();
        Assert.That(commits[0].Id, Is.EqualTo(allCommits[2].Id));
        Assert.That(commits[1].Id, Is.EqualTo(allCommits[3].Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetCommitsSince()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 5);

        var defaultBranch = context.Project.DefaultBranch;
        var since = DateTime.UtcNow;
        var expectedSinceValue = Uri.EscapeDataString(since.ToString("s", CultureInfo.InvariantCulture));
        var commitRequest = new GetCommitsRequest
        {
            RefName = defaultBranch,
            Since = since,
        };

        // Act
        var commits = context.RepositoryClient.GetCommits(commitRequest).ToArray();

        // Assert
        var lastRequestQueryString = context.Context.LastRequest.RequestUri.Query;

        Assert.That(lastRequestQueryString, Does.Contain($"since={expectedSinceValue}"));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetCommitsDoesntIncludeSinceWhenNotSpecified()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 5);

        var defaultBranch = context.Project.DefaultBranch;
        var commitRequest = new GetCommitsRequest
        {
            RefName = defaultBranch,
            Since = null,
        };

        // Act
        var commits = context.RepositoryClient.GetCommits(commitRequest).ToArray();

        // Assert
        var lastRequestQueryString = context.Context.LastRequest.RequestUri.Query;

        Assert.That(lastRequestQueryString, Does.Not.Contain("since="));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetCommitsUntil()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 5);

        var defaultBranch = context.Project.DefaultBranch;
        var until = DateTime.UtcNow;
        var expectedUntilValue = Uri.EscapeDataString(until.ToString("s", CultureInfo.InvariantCulture));
        var commitRequest = new GetCommitsRequest
        {
            RefName = defaultBranch,
            Until = until,
        };

        // Act
        var commits = context.RepositoryClient.GetCommits(commitRequest).ToArray();

        // Assert
        var lastRequestQueryString = context.Context.LastRequest.RequestUri.Query;

        Assert.That(lastRequestQueryString, Does.Contain($"until={expectedUntilValue}"));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetCommitsDoesntIncludeUntilWhenNotSpecified()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 5);

        var defaultBranch = context.Project.DefaultBranch;
        var commitRequest = new GetCommitsRequest
        {
            RefName = defaultBranch,
            Until = null,
        };

        // Act
        var commits = context.RepositoryClient.GetCommits(commitRequest).ToArray();

        // Assert
        var lastRequestQueryString = context.Context.LastRequest.RequestUri.Query;

        Assert.That(lastRequestQueryString, Does.Not.Contain("until="));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetCommitDiff()
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);

        Assert.That(context.RepositoryClient.GetCommitDiff(context.RepositoryClient.Commits.First().Id).ToArray(), Is.Not.Empty);
    }

    [TestCase(4)]
    [TestCase(11)]
    [NGitLabRetry]
    public async Task GetAllTreeObjectsAtRoot(int commitCount)
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount);

        var treeObjects = context.RepositoryClient.GetTree(string.Empty).ToList();

        var expectedFileCount = (int)Math.Ceiling(commitCount / 2.0);
        var expectedDirCount = 1;

        Assert.That(treeObjects.Count(t => t.Type == ObjectType.blob), Is.EqualTo(expectedFileCount));
        Assert.That(treeObjects.Count(t => t.Type == ObjectType.tree), Is.EqualTo(expectedDirCount));
        Assert.That(treeObjects.All(t => string.Equals(t.Path, t.Name, StringComparison.Ordinal)), Is.True);
    }

    [TestCase(4)]
    [TestCase(11)]
    [NGitLabRetry]
    public async Task GetAllTreeObjectsRecursivelyFromRoot(int commitCount)
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount);

        var treeObjects = context.RepositoryClient.GetTree(string.Empty, @ref: null, recursive: true).ToList();

        var expectedFileCount = commitCount;
        var expectedDirCount = 1;

        Assert.That(treeObjects.Count(t => t.Type == ObjectType.blob), Is.EqualTo(expectedFileCount));
        Assert.That(treeObjects.Count(t => t.Type == ObjectType.tree), Is.EqualTo(expectedDirCount));
    }

    [TestCase(4)]
    [TestCase(11)]
    [NGitLabRetry]
    public async Task GetAllTreeObjectsRecursivelyFromRootAsync(int commitCount)
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount);

        var treeObjects = new List<Tree>();
        await foreach (var item in context.RepositoryClient.GetTreeAsync(new RepositoryGetTreeOptions { Path = string.Empty, Ref = null, Recursive = true }))
        {
            treeObjects.Add(item);
        }

        var expectedFileCount = commitCount;
        var expectedDirCount = 1;

        Assert.That(treeObjects.Count(t => t.Type == ObjectType.blob), Is.EqualTo(expectedFileCount));
        Assert.That(treeObjects.Count(t => t.Type == ObjectType.tree), Is.EqualTo(expectedDirCount));
    }

    [TestCase(4)]
    [TestCase(11)]
    [NGitLabRetry]
    public async Task GetAllTreeObjectsRecursivelyFromSubfolderAsync(int commitCount)
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount);

        var treeObjects = new List<Tree>();
        await foreach (var item in context.RepositoryClient.GetTreeAsync(new RepositoryGetTreeOptions { Path = RepositoryClientTestsContext.SubfolderName, Ref = null, Recursive = true }))
        {
            treeObjects.Add(item);
        }

        Assert.That(treeObjects.All(t => t.Path.StartsWith(RepositoryClientTestsContext.SubfolderName, StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetAllTreeObjectsInPathOnRef()
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);

        var treeObjects = context.RepositoryClient.GetTree(string.Empty, context.Project.DefaultBranch, recursive: false);
        Assert.That(treeObjects, Is.Not.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetAllTreeObjectsInPathWith100ElementsByPage()
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);

        var treeObjects = context.RepositoryClient.GetTree(new RepositoryGetTreeOptions { Path = string.Empty, PerPage = 100 });
        Assert.That(treeObjects, Is.Not.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetAllTreeObjectsAtInvalidPath()
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);

        var treeObjects = context.RepositoryClient.GetTree("Fakepath");
        Assert.That(treeObjects, Is.Empty);
    }

    [TestCase(CommitRefType.All)]
    [TestCase(CommitRefType.Branch)]
    [TestCase(CommitRefType.Tag)]
    [NGitLabRetry]
    public async Task GetCommitRefs(CommitRefType type)
    {
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);

        var commitRefs = context.RepositoryClient.GetCommitRefs(context.RepositoryClient.Commits.First().Id, type).ToArray();

        if (type == CommitRefType.Tag)
        {
            Assert.That(commitRefs, Is.Empty);
        }
        else
        {
            Assert.That(commitRefs, Is.Not.Empty);
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task GetArchive_NoQuerySpecified_PathConstructedWithNoParameters()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2).ConfigureAwait(false);

        // Act
        context.RepositoryClient.GetArchive((stream) => { });

        // Assert
        var requestPathAndQuery = context.Context.LastRequest.RequestUri.PathAndQuery;

        Assert.Multiple(() =>
        {
            Assert.That(requestPathAndQuery, Is.Not.Null);
            Assert.That(requestPathAndQuery.EndsWith("/archive", StringComparison.OrdinalIgnoreCase), Is.True);
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task GetArchive_QueryInstanceIsNull_PathConstructedWithNoParameters()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2).ConfigureAwait(false);
        var firstCommitId = context.Commits[0].Id.ToString();

        // Act
        context.RepositoryClient.GetArchive((stream) => { }, fileArchiveQuery: null);

        // Assert
        var requestPathAndQuery = context.Context.LastRequest.RequestUri.PathAndQuery;

        Assert.Multiple(() =>
        {
            Assert.That(requestPathAndQuery, Is.Not.Null);
            Assert.That(requestPathAndQuery.EndsWith("/archive", StringComparison.OrdinalIgnoreCase), Is.True);
        });
    }

    [TestCase(null, "")]
    [TestCase(FileArchiveFormat.Bz2, ".bz2")]
    [TestCase(FileArchiveFormat.Gz, ".gz")]
    [TestCase(FileArchiveFormat.Tar, ".tar")]
    [TestCase(FileArchiveFormat.TarBz2, ".tar.bz2")]
    [TestCase(FileArchiveFormat.TarGz, ".tar.gz")]
    [TestCase(FileArchiveFormat.Tb2, ".tb2")]
    [TestCase(FileArchiveFormat.Tbz2, ".tbz2")]
    [TestCase(FileArchiveFormat.Zip, ".zip")]
    [NGitLabRetry]
    public async Task GetArchive_QuerySpecifiesFormatValue_ArchiveExtensionPassedCorrectly(
        FileArchiveFormat? archiveFormat, string expectedExtension)
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);
        var fileArchiveQuery = new FileArchiveQuery
        {
            Format = archiveFormat,
        };

        // Act
        context.RepositoryClient.GetArchive((stream) => { }, fileArchiveQuery);

        // Assert
        var requestPathAndQuery = context.Context.LastRequest.RequestUri.PathAndQuery;

        Assert.Multiple(() =>
        {
            Assert.That(requestPathAndQuery, Is.Not.Null);
            Assert.That(requestPathAndQuery.EndsWith($"/archive{expectedExtension}",
                StringComparison.OrdinalIgnoreCase), Is.True);
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task GetArchive_QuerySpecifiesRevision_ShaValuePassedCorrectly()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);
        var firstCommitId = context.Commits[0].Id.ToString();
        var fileArchiveQuery = new FileArchiveQuery
        {
            Ref = firstCommitId,
        };

        // Act
        context.RepositoryClient.GetArchive((stream) => { }, fileArchiveQuery);

        // Assert
        var requestPathAndQuery = context.Context.LastRequest.RequestUri.PathAndQuery;

        Assert.Multiple(() =>
        {
            Assert.That(requestPathAndQuery, Is.Not.Null);
            Assert.That(requestPathAndQuery.Contains($"sha={firstCommitId}",
                StringComparison.OrdinalIgnoreCase), Is.True);
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task GetArchive_QuerySpecifiesPath_PathValuePassedCorrectly()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);
        var path = RepositoryClientTestsContext.SubfolderName;
        var fileArchiveQuery = new FileArchiveQuery
        {
            Path = path,
        };

        // Act
        context.RepositoryClient.GetArchive((stream) => { }, fileArchiveQuery);

        // Assert
        var requestPathAndQuery = context.Context.LastRequest.RequestUri.PathAndQuery;

        Assert.Multiple(() =>
        {
            Assert.That(requestPathAndQuery, Is.Not.Null);
            Assert.That(requestPathAndQuery.Contains($"path={path}", StringComparison.OrdinalIgnoreCase), Is.True);
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task GetArchive_QuerySpecifiesAllParameters_AllParametersPassedCorrectly()
    {
        // Arrange
        using var context = await RepositoryClientTestsContext.CreateAsync(commitCount: 2);
        var firstCommitId = context.Commits[0].Id.ToString();
        var path = RepositoryClientTestsContext.SubfolderName;
        var fileArchiveQuery = new FileArchiveQuery
        {
            Format = FileArchiveFormat.Zip,
            Path = path,
            Ref = firstCommitId,
        };

        // Act
        context.RepositoryClient.GetArchive((stream) => { }, fileArchiveQuery);

        // Assert
        var requestPathAndQuery = context.Context.LastRequest.RequestUri.PathAndQuery;

        Assert.Multiple(() =>
        {
            Assert.That(requestPathAndQuery, Is.Not.Null);
            Assert.That(requestPathAndQuery.Contains($"/archive.zip", StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(requestPathAndQuery.Contains($"path={path}", StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(requestPathAndQuery.Contains($"sha={firstCommitId}", StringComparison.OrdinalIgnoreCase), Is.True);
        });
    }
}
