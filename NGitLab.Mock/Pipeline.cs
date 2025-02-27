using System;
using System.Collections.Generic;
using System.Globalization;
using NGitLab.Models;

namespace NGitLab.Mock;

public sealed class Pipeline : GitLabObject
{
    public Pipeline(string @ref)
    {
        Ref = @ref;
    }

    public Project Project => (Project)Parent;

    internal bool IsDownStreamPipeline { get; set; }

    public long Id { get; set; }

    public long ProjectId => Project?.Id ?? 0;

    public JobStatus Status { get; set; }

    public string Ref { get; set; }

    public bool Tag { get; set; }

    public Sha1 Sha { get; set; }

    public string Source { get; set; }

    public Sha1 BeforeSha { get; set; }

    public string YamlError { get; set; }

    public User User { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public DateTimeOffset CommittedAt { get; set; }

    public TimeSpan? Duration { get; set; }

    public double Coverage { get; set; }

    public IEnumerable<PipelineVariable> Variables { get; set; }

    public string CiToken { get; set; }

    public TestReport TestReports { get; set; }

    public TestReportSummary TestReportsSummary { get; set; }

    public string Name { get; set; }

    [Obsolete("Use other overloads")]
    public Job AddNewJob(Project project)
    {
        return AddJob(project, new Job());
    }

    public Job AddNewJob(JobStatus status)
    {
        var job = AddNewJob(name: "temp", status);
        job.Name = "Job" + job.Id.ToString(CultureInfo.InvariantCulture);
        return job;
    }

    public Job AddNewJob(string name, JobStatus status, User user = null)
    {
        var job = AddJob(new Job
        {
            Name = name,
            Pipeline = this,
            Status = status,
            User = user ?? User,
            CreatedAt = DateTime.UtcNow,
        });

        if (status is JobStatus.Running or JobStatus.Success or JobStatus.Failed or JobStatus.Canceled)
        {
            job.StartedAt = DateTime.UtcNow;
        }

        if (status is JobStatus.Success or JobStatus.Failed or JobStatus.Canceled)
        {
            job.FinishedAt = DateTime.UtcNow;
        }

        return job;
    }

    [Obsolete("Use other overloads")]
    public Job AddJob(Project project, Job job)
    {
        return project.Jobs.Add(job, this);
    }

    public Job AddJob(Job job)
    {
        return Project.Jobs.Add(job, this);
    }

    internal JobBasic.JobPipeline ToJobPipeline()
    {
        return new JobBasic.JobPipeline
        {
            Id = Id,
            ProjectId = Project.Id,
            Ref = Ref,
            Sha = Sha,
            Status = Status,
        };
    }

    internal PipelineBasic ToPipelineBasicClient()
    {
        return new PipelineBasic
        {
            Id = Id,
            Status = Status,
            Ref = Ref,
            Sha = Sha,
            Source = Source,
            CreatedAt = CreatedAt.UtcDateTime,
            UpdatedAt = UpdatedAt.UtcDateTime,
            ProjectId = Project.Id,
        };
    }

    internal Models.Pipeline ToPipelineClient()
    {
        return new Models.Pipeline
        {
            Id = Id,
            Status = Status,
            Ref = Ref,
            Tag = Tag,
            Sha = Sha,
            Source = Source,
            BeforeSha = BeforeSha,
            YamlError = YamlError,
            User = User.ToClientUser(),
            CreatedAt = CreatedAt.UtcDateTime,
            UpdatedAt = UpdatedAt.UtcDateTime,
            StartedAt = StartedAt.HasValue ? StartedAt.Value.UtcDateTime : default,
            FinishedAt = FinishedAt.HasValue ? FinishedAt.Value.UtcDateTime : default,
            Duration = Duration.HasValue ? Duration.Value.Ticks : 0,
            Coverage = Coverage,
            ProjectId = ProjectId,
            WebUrl = Project?.WebUrl + "/-/pipelines/" + Id.ToString(CultureInfo.InvariantCulture),
            Name = Name,
        };
    }
}
