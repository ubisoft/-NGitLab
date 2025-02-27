using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NGitLab.Mock.Internals;
using NGitLab.Models;

namespace NGitLab.Mock.Clients;

internal sealed class JobClient : ClientBase, IJobClient
{
    private readonly long _projectId;

    public JobClient(ClientContext context, ProjectId projectId)
        : base(context)
    {
        _projectId = Server.AllProjects.FindProject(projectId.ValueAsString()).Id;
    }

    public Models.Job Get(long jobId)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            var job = project.Jobs.GetById(jobId);

            if (job == null)
                throw GitLabException.NotFound();

            return job.ToJobClient();
        }
    }

    [SuppressMessage("Design", "MA0042:Do not use blocking calls in an async method", Justification = "Would be an infinite recursion")]
    public async Task<Models.Job> GetAsync(long jobId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Get(jobId);
    }

    public byte[] GetJobArtifacts(long jobId)
    {
        throw new NotImplementedException();
    }

    public byte[] GetJobArtifact(long jobId, string path)
    {
        throw new NotImplementedException();
    }

    public byte[] GetJobArtifact(JobArtifactQuery query)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Models.Job> GetJobs(JobScopeMask scope)
    {
        return GetJobs(new JobQuery { Scope = scope });
    }

    public IEnumerable<Models.Job> GetJobs(JobQuery query)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);

            if (query.Scope == JobScopeMask.All)
                return project.Jobs.Select(j => j.ToJobClient());

            var scopes = new List<string>();
            foreach (Enum value in Enum.GetValues(query.Scope.GetType()))
            {
                if (query.Scope.HasFlag(value))
                {
                    scopes.Add(value.ToString());
                }
            }

            var jobs = project.Jobs.Where(j => scopes.Exists(scope => string.Equals(j.Status.ToString(), scope, StringComparison.OrdinalIgnoreCase)));
            return jobs.Select(j => j.ToJobClient()).ToList();
        }
    }

    public GitLabCollectionResponse<Models.Job> GetJobsAsync(JobQuery query)
    {
        return GitLabCollectionResponse.Create(GetJobs(query));
    }

    public string GetTrace(long jobId)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            var job = project.Jobs.GetById(jobId);

            if (job == null)
                throw GitLabException.NotFound();

            return job.Trace;
        }
    }

    [SuppressMessage("Design", "MA0042:Do not use blocking calls in an async method", Justification = "Would be an infinite recursion")]
    public async Task<string> GetTraceAsync(long jobId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return GetTrace(jobId);
    }

    public Models.Job RunAction(long jobId, JobAction action)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            var job = project.Jobs.GetById(jobId);

            switch (action)
            {
                case JobAction.Cancel:
                    job.Status = JobStatus.Canceled;
                    break;
                case JobAction.Erase:
                    job.Artifacts = null;
                    job.Trace = null;
                    break;
                case JobAction.Play:
                    job.Status = JobStatus.Running;
                    break;
                case JobAction.Retry:
                    var retryJob = job.Clone();
                    retryJob.Status = JobStatus.Running;
                    project.Jobs.Add(retryJob, project.Pipelines.GetById(job.Pipeline.Id));
                    return retryJob.ToJobClient();
            }

            return job.ToJobClient();
        }
    }

    [SuppressMessage("Design", "MA0042:Do not use blocking calls in an async method", Justification = "Would be an infinite recursion")]
    public async Task<Models.Job> RunActionAsync(long jobId, JobAction action, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return RunAction(jobId, action);
    }
}
