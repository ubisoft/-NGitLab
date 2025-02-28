using System;
using System.Collections.Generic;
using System.Linq;
using NGitLab.Models;

namespace NGitLab.Mock.Clients;

internal sealed class CommitStatusClient : ClientBase, ICommitStatusClient
{
    private readonly long _projectId;

    public CommitStatusClient(ClientContext context, ProjectId projectId)
        : base(context)
    {
        _projectId = Server.AllProjects.FindProject(projectId.ValueAsString()).Id;
    }

    Models.CommitStatus ICommitStatusClient.AddOrUpdate(CommitStatusCreate status)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.Contribute);
            var commitStatus = project.CommitStatuses.FirstOrDefault(cs => Equals(cs, status));
            if (commitStatus == null)
            {
                commitStatus = new CommitStatus();
                project.CommitStatuses.Add(commitStatus);
            }

            commitStatus.Name = status.Name;
            commitStatus.Description = status.Description;
            commitStatus.Coverage = status.Coverage;
            commitStatus.Ref = status.Ref;
            commitStatus.Sha = status.CommitSha;
            commitStatus.TargetUrl = status.TargetUrl;

            return commitStatus.ToClientCommitStatus();
        }

        static bool Equals(CommitStatus a, CommitStatusCreate b)
        {
            return string.Equals(a.Name, b.Name, StringComparison.Ordinal) &&
                   string.Equals(a.Ref, b.Ref, StringComparison.Ordinal) &&
                   string.Equals(a.Sha, b.CommitSha, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.TargetUrl, b.TargetUrl, StringComparison.Ordinal);
        }
    }

    IEnumerable<Models.CommitStatus> ICommitStatusClient.AllBySha(string commitSha)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            return project.CommitStatuses
                .Where(p => string.Equals(p.Sha, commitSha, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.ToClientCommitStatus())
                .ToList();
        }
    }

    GitLabCollectionResponse<Models.CommitStatus> ICommitStatusClient.GetAsync(string commitSha, CommitStatusQuery query)
    {
        throw new NotImplementedException();
    }
}
