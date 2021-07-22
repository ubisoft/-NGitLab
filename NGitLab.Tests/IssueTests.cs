﻿using System.Linq;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests
{
    public class IssueTests
    {
        [Test]
        [NGitLabRetry]
        public async Task Test_get_issue_with_IssueQuery()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var project = context.CreateProject();
            var issuesClient = context.Client.Issues;
            var issue1 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title1" });
            var issue2 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title2" });

            var issues = issuesClient.Get(new IssueQuery
            {
                State = IssueState.opened,
            }).Where(i => i.ProjectId == project.Id).ToList();

            Assert.AreEqual(2, issues.Count);
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_unassigned_issues_with_IssueQuery()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var project = context.CreateProject();
            var issuesClient = context.Client.Issues;
            var issue1 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title1" });
            var issue2 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title2", AssigneeId = context.Client.Users.Current.Id });

            var issues = issuesClient.Get(new IssueQuery
            {
                AssigneeId = QueryAssigneeId.None,
                State = IssueState.opened,
            }).Where(i => i.ProjectId == project.Id).ToList();

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(issue1.Id, issues[0].Id);
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_assigned_issues_with_IssueQuery()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var project = context.CreateProject();
            var issuesClient = context.Client.Issues;
            var issue1 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title1" });
            var issue2 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title2", AssigneeId = context.Client.Users.Current.Id });

            var issues = issuesClient.Get(new IssueQuery
            {
                AssigneeId = context.Client.Users.Current.Id,
                State = IssueState.opened,
            }).Where(i => i.ProjectId == project.Id).ToList();

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(issue2.Id, issues[0].Id);
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_assigned_issues_with_IssueQuery_and_project_id()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var project = context.CreateProject();
            var issuesClient = context.Client.Issues;
            var issue1 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title1" });
            var issue2 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title2", AssigneeId = context.Client.Users.Current.Id });

            var issues = issuesClient.Get(project.Id, new IssueQuery
            {
                AssigneeId = context.Client.Users.Current.Id,
                State = IssueState.opened,
            }).ToList();

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(issue2.Id, issues[0].Id);
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_issues_with_invalid_project_id_will_throw()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var issuesClient = context.Client.Issues;

            Assert.Throws(Is.InstanceOf<GitLabException>(), () => issuesClient.ForProject(int.MaxValue).ToList());
            Assert.Throws(Is.InstanceOf<GitLabException>(), () => issuesClient.Get(int.MaxValue, new IssueQuery()).ToList());
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_all_project_issues()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var project = context.CreateProject();
            var issuesClient = context.Client.Issues;
            var issue1 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title1" });
            var issue2 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title2", AssigneeId = context.Client.Users.Current.Id });

            var issues = issuesClient.ForProject(project.Id).ToList();
            Assert.AreEqual(2, issues.Count);

            issues = issuesClient.Get(project.Id, new IssueQuery()).ToList();
            Assert.AreEqual(2, issues.Count);
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_all_resource_label_events()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var project = context.CreateProject();
            var issuesClient = context.Client.Issues;
            var issue1 = issuesClient.Create(new IssueCreate { Id = project.Id, Title = "title1" });

            var issues = issuesClient.ForProject(project.Id).ToList();
            Assert.AreEqual(1, issues.Count);

            var testLabel = "test";
            var updatedIssue = issues[0];
            issuesClient.Edit(new IssueEdit
            {
                Id = project.Id,
                IssueId = updatedIssue.IssueId,
                Labels = testLabel,
            });

            issuesClient.Edit(new IssueEdit
            {
                Id = project.Id,
                IssueId = updatedIssue.IssueId,
                Labels = string.Empty,
            });

            var resourceLabelEvents = issuesClient.ResourceLabelEvents(project.Id, updatedIssue.IssueId).ToList();
            Assert.AreEqual(2, resourceLabelEvents.Count);

            var addLabelEvent = resourceLabelEvents.First(e => e.Action == ResourceLabelEventAction.Add);
            Assert.AreEqual(testLabel, addLabelEvent.Label.Name);
            Assert.AreEqual(ResourceLabelEventAction.Add, addLabelEvent.Action);

            var removeLabelEvent = resourceLabelEvents.First(e => e.Action == ResourceLabelEventAction.Remove);
            Assert.AreEqual(testLabel, removeLabelEvent.Label.Name);
            Assert.AreEqual(ResourceLabelEventAction.Remove, removeLabelEvent.Action);
        }
    }
}
