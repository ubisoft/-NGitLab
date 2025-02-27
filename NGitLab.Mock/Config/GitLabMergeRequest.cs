using System;
using System.Collections.Generic;

namespace NGitLab.Mock.Config;

/// <summary>
/// Describe a merge request in a GitLab project
/// </summary>
public class GitLabMergeRequest : GitLabObject<GitLabProject>
{
    public GitLabMergeRequest()
    {
        Comments = new GitLabCommentsCollection(this);
    }

    /// <summary>
    /// Title (required)
    /// </summary>
    public string Title { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Author username (required if default user not defined)
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Assignee username
    /// </summary>
    public string Assignee { get; set; }

    public string SourceBranch { get; set; }

    public string TargetBranch { get; set; }

    /// <summary>
    /// Labels names
    /// </summary>
    public IList<string> Labels { get; } = [];

    /// <summary>
    /// Approvers usernames
    /// </summary>
    public IList<string> Approvers { get; } = [];

    public GitLabCommentsCollection Comments { get; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? MergedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public string Milestone { get; set; }
}

public class GitLabMergeRequestsCollection : GitLabCollection<GitLabMergeRequest, GitLabProject>
{
    internal GitLabMergeRequestsCollection(GitLabProject parent)
        : base(parent)
    {
    }
}
