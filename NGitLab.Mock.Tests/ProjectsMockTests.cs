using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Mock.Config;
using NGitLab.Models;
using NUnit.Framework;

namespace NGitLab.Mock.Tests;

public class ProjectsMockTests
{
    [Test]
    public void WithProjectHelper_WhenPathNotSpecified_ItAutogeneratesPathFromName()
    {
        // Arrange
        var config = new GitLabConfig()
            .WithUser("Foo", isDefault: true);

        // Act
        using var server = config
            .WithProject("TEST", configure: p => p.@Namespace = "Foo")
            .BuildServer();

        // Assert
        var client = server.CreateClient();
        var project = client.Projects["Foo/Test"];

        Assert.That(project.Path, Is.EqualTo("test"));
    }

    [Test]
    public void Test_projects_created_can_be_found()
    {
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProject("Test", @namespace: "testgroup", addDefaultUserAsMaintainer: true)
            .BuildServer();

        var client = server.CreateClient();
        var project = client.Projects["testgroup/Test"];

        Assert.That(project, Is.Not.Null);
        Assert.That(project.Name, Is.EqualTo("Test"));
        Assert.That(project.Namespace.FullPath, Is.EqualTo("testgroup"));
    }

    [Test]
    public void GetProjectAsync_WhenProjectDoesNotExist_ShouldThrowNotFound()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("TestUser", isDefault: true)
            .BuildServer();
        var gitLabClient = server.CreateClient();
        var projectClient = gitLabClient.Projects;

        // Act/Assert
        var ex = Assert.ThrowsAsync<GitLabException>(() => projectClient.GetAsync("baz1234"));

        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public void GetProjectAsync_WhenProjectInaccessible_ShouldThrowNotFound()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("TestUser1", isDefault: true)
            .WithUser("TestUser2")
            .BuildServer();
        var testUser1ProjectClient = server.CreateClient("TestUser1").Projects;
        var testUser2ProjectClient = server.CreateClient("TestUser2").Projects;

        var testUser2Project = testUser2ProjectClient.Create(new ProjectCreate
        {
            Name = $"Project_Test_{Guid.NewGuid()}",
            VisibilityLevel = VisibilityLevel.Private,
        });

        // Act/Assert
        var ex = Assert.ThrowsAsync<GitLabException>(() => testUser1ProjectClient.GetAsync(testUser2Project.Id));

        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public void Test_project_can_be_cloned_by_default()
    {
        using var tempDir = TemporaryDirectory.Create();
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProject("Test", clonePath: tempDir.FullPath)
            .BuildServer();

        Assert.That(Directory.Exists(tempDir.GetFullPath(".git")), Is.True);
    }

    [Test]
    public void Test_project_with_submodules()
    {
        using var tempDir = TemporaryDirectory.Create();
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProject("ModuleA", configure: x => x.WithCommit(configure: c => c.WithFile("A.txt")))
            .WithProject("ModuleB", configure: x => x.WithCommit(configure: c => c.WithFile("B.txt")))
            .WithProject("Test", clonePath: tempDir.FullPath, configure: x =>
                x.WithCommit("Init", configure: c
                    => c.WithSubModule("ModuleA")
                        .WithSubModule("ModuleB")))
            .BuildServer();

        Assert.That(Directory.Exists(tempDir.GetFullPath(".git")), Is.True);
        Assert.That(System.IO.File.Exists(tempDir.GetFullPath("ModuleA/.git")), Is.True);
        Assert.That(System.IO.File.Exists(tempDir.GetFullPath("ModuleA/A.txt")), Is.True);
        Assert.That(System.IO.File.Exists(tempDir.GetFullPath("ModuleB/.git")), Is.True);
        Assert.That(System.IO.File.Exists(tempDir.GetFullPath("ModuleB/B.txt")), Is.True);
    }

    [Test]
    public void Test_project_with_nested_submodules()
    {
        using var tempDir = TemporaryDirectory.Create();
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProject("ModuleA", configure: x => x.WithCommit(configure: c => c.WithFile("A.txt")))
            .WithProject("ModuleB", configure: x => x.WithCommit(configure: c
                                                                     => c.WithFile("B.txt")
                                                                         .WithSubModule("ModuleA")))
            .WithProject("Test", clonePath: tempDir.FullPath, configure: x =>
                x.WithCommit(configure: c
                    => c.WithSubModule("ModuleB")))
            .BuildServer();

        Assert.That(Directory.Exists(tempDir.GetFullPath(".git")), Is.True);
        Assert.That(System.IO.File.Exists(tempDir.GetFullPath("ModuleB/.git")), Is.True);
        Assert.That(System.IO.File.Exists(tempDir.GetFullPath("ModuleB/B.txt")), Is.True);
        Assert.That(System.IO.File.Exists(tempDir.GetFullPath("ModuleB/ModuleA/.git")), Is.True);
        Assert.That(System.IO.File.Exists(tempDir.GetFullPath("ModuleB/ModuleA/A.txt")), Is.True);
    }

