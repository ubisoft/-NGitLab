using System;

namespace NGitLab;

public class GetCommitsRequest
{
    internal const uint DefaultPerPage = 100;

    public string RefName { get; set; }

    public string Path { get; set; }

    public bool? FirstParent { get; set; }

    public int MaxResults { get; set; }

    public uint PerPage { get; set; } = DefaultPerPage;

    public uint? Page { get; set; }

    public DateTime? Since { get; set; }

    public DateTime? Until { get; set; }
}
