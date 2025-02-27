using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using NGitLab.Extensions;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests;

public class ProjectsTests
{
    [Test]
    [NGitLabRetry]
    public async Task GetProjectByIdAsync()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var projectResult = await projectClient.GetByIdAsync(project.Id, new SingleProjectQuery(), CancellationToken.None);
        Assert.That(projectResult.Id, Is.EqualTo(project.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetByNamespacedPathAsync()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var projectResult = await projectClient.GetByNamespacedPathAsync(project.PathWithNamespace, new SingleProjectQuery(), CancellationToken.None);
        Assert.That(projectResult.Id, Is.EqualTo(project.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectAsync_WorksWithId_ReturnsProject()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        // Act
        var projectResult = await projectClient.GetAsync(project.Id, new() { Statistics = true });

        // Assert
        Assert.That(projectResult.Id, Is.EqualTo(project.Id));
        Assert.That(projectResult.Statistics, Is.Not.Null);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectAsync_WithPathAndWithoutQuery_ReturnsProject()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        // Act
        var projectResult = await projectClient.GetAsync(project.PathWithNamespace, query: null);

        // Assert
        Assert.That(projectResult.Id, Is.EqualTo(project.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectAsync_WhenProjectDoesNotExist_ShouldThrowNotFound()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;

        // Act
        // Assert
        var ex = Assert.ThrowsAsync<GitLabException>(() => projectClient.GetAsync("baz1234"));

        // Assert
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectAsync_WhenProjectInaccessible_ShouldThrowNotFound()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var adminProjectClient = context.AdminClient.Projects;
        var userProjectClient = context.Client.Projects;

        var adminProject = adminProjectClient.Create(new ProjectCreate
        {
            Name = "Project_Test_" + context.GetRandomNumber().ToStringInvariant(),
            VisibilityLevel = VisibilityLevel.Private,
        });

        // Act/Assert
        var ex = Assert.ThrowsAsync<GitLabException>(() => userProjectClient.GetAsync(adminProject.Id));

        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectsAsync()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var projects = new List<Project>();
        await foreach (var item in projectClient.GetAsync(new ProjectQuery()))
        {
            projects.Add(item);
        }

        Assert.That(projects, Is.Not.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetOwnedProjects()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var projects = projectClient.Owned.Take(30).ToArray();
        Assert.That(projects, Is.Not.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetVisibleProjects()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var projects = projectClient.Visible.Take(30).ToArray();

        Assert.That(projects, Is.Not.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetAccessibleProjects()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var projects = projectClient.Accessible.Take(30).ToArray();

        Assert.That(projects, Is.Not.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectsByQuery()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var query = new ProjectQuery
        {
            Simple = true,
            Search = project.Name,
        };

        var projects = projectClient.Get(query).Take(10).ToArray();
        Assert.That(projects, Has.Length.EqualTo(1));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectsStatistics()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var projects = projectClient.Get(new ProjectQuery { Statistics = true }).Take(10).ToList();
        if (projects.Count == 0)
        {
            Assert.Fail("No projects found.");
        }

        projects.ForEach(p => Assert.That(p.Statistics, Is.Not.Null));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectsProperties()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var projects = projectClient.Get(new ProjectQuery()).ToList();

        if (projects.Count == 0)
        {
            Assert.Fail("No projects found.");
        }

        projects.ForEach(p => Assert.That(p.Links, Is.Not.Null));
        projects.ForEach(p => Assert.That(p.MergeMethod, Is.Not.Null));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectsByQuery_VisibilityInternal()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(p => p.VisibilityLevel = VisibilityLevel.Internal);
        var projectClient = context.Client.Projects;

        var query = new ProjectQuery
        {
            Simple = true,
            Visibility = VisibilityLevel.Internal,
        };

        var projects = projectClient.Get(query).ToList();

        Assert.That(projects, Is.Not.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectByIdByQuery_Statistics()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(p => p.VisibilityLevel = VisibilityLevel.Internal);
        var projectClient = context.Client.Projects;

        var query = new SingleProjectQuery
        {
            Statistics = true,
        };

        project = projectClient.GetById(project.Id, query);

        Assert.That(project, Is.Not.Null);
        Assert.That(project.Statistics, Is.Not.Null);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectLanguages()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(p => p.VisibilityLevel = VisibilityLevel.Internal);
        var projectClient = context.Client.Projects;

        var file = new FileUpsert
        {
            Branch = project.DefaultBranch,
            CommitMessage = "add javascript file",
            Content = "var test = 0;",
            Path = "test.js",
        };

        context.Client.GetRepository(project.Id).Files.Create(file);
        var languages = projectClient.GetLanguages(project.Id.ToStringInvariant());
        Assert.That(languages, Has.Count.EqualTo(1));
        Assert.That(languages.First().Key, Is.EqualTo("javascript").IgnoreCase);
        Assert.That(languages.First().Value, Is.EqualTo(100));
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectsCanSpecifyTheProjectPerPageCount()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(p => p.VisibilityLevel = VisibilityLevel.Internal);
        var projectClient = context.Client.Projects;

        var query = new ProjectQuery
        {
            Simple = true,
            Visibility = VisibilityLevel.Internal,
            PerPage = 5,
        };

        var projects = projectClient.Get(query).Take(10).ToList();

        Assert.That(projects, Is.Not.Empty);
        Assert.That(context.LastRequest.RequestUri.ToString(), Contains.Substring("per_page=5"));
    }

    [TestCase(false)]
    [TestCase(true)]
    [NGitLabRetry]
    public async Task CreateUpdateDelete(bool initiallySetTagsInsteadOfTopics)
    {
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;

        var project = new ProjectCreate
        {
            Description = "desc",
            IssuesEnabled = true,
            MergeRequestsEnabled = true,
            Name = "CreateDelete_Test_" + context.GetRandomNumber().ToStringInvariant(),
            NamespaceId = null,
            SnippetsEnabled = true,
            VisibilityLevel = VisibilityLevel.Internal,
            WikiEnabled = true,
        };

        var expectedTopics = new List<string> { "Tag-1", "Tag-2" };
        if (initiallySetTagsInsteadOfTopics)
            project.Tags = expectedTopics;
        else
            project.Topics = expectedTopics;

        var createdProject = projectClient.Create(project);

        Assert.That(createdProject.Description, Is.EqualTo(project.Description));
        Assert.That(createdProject.IssuesEnabled, Is.EqualTo(project.IssuesEnabled));
        Assert.That(createdProject.MergeRequestsEnabled, Is.EqualTo(project.MergeRequestsEnabled));
        Assert.That(createdProject.Name, Is.EqualTo(project.Name));
        Assert.That(createdProject.VisibilityLevel, Is.EqualTo(project.VisibilityLevel));
        Assert.That(createdProject.Topics, Is.EquivalentTo(expectedTopics));
        Assert.That(createdProject.TagList, Is.EquivalentTo(expectedTopics));
        Assert.That(createdProject.RepositoryAccessLevel, Is.EqualTo(RepositoryAccessLevel.Enabled));

        // Update
        expectedTopics = ["Tag-3"];
        var updateOptions = new ProjectUpdate { Visibility = VisibilityLevel.Private, Topics = expectedTopics };
        var updatedProject = projectClient.Update(createdProject.Id.ToStringInvariant(), updateOptions);
        Assert.That(updatedProject.VisibilityLevel, Is.EqualTo(VisibilityLevel.Private));
        Assert.That(updatedProject.Topics, Is.EquivalentTo(expectedTopics));
        Assert.That(updatedProject.TagList, Is.EquivalentTo(expectedTopics));

        updateOptions.Visibility = VisibilityLevel.Public;
        updateOptions.Topics = null;    // If Topics are null, the project's existing topics will remain
        updatedProject = projectClient.Update(createdProject.Id.ToStringInvariant(), updateOptions);
        Assert.That(updatedProject.VisibilityLevel, Is.EqualTo(VisibilityLevel.Public));
        Assert.That(updatedProject.Topics, Is.EquivalentTo(expectedTopics));
        Assert.That(updatedProject.TagList, Is.EquivalentTo(expectedTopics));

        var updatedProject2 = projectClient.Update(createdProject.PathWithNamespace, new ProjectUpdate { Visibility = VisibilityLevel.Internal });
        Assert.That(updatedProject2.VisibilityLevel, Is.EqualTo(VisibilityLevel.Internal));

        projectClient.Delete(createdProject.Id);
    }

    [Test]
    [NGitLabRetry]
    public async Task CreateAsync_CreatesNewProject()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;
        var user = context.Client.Users.Current;
        var projectName = "CreateAsync_" + context.GetRandomNumber().ToStringInvariant();

        var expected = new ProjectCreate()
        {
            Name = projectName,
            Path = projectName.ToLowerInvariant(),
            Description = "desc",
            DefaultBranch = "foo",
            InitializeWithReadme = true,
            IssuesEnabled = true,
            MergeRequestsEnabled = true,
            VisibilityLevel = VisibilityLevel.Private,
            Topics = ["t1", "t2"],
            BuildTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds,
        };

        // Act
        var actual = await projectClient.CreateAsync(expected);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(actual.Name, Is.EqualTo(expected.Name));
            Assert.That(actual.Path, Is.EqualTo(expected.Path));
            Assert.That(actual.NameWithNamespace, Is.EqualTo($"{user.Name} / {expected.Name}"));
            Assert.That(actual.PathWithNamespace, Is.EqualTo($"{user.Username}/{expected.Path}"));
            Assert.That(actual.Description, Is.EqualTo(expected.Description));
            Assert.That(actual.DefaultBranch, Is.EqualTo(expected.DefaultBranch));
            Assert.That(actual.IssuesEnabled, Is.EqualTo(expected.IssuesEnabled));
            Assert.That(actual.MergeRequestsEnabled, Is.EqualTo(expected.MergeRequestsEnabled));
            Assert.That(actual.VisibilityLevel, Is.EqualTo(expected.VisibilityLevel));
            Assert.That(actual.Topics, Is.EquivalentTo(expected.Topics));
            Assert.That(actual.RepositoryAccessLevel, Is.EqualTo(RepositoryAccessLevel.Enabled));
            Assert.That(actual.BuildTimeout, Is.EqualTo(expected.BuildTimeout));
        });
    }

    [Test]
    public async Task CreateAsync_WhenInitializeWithReadmeIsFalse_ItIgnoresDefaultBranch()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;

        var expected = new ProjectCreate()
        {
            Name = "CreateAsync_" + context.GetRandomNumber().ToStringInvariant(),
            DefaultBranch = "foo",
            InitializeWithReadme = false,
        };

        // Act
        var actual = await projectClient.CreateAsync(expected);

        // Assert
        Assert.That(actual.Name, Is.EqualTo(expected.Name));
        Assert.That(actual.DefaultBranch, Is.Not.EqualTo(expected.DefaultBranch));
    }

    [Test]
    [NGitLabRetry]
    public async Task CreateAsync_WhenProjectAlreadyExists_ItThrows()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var existingProject = context.CreateProject();
        var projectClient = context.Client.Projects;

        // Act
        var ex = Assert.ThrowsAsync<GitLabException>(() =>
            projectClient.CreateAsync(new()
            {
                Path = existingProject.Path,
            }));

        // Assert
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorMessage, Contains.Substring("[\"has already been taken\"]"));
    }

    [Test]
    [NGitLabRetry]
    public async Task SearchAsync_WhenSearchForExistingProject_ItFindsIt()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        // Act
        var actualProjects = projectClient.GetAsync(new()
        {
            Search = project.Name,
            Scope = ProjectQueryScope.All,
        });

        // Assert
        Assert.That(actualProjects.Count(), Is.EqualTo(1));
        Assert.That(actualProjects.Single().Id, Is.EqualTo(project.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task SearchAsync_WhenNotFound_ReturnsEmptySet()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;

        // Act
        var actualProjects = projectClient.GetAsync(new()
        {
            Search = Guid.NewGuid().ToString(),
            Scope = ProjectQueryScope.All,
        });

        // Assert
        Assert.That(actualProjects, Is.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task UpdateAsync_WhenUpdateVisibilityAndTopics_ItWorks()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(p =>
        {
            p.VisibilityLevel = VisibilityLevel.Public;
            p.Topics = ["Tag-1", "Tag-2"];
        });

        var projectClient = context.Client.Projects;

        // Act
        var expectedTopics = new List<string>() { "Tag-3" };
        var updatedProject = await projectClient.UpdateAsync(project.PathWithNamespace, new()
        {
            Visibility = VisibilityLevel.Private,
            Topics = expectedTopics,
        });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(updatedProject.VisibilityLevel, Is.EqualTo(VisibilityLevel.Private));
            Assert.That(updatedProject.Topics, Is.EquivalentTo(expectedTopics));
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task UpdateAsync_WhenProjectNotFound_ItThrows()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;

        // Act
        var ex = Assert.ThrowsAsync<GitLabException>(() =>
            projectClient.UpdateAsync(int.MaxValue, new()
            {
                Visibility = VisibilityLevel.Private,
            }));

        // Assert
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    [NGitLabRetry]
    public async Task DeleteAsync_WhenProjectExists_ItIsDeleted()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var group = context.CreateGroup();
        var project = context.CreateProject(group.Id);
        var projectClient = context.Client.Projects;

        // Act
        await projectClient.DeleteAsync(project.Id);

        // Assert
        Assert.ThrowsAsync<GitLabException>(() => projectClient.GetAsync(project.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task DeleteAsync_WhenProjectNotFound_ItThrows()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var group = context.CreateGroup();
        var project = context.CreateProject(group.Id);
        var projectClient = context.Client.Projects;

        // Act
        var ex = Assert.ThrowsAsync<GitLabException>(() => projectClient.DeleteAsync(int.MaxValue));

        // Assert
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // No owner level (50) for project! See https://docs.gitlab.com/ee/api/members.html
    [TestCase(AccessLevel.Guest)]
    [TestCase(AccessLevel.Reporter)]
    [TestCase(AccessLevel.Developer)]
    [TestCase(AccessLevel.Maintainer)]
    [NGitLabRetry]
    public async Task Test_get_by_project_query_projectQuery_MinAccessLevel_returns_projects(AccessLevel accessLevel)
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        // Arrange
        var query = new ProjectQuery
        {
            MinAccessLevel = accessLevel,
        };

        // Act
        var result = projectClient.Get(query).Take(10).ToArray();

        // Assert
        Assert.That(result.Length != 0, Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task ForkProject()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;

        var createdProject = context.CreateProject(p =>
        {
            p.Description = "desc";
            p.IssuesEnabled = true;
            p.MergeRequestsEnabled = true;
            p.Name = "ForkProject_Test_" + context.GetRandomNumber().ToStringInvariant();
            p.NamespaceId = null;
            p.SnippetsEnabled = true;
            p.VisibilityLevel = VisibilityLevel.Internal;
            p.WikiEnabled = true;
            p.Topics = ["Tag-1", "Tag-2"];
        });

        context.Client.GetRepository(createdProject.Id).Files.Create(new FileUpsert
        {
            Branch = createdProject.DefaultBranch,
            CommitMessage = "add readme",
            Path = "README.md",
            RawContent = "this project should only live during the unit tests, you can delete if you find some",
        });

        var forkedProject = projectClient.Fork(createdProject.Id.ToStringInvariant(), new ForkProject
        {
            Path = createdProject.Path + "-fork",
            Name = createdProject.Name + "Fork",
        });

        // Wait for the fork to be ready
        await GitLabTestContext.RetryUntilAsync(() => projectClient[forkedProject.Id], p => string.Equals(p.ImportStatus, "finished", StringComparison.Ordinal), TimeSpan.FromMinutes(2));

        var forks = projectClient.GetForks(createdProject.Id.ToStringInvariant(), new ForkedProjectQuery());
        Assert.That(forks.Single().Id, Is.EqualTo(forkedProject.Id));

        // Create a merge request with AllowCollaboration (only testable on a fork, also the source branch must not be protected)
        context.Client.GetRepository(forkedProject.Id).Branches.Create(new BranchCreate { Name = "branch-test", Ref = createdProject.DefaultBranch });
        var mr = context.Client.GetMergeRequest(forkedProject.Id).Create(new MergeRequestCreate
        {
            AllowCollaboration = true,
            Description = "desc",
            SourceBranch = "branch-test",
            TargetBranch = createdProject.DefaultBranch,
            TargetProjectId = createdProject.Id,
            Title = "title",
        });

        Assert.That(mr.AllowCollaboration, Is.True);

        projectClient.Delete(forkedProject.Id);
        projectClient.Delete(createdProject.Id);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectsByLastActivity()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();
        var projectClient = context.Client.Projects;

        var date = DateTimeOffset.UtcNow.AddMonths(-1);
        var query = new ProjectQuery
        {
            LastActivityAfter = date,
            OrderBy = "last_activity_at",
            Ascending = true,
        };

        var projects = projectClient.Get(query).Take(10).ToList();
        Assert.That(projects, Is.Not.Empty);
        Assert.That(projects.Select(p => p.LastActivityAt), Is.All.GreaterThan(date.UtcDateTime));
    }

    [Test]
    [NGitLabRetry]
    public async Task IsEmpty()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;

        var createdProject = context.CreateProject(prj =>
        {
            prj.Name = "Project_Test_" + context.GetRandomNumber().ToStringInvariant();
            prj.VisibilityLevel = VisibilityLevel.Internal;
        });
        Assert.That(createdProject.EmptyRepo, Is.True);

        context.Client.GetRepository(createdProject.Id).Files.Create(new FileUpsert
        {
            Branch = createdProject.DefaultBranch,
            CommitMessage = "add readme",
            Path = "README.md",
            RawContent = "this project should only live during the unit tests, you can delete if you find some",
        });

        createdProject = projectClient[createdProject.Id];
        Assert.That(createdProject.EmptyRepo, Is.False);
    }

    [Test]
    [NGitLabRetry]
    public async Task GetProjectByTopics()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();

        var topicRequired1 = CreateTopic();
        var topicRequired2 = CreateTopic();
        var topicOptional1 = CreateTopic();
        var topicOptional2 = CreateTopic();

        context.CreateProject();
        context.CreateProject(p => p.Topics = [topicRequired1, topicOptional1]);
        context.CreateProject(p => p.Topics = [topicRequired1, topicRequired2]);
        context.CreateProject(p => p.Topics = [topicRequired1, topicOptional2]);
        context.CreateProject(p => p.Topics = [topicRequired1, topicOptional1, topicRequired2]);
        context.CreateProject(p => p.Topics = [topicOptional1, topicOptional2, topicRequired2]);

        var projectClient = context.Client.Projects;

        var query = new ProjectQuery();
        query.Topics.Add(topicRequired1);
        query.Topics.Add(topicRequired2);

        // Act
        var projects = projectClient.Get(query); // Get projects that have both required topics

        // Assert
        Assert.That(projects.Count(), Is.EqualTo(2));

        static string CreateTopic() => Guid.NewGuid().ToString("N");
    }

    [TestCase(null)]
    [TestCase(SquashOption.Always)]
    [TestCase(SquashOption.Never)]
    [TestCase(SquashOption.DefaultOff)]
    [TestCase(SquashOption.DefaultOn)]
    [NGitLabRetry]
    public async Task CreateProjectWithSquashOption(SquashOption? inputSquashOption)
    {
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;

        var project = new ProjectCreate
        {
            Description = "desc",
            Name = "CreateProjectWithSquashOption_Test_" + context.GetRandomNumber().ToStringInvariant(),
            VisibilityLevel = VisibilityLevel.Internal,
            SquashOption = inputSquashOption,
        };

        var createdProject = projectClient.Create(project);

        var expectedSquashOption = inputSquashOption ?? SquashOption.DefaultOff;
        Assert.That(createdProject.SquashOption, Is.EqualTo(expectedSquashOption));

        projectClient.Delete(createdProject.Id);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_project_groups_query_returns_searched_group()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;
        var group = context.CreateGroup();
        var project = context.CreateProject(group.Id);
        context.CreateGroup();

        var groups = projectClient.GetGroupsAsync(project.Id, new ProjectGroupsQuery
        {
            Search = group.Name,
        }).ToArray();

        Assert.That(groups.Select(g => g.Id), Is.EquivalentTo(new[] { group.Id }));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_project_groups_query_returns_ancestor_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var projectClient = context.Client.Projects;
        var group = context.CreateGroup();
        var subgroup = context.CreateSubgroup(group.Id);
        var project = context.CreateProject(subgroup.Id);

        var groups = projectClient.GetGroupsAsync(project.Id, new ProjectGroupsQuery()).ToArray();

        Assert.That(groups.Select(g => g.Id), Is.EquivalentTo(new[] { group.Id, subgroup.Id }));
    }
}
