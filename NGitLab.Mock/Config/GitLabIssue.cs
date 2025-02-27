using System;
using System.Collections.Generic;

namespace NGitLab.Mock.Config;

/// <summary>
/// Describe an issue in a GitLab project
/// </summary>
public class GitLabIssue : GitLabObject<GitLabProject>
{
    public GitLabIssue()
    {
        Comments = new GitLabCommentsCollection(this);
    }

    /// <summary>
    /// Title (required)
    /// </summary>
    public string Title { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Labels names
    /// </summary>
    public IList<string> Labels { get; } = [];

    /// <summary>
    /// Author username (required if default user not defined)
    /// </summary>
    public string Author { get; set; }

    /// <summary>
    /// Assignee username (allow multiple separated by ',')
    /// </summary>
    public string Assignee { get; set; }

    /// <summary>
    /// Milestone title
    /// </summary>
    public string Milestone { get; set; }

    public GitLabCommentsCollection Comments { get; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }
}

public class GitLabIssuesCollection : GitLabCollection<GitLabIssue, GitLabProject>
{
    internal GitLabIssuesCollection(GitLabProject parent)
        : base(parent)
    {
    }
}