    [Test]
    public void Test_projects_created_url_ends_with_namespace_and_name()
    {
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProject("Test", @namespace: "testgroup", addDefaultUserAsMaintainer: true)
            .BuildServer();

        var client = server.CreateClient();
        var project = client.Projects["testgroup/Test"];

        Assert.That(project, Is.Not.Null);
        Assert.That(project.SshUrl, Does.EndWith($"testgroup{Path.DirectorySeparatorChar}test"));
        Assert.That(project.HttpUrl, Does.EndWith($"testgroup{Path.DirectorySeparatorChar}test"));
        Assert.That(project.WebUrl, Does.EndWith("testgroup/test"));
    }

    [Test]
    public void Test_get_languages()
    {
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew();

        var client = server.CreateClient(user);
        Assert.That(client.Projects.GetLanguages(project.Id.ToString(CultureInfo.InvariantCulture)), Is.Empty);

        project.Repository.Commit(user, "dummy", new[] { File.CreateFromText("test.cs", "dummy"), File.CreateFromText("test.js", "dummy") });
        var languages = client.Projects.GetLanguages(project.Id.ToString(CultureInfo.InvariantCulture));
        Assert.That(languages, Has.Count.EqualTo(2));
        Assert.That(languages["C#"], Is.EqualTo(0.5d));
        Assert.That(languages["JavaScript"], Is.EqualTo(0.5d));
    }

    [Test]
    public void Test_empty_repo()
    {
        using var server = new GitLabServer();
        var user = server.Users.AddNew();
        var project = user.Namespace.Projects.AddNew();

        Assert.That(project.ToClientProject(user).EmptyRepo, Is.True);

        project.Repository.Commit(user, "dummy");
        Assert.That(project.ToClientProject(user).EmptyRepo, Is.False);
    }

    [Test]
    public void Test_project_permissions_maintainer_with_project_access()
    {
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProject("Test", @namespace: "testgroup", addDefaultUserAsMaintainer: true)
            .BuildServer();

        var client = server.CreateClient();
        var project = client.Projects["testgroup/Test"];

        Assert.That(project, Is.Not.Null);
        Assert.That(project.Permissions.GroupAccess, Is.Null);
        Assert.That(project.Permissions.ProjectAccess.AccessLevel, Is.EqualTo(AccessLevel.Maintainer));
    }

    [Test]
    public void Test_project_permissions_with_no_access()
    {
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProject("Test", @namespace: "testgroup")
            .BuildServer();

        var client = server.CreateClient();
        var project = client.Projects["testgroup/Test"];

        Assert.That(project, Is.Not.Null);
        Assert.That(project.Permissions.GroupAccess, Is.Null);
        Assert.That(project.Permissions.ProjectAccess, Is.Null);
    }

    [Test]
    public void Test_project_permissions_with_group_access()
    {
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithGroup("testgroup", addDefaultUserAsMaintainer: true)
            .WithProject("Test", @namespace: "testgroup")
            .BuildServer();

        var client = server.CreateClient();
        var project = client.Projects["testgroup/Test"];

        Assert.That(project, Is.Not.Null);
        Assert.That(project.Permissions.ProjectAccess, Is.Null);
        Assert.That(project.Permissions.GroupAccess.AccessLevel, Is.EqualTo(AccessLevel.Maintainer));
    }

    [Test]
    public async Task CreateAsync_WhenMockCreatedWithSupportedOptions_TheyAreAvailableInModel()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithGroupOfFullPath("my-group", name: "MyGroup", addDefaultUserAsMaintainer: true)
            .BuildServer();

        var projectClient = server.CreateClient().Projects;

