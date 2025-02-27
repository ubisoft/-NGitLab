using System.Collections.Generic;

namespace NGitLab.Mock.Config;

/// <summary>
/// Describe a commit in a GitLab project
/// </summary>
public class GitLabCommit : GitLabObject<GitLabProject>
{
    /// <summary>
    /// Author username (required if default user not defined)
    /// </summary>
    public string User { get; set; }

    public string Message { get; set; }

    /// <summary>
    /// Alias to reference it in pipeline
    /// </summary>
    public string Alias { get; set; }

    /// <summary>
    /// Files in the repository at this commit
    /// </summary>
    public IList<GitLabFileDescriptor> Files { get; } = [];

    /// <summary>
    /// Submodules added at this commit
    /// </summary>
    public IList<GitLabSubModuleDescriptor> SubModules { get; } = [];

    public IList<string> Tags { get; } = [];

    /// <summary>
    /// Source branch if a checkout or for a merge commit (required for merge commit)
    /// </summary>
    public string SourceBranch { get; set; }

    /// <summary>
    /// Target branch for a merge commit (required for merge commit)
    /// </summary>
    public string TargetBranch { get; set; }

    /// <summary>
    /// Branch to checkout before make more operation
    /// </summary>
    public string FromBranch { get; set; }

    /// <summary>
    /// Indicates if source branch must be deleted after merge
    /// </summary>
    public bool DeleteSourceBranch { get; set; }
}

public class GitLabCommitsCollection : GitLabCollection<GitLabCommit, GitLabProject>
{
    internal GitLabCommitsCollection(GitLabProject parent)
        : base(parent)
    {
    }
}
