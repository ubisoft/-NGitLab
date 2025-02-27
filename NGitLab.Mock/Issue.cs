using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NGitLab.Mock;

public sealed class Issue : GitLabObject
{
    public Project Project => (Project)Parent;

    public long Id { get; set; }

    public long Iid { get; set; }

    public long ProjectId => Project.Id;

    public string Title { get; set; }

    public string Description { get; set; }

    public string[] Labels { get; set; }

    public Milestone Milestone { get; set; }

    public UserRef Assignee
    {
        get => Assignees?.FirstOrDefault();
        set => Assignees = (value != null) ? new[] { value } : null;
    }

    public UserRef[] Assignees { get; set; }

    public UserRef Author { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public bool Confidential { get; set; }

    public bool DiscussionLocked { get; set; }

    public IList<ProjectIssueNote> Notes { get; set; } = [];

    public string WebUrl => Server.MakeUrl($"{Project.PathWithNamespace}/-/issues/{Iid.ToString(CultureInfo.InvariantCulture)}");

    public IssueState State
    {
        get
        {
            if (ClosedAt.HasValue)
                return IssueState.closed;

            return IssueState.opened;
        }

        set
        {
            if (value == IssueState.closed)
            {
                ClosedAt = DateTimeOffset.UtcNow;
            }
            else if (value == IssueState.opened)
            {
                ClosedAt = null;
            }
        }
    }

    public Issue()
    {
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Models.Issue ToClientIssue()
    {
        return new Models.Issue
        {
            Id = Id,
            IssueId = Iid,
            ProjectId = ProjectId,
            Title = Title,
            Description = Description,
            Labels = Labels,
            Milestone = Milestone?.ToClientMilestone(),
            Assignee = Assignee?.ToClientAssignee(),
            Assignees = Assignees?.Select(a => a.ToClientAssignee())?.ToArray(),
            Author = Author.ToClientAuthor(),
            State = State.ToString(),
            CreatedAt = CreatedAt.UtcDateTime,
            UpdatedAt = UpdatedAt.UtcDateTime,
            WebUrl = WebUrl,
            Confidential = Confidential,
            DiscussionLocked = DiscussionLocked,
        };
    }

    public bool CanUserViewIssue(User user)
    {
        return !Confidential || Author.Id == user.Id || Project.CanUserViewConfidentialIssues(user);
    }
}
