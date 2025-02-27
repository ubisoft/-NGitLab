using System;
using System.Collections.Generic;
using System.Linq;
using NGitLab.Models;

namespace NGitLab.Mock.Clients;

public static class LibGit2SharpExtensions
{
    public static Commit ToCommitClient(this LibGit2Sharp.Commit commit, Project project)
    {
        var commitSha = new Sha1(commit.Sha);
        var commitInfo = project.CommitInfos.SingleOrDefault(c => commitSha.Equals(new Sha1(c.Sha)));
        return new Commit
        {
            AuthoredDate = commit.Author.When.UtcDateTime,
            AuthorEmail = commit.Author.Email,
            AuthorName = commit.Author.Name,
            CommittedDate = commit.Committer.When.UtcDateTime,
            CommitterEmail = commit.Committer.Email,
            CommitterName = commit.Committer.Name,
            CreatedAt = commit.Committer.When.UtcDateTime,
            Id = new Sha1(commit.Sha),
            Message = commit.Message,
            ShortId = commit.Sha[..8],
            Title = commit.MessageShort,
            Parents = commit.Parents.Select(p => new Sha1(p.Sha)).ToArray(),
            Status = commitInfo?.Status ?? "success",
            WebUrl = $"{project.WebUrl}/-/commits/{commit.Sha}",
        };
    }

    public static Branch ToBranchClient(this LibGit2Sharp.Branch branch, Project project)
    {
        var commit = branch.Tip;
        return new Branch
        {
            CanPush = true,
            Protected = false,
            DevelopersCanMerge = true,
            DevelopersCanPush = true,
            Merged = false,
            Name = branch.FriendlyName,
            Default = string.Equals(branch.FriendlyName, project.DefaultBranch, StringComparison.Ordinal),
            Commit = ToCommitInfo(commit),
        };
    }

    public static Models.CommitInfo ToCommitInfo(this LibGit2Sharp.Commit commit)
    {
        return new Models.CommitInfo
        {
            Id = new Sha1(commit.Sha),
            AuthorName = commit.Author.Name,
            AuthorEmail = commit.Author.Email,
            AuthoredDate = commit.Author.When.UtcDateTime,
            CommitterName = commit.Committer.Name,
            CommitterEmail = commit.Committer.Email,
            CommittedDate = commit.Committer.When.UtcDateTime,
            Message = commit.Message,
            Parents = commit.Parents.Select(c => new Sha1(c.Sha)).ToArray(),
        };
    }

    internal static LibGit2Sharp.Commit GetLastCommitForFileChanges(this LibGit2Sharp.Repository repository, string filePath)
    {
        try
        {
            return repository.Commits.QueryBy(filePath).FirstOrDefault()?.Commit;
        }
        catch (KeyNotFoundException)
        {
            // LibGit2Sharp sometimes fails with the following exception
            // System.Collections.Generic.KeyNotFoundException: The given key '1d08df45e551942eaa70d9f5ab6f5f7665a3f5b3' was not present in the dictionary.
            //    at System.Collections.Generic.Dictionary`2.get_Item(TKey key)
            //    at LibGit2Sharp.Core.FileHistory.FullHistory(IRepository repo, String path, CommitFilter filter)+MoveNext() in /_/LibGit2Sharp/Core/FileHistory.cs:line 120
            //    at System.Linq.Enumerable.TryGetFirst[TSource](IEnumerable`1 source, Boolean& found)
            //    at System.Linq.Enumerable.FirstOrDefault[TSource](IEnumerable`1 source)
            //    at NGitLab.Mock.Clients.LibGit2SharpExtensions.GetLastCommitForFileChanges(Repository repository, String filePath) in /_/NGitLab.Mock/Clients/LibGit2SharpExtensions.cs:line 65
            //    at NGitLab.Mock.Repository.GetFile(String filePath, String ref) in /_/NGitLab.Mock/Repository.cs:line 485
            //    at NGitLab.Mock.Clients.FileClient.Get(String filePath, String ref) in /_/NGitLab.Mock/Clients/FileClient.cs:line 77
            //    at NGitLab.Mock.Clients.FileClient.GetAsync(String filePath, String ref, CancellationToken cancellationToken) in /_/NGitLab.Mock/Clients/FileClient.cs:line 125
        }

        return null;
    }
}
