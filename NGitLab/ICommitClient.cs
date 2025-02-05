using System;
using System.Threading.Tasks;
using System.Threading;
using NGitLab.Models;

namespace NGitLab;

public interface ICommitClient
{
    /// <summary>
    /// Returns the status of a branch.
    /// </summary>
    [Obsolete("This endpoint does not always return data consistent with the pipeline list. " +
              "Consider using PipelineClient.Search() instead.")]
    JobStatus GetJobStatus(string branchName);

    /// <summary>
    /// Create a commit
    /// </summary>
    Commit Create(CommitCreate commit);

    /// <summary>
    /// Get a specific commit identified by the commit hash or name of a branch or tag.
    /// </summary>
    Commit GetCommit(string @ref);

    /// <summary>
    /// Get a specific commit identified by the commit hash or name of a branch or tag.
    /// </summary>
    Task<Commit> GetCommitAsync(string @ref, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cherry-picks a commit to a given branch.
    /// </summary>
    Commit CherryPick(CommitCherryPick cherryPick);

    /// <summary>
    /// Reverts a specific branch commit
    /// </summary>
    Commit Revert(CommitRevert revert);

    /// <summary>
    /// Get merge requests related to a commit
    /// </summary>
    GitLabCollectionResponse<MergeRequest> GetRelatedMergeRequestsAsync(RelatedMergeRequestsQuery query);
}
