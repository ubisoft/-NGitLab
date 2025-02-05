using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using NGitLab.Mock.Internals;
using NGitLab.Models;

namespace NGitLab.Mock.Clients;

internal sealed class CommitClient : ClientBase, ICommitClient
{
    private readonly long _projectId;

    public CommitClient(ClientContext context, ProjectId projectId)
        : base(context)
    {
        _projectId = Server.AllProjects.FindProject(projectId.ValueAsString()).Id;
    }

    public Commit Create(CommitCreate commit)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.Contribute);
            var gitCommit = project.Repository.Commit(commit);

            return gitCommit.ToCommitClient(project);
        }
    }

    public Commit GetCommit(string @ref)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            var commit = project.Repository.GetCommit(@ref);

            return commit?.ToCommitClient(project);
        }
    }

    public async Task<Commit> GetCommitAsync(string @ref, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return GetCommit(@ref);
    }

    public Commit CherryPick(CommitCherryPick cherryPick)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.Contribute);
            var gitCommit = project.Repository.CherryPick(cherryPick);

            return gitCommit.ToCommitClient(project);
        }
    }

    public JobStatus GetJobStatus(string branchName)
    {
        throw new NotImplementedException();
    }

    public GitLabCollectionResponse<Models.MergeRequest> GetRelatedMergeRequestsAsync(RelatedMergeRequestsQuery query)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);

            var mergeRequests = project.MergeRequests.Where(mr => mr.Commits.SingleOrDefault(
                    commit => commit.Sha.Equals(query.Sha.ToString(), StringComparison.OrdinalIgnoreCase)) != null);

            var relatedMerqueRequests = mergeRequests.Select(mr => mr.ToMergeRequestClient());

            return GitLabCollectionResponse.Create(relatedMerqueRequests);
        }
    }

    public Commit Revert(CommitRevert revert)
    {
        throw new NotImplementedException();
    }
}
