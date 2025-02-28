using System.Collections.Generic;
using NGitLab.Models;

namespace NGitLab.Impl;

public class CommitStatusClient : ICommitStatusClient
{
    private readonly API _api;
    private readonly string _projectPath;
    private readonly string _statusCreatePath;

    public CommitStatusClient(API api, ProjectId projectId)
    {
        _api = api;

        _projectPath = $"{Project.Url}/{projectId.ValueAsUriParameter()}";
        _statusCreatePath = $"{_projectPath}/statuses";
    }

    public IEnumerable<CommitStatus> AllBySha(string commitSha) => _api.Get().GetAll<CommitStatus>(GetCommitStatusesPath(commitSha));

    public GitLabCollectionResponse<CommitStatus> GetAsync(string commitSha, CommitStatusQuery query = null)
    {
        var url = ConstructGetUrl(GetCommitStatusesPath(commitSha), query);
        return _api.Get().GetAllAsync<CommitStatus>(url);
    }

    public CommitStatus AddOrUpdate(CommitStatusCreate status) => _api.Post().With(status).To<CommitStatus>($"{_statusCreatePath}/{status.CommitSha}");

    private string GetCommitStatusesPath(string commitSha) => $"{_projectPath}/repository/commits/{commitSha.ToLowerInvariant()}/statuses";

    private static string ConstructGetUrl(string url, CommitStatusQuery query)
    {
        if (query is null)
            return url;

        url = Utils.AddParameter(url, "ref", query.Ref);
        url = Utils.AddParameter(url, "stage", query.Stage);
        url = Utils.AddParameter(url, "name", query.Name);
        url = Utils.AddParameter(url, "pipeline_id", query.PipelineId);
        url = Utils.AddParameter(url, "order_by", query.OrderBy);
        url = Utils.AddParameter(url, "sort", query.Sort);
        url = Utils.AddParameter(url, "all", query.All);
        url = Utils.AddParameter(url, "per_page", query.PerPage);

        return url;
    }
}
