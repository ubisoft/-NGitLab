using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NGitLab.Mock.Internals;
using NGitLab.Models;

namespace NGitLab.Mock.Clients;

internal sealed class RepositoryClient : ClientBase, IRepositoryClient
{
    private readonly long _projectId;

    public RepositoryClient(ClientContext context, ProjectId projectId)
        : base(context)
    {
        _projectId = Server.AllProjects.FindProject(projectId.ValueAsString()).Id;
    }

    public ITagClient Tags => new TagClient(Context, _projectId);

    public IFilesClient Files => new FileClient(Context, _projectId);

    public IBranchClient Branches => new BranchClient(Context, _projectId);

    public IProjectHooksClient ProjectHooks => new ProjectHooksClient(Context, _projectId);

    public IContributorClient Contributors => new ContributorClient(Context, _projectId);

    public IEnumerable<Tree> Tree
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                var project = GetProject(_projectId, ProjectPermission.View);
                return project.Repository.GetTree().ToList();
            }
        }
    }

    public IEnumerable<Commit> Commits
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                var project = GetProject(_projectId, ProjectPermission.View);
                return project.Repository.GetCommits().Select(commit => ConvertToNGitLabCommit(commit, project)).ToList();
            }
        }
    }

    public IEnumerable<Tree> GetTree(string path)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Tree> GetTree(string path, string @ref, bool recursive)
    {
        throw new NotImplementedException();
    }

    public GitLabCollectionResponse<Tree> GetTreeAsync(RepositoryGetTreeOptions options)
    {
        return GitLabCollectionResponse.Create(GetTree(options));
    }

    public IEnumerable<Tree> GetTree(RepositoryGetTreeOptions options)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            return project.Repository.GetTree(options).ToList();
        }
    }

    public void GetRawBlob(string sha, Action<Stream> parser)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            project.Repository.GetRawBlob(sha, parser);
        }
    }

    public void GetArchive(Action<Stream> parser)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Commit> GetCommits(string refName, int maxResults = 0)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            return project.Repository.GetCommits(refName).Select(commit => ConvertToNGitLabCommit(commit, project)).ToList();
        }
    }

    public IEnumerable<Commit> GetCommits(GetCommitsRequest request)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            return project.Repository.GetCommits(request).Select(commit => ConvertToNGitLabCommit(commit, project)).ToList();
        }
    }

    public async IAsyncEnumerable<Commit> GetCommitsAsync(GetCommitsRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            foreach (var commit in project.Repository.GetCommits(request))
            {
                yield return ConvertToNGitLabCommit(commit, project);
            }
        }
    }

    public Commit GetCommit(Sha1 sha)
    {
        throw new NotImplementedException();
    }

    public Task<Commit> GetCommitAsync(Sha1 sha, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Diff> GetCommitDiff(Sha1 sha)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<Diff> GetCommitDiffAsync(Sha1 sha, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Ref> GetCommitRefs(Sha1 sha, CommitRefType type = CommitRefType.All)
    {
        throw new NotImplementedException();
    }

    private static Commit ConvertToNGitLabCommit(LibGit2Sharp.Commit commit, Project project)
    {
        return commit.ToCommitClient(project);
    }

    public CompareResults Compare(CompareQuery query)
    {
        throw new NotImplementedException();
    }
}
