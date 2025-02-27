namespace NGitLab.Mock;

public sealed class CommitStatus : GitLabObject
{
    public new Project Parent => (Project)base.Parent;

    public string Sha { get; set; }

    public string Status { get; set; }

    public string Ref { get; set; }

    public string Name { get; set; }

    public string TargetUrl { get; set; }

    public string Description { get; set; }

    public int? Coverage { get; set; }

    public Models.CommitStatus ToClientCommitStatus()
    {
        return new Models.CommitStatus
        {
            CommitSha = Sha,
            Name = Name,
            Ref = Ref,
            Status = Status,
        };
    }
}