        var expected = new ProjectCreate()
        {
            Name = "MyProject",
            Path = "my-project",
            NamespaceId = server.Groups.First(g => string.Equals(g.Name, "MyGroup", StringComparison.Ordinal)).Id,
            Description = "Description",
            DefaultBranch = "foo",
            InitializeWithReadme = true,
            Topics = ["t1", "t2"],
            BuildTimeout = (int)TimeSpan.FromMinutes(15).TotalSeconds,
        };

        // Act
        var actual = await projectClient.CreateAsync(expected);

        // Assert
        Assert.That(actual.Name, Is.EqualTo(expected.Name));
        Assert.That(actual.Path, Is.EqualTo(expected.Path));
        Assert.That(actual.Description, Is.EqualTo(expected.Description));
        Assert.That(actual.Namespace.Id, Is.EqualTo(expected.NamespaceId));
        Assert.That(actual.NameWithNamespace, Is.EqualTo($"MyGroup / {expected.Name}"));
        Assert.That(actual.PathWithNamespace, Is.EqualTo($"my-group/{expected.Path}"));
        Assert.That(actual.DefaultBranch, Is.EqualTo(expected.DefaultBranch));
        Assert.That(actual.Topics, Is.EquivalentTo(expected.Topics));
        Assert.That(actual.BuildTimeout, Is.EqualTo(expected.BuildTimeout));
    }

    [Test]
    public async Task CreateAsync_WhenInitializeWithReadmeIsFalse_ItIgnoresDefaultBranch()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .BuildServer();

        var projectClient = server.CreateClient().Projects;

        var expected = new ProjectCreate()
        {
            Name = "MyProject",
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
    public void CreateAsync_WhenProjectPathAlreadyExists_ItThrows()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProjectOfFullPath("Test/duplicate")
            .BuildServer();

        var projectClient = server.CreateClient().Projects;

        // Act
        var ex = Assert.CatchAsync<GitLabException>(() =>
            projectClient.CreateAsync(new()
            {
                Path = "DUPLICATE", // GitLab path is case-INsensitive
                Name = "project2",
            }));

        // Assert
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorMessage, Contains.Substring("has already been taken"));
    }

    [Test]
    public void CreateAsync_WhenProjectNameAlreadyExists_ItThrows()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProjectOfFullPath("Test/duplicate")
            .BuildServer();

        var projectClient = server.CreateClient().Projects;

        // Act
        var ex = Assert.ThrowsAsync<GitLabException>(() =>
            projectClient.CreateAsync(new()
            {
                Path = "project2",
                Name = "duplicate", // GitLab name is case-Sensitive
            }));

        // Assert
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(ex.ErrorMessage, Contains.Substring("has already been taken"));
    }

    [Test]
    public async Task CreateAsync_WhenProjectNameOfDifferentCaseAlreadyExists_ItWorks()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProjectOfFullPath("Test/not_duplicate")
            .BuildServer();

        var projectClient = server.CreateClient().Projects;

        // Act
        var newProject = await projectClient.CreateAsync(new()
        {
            Path = "project2",
            Name = "NOT_DUPLICATE", // GitLab name is case-Sensitive
        });

        // Assert
        Assert.That(newProject.Name, Is.EqualTo("NOT_DUPLICATE"));
    }

    [Test]
    public void UpdateAsync_WhenProjectNotFound_ItThrows()
    {
        // Arrange
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .BuildServer();

        var projectClient = server.CreateClient().Projects;

        // Act
        var ex = Assert.CatchAsync<GitLabException>(() =>
            projectClient.UpdateAsync(int.MaxValue, new()
            {
                Visibility = VisibilityLevel.Private,
            }));

        // Assert
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteAsync_WhenProjectExists_ItIsDeleted()
    {
        var projectFullPath = $"Test/{nameof(DeleteAsync_WhenProjectExists_ItIsDeleted)}";
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .WithProjectOfFullPath(projectFullPath)
            .BuildServer();

        var projectClient = server.CreateClient().Projects;

        // Act
        await projectClient.DeleteAsync(projectFullPath);

        // Assert
        Assert.CatchAsync<GitLabException>(() => projectClient.GetAsync(projectFullPath));
    }

    [Test]
    public void DeleteAsync_WhenProjectNotFound_ItThrows()
    {
        using var server = new GitLabConfig()
            .WithUser("Test", isDefault: true)
            .BuildServer();

        var projectClient = server.CreateClient().Projects;

        // Act
        var ex = Assert.CatchAsync<GitLabException>(() => projectClient.DeleteAsync(int.MaxValue));

        // Assert
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
