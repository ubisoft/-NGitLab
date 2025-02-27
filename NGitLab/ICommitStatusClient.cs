using System.Collections.Generic;
using NGitLab.Models;

namespace NGitLab;

public interface ICommitStatusClient
{
    IEnumerable<CommitStatus> AllBySha(string commitSha);

    /// <remarks>
    /// Refer to <see href="https://docs.gitlab.com/ee/api/commits.html#list-the-statuses-of-a-commit">"List the statuses of a commit" GitLab doc</see>
    /// </remarks>
    GitLabCollectionResponse<CommitStatus> GetAsync(string commitSha, CommitStatusQuery query = null);

    /// <remarks>
    /// Refer to <see href="https://docs.gitlab.com/ee/api/commits.html#set-the-pipeline-status-of-a-commit">"Set the pipeline status of a commit" GitLab doc</see>
    /// </remarks>
    CommitStatus AddOrUpdate(CommitStatusCreate status);
}
