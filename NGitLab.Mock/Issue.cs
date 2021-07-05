﻿using System;
using System.Globalization;
using System.Linq;

namespace NGitLab.Mock
{
    public sealed class Issue : GitLabObject
    {
        public Project Project => (Project)Parent;

        public int Id { get; set; }

        public int Iid { get; set; }

        public int ProjectId => Project.Id;

        public string Title { get; set; }

        public string Description { get; set; }

        public string[] Labels { get; set; }

        public Milestone Milestone { get; set; }

        public UserRef Assignee
        {
            get => Assignees.FirstOrDefault();
            set => Assignees = new[] { value };
        }

        public UserRef[] Assignees { get; set; }

        public UserRef Author { get; set; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset UpdatedAt { get; set; }

        public DateTimeOffset? ClosedAt { get; set; }

        public string WebUrl => Server.MakeUrl($"{Project.PathWithNamespace}/issues/{Id.ToString(CultureInfo.InvariantCulture)}");

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
                Assignees = Assignees?.Select(a => a.ToClientAssignee()).ToArray(),
                Author = Author.ToClientAuthor(),
                State = State.ToString(),
                CreatedAt = CreatedAt.UtcDateTime,
                UpdatedAt = UpdatedAt.UtcDateTime,
                WebUrl = WebUrl,
            };
        }
    }
}
