using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NGitLab.Mock.Internals;
using NGitLab.Models;

namespace NGitLab.Mock.Clients;

internal sealed class ProjectClient : ClientBase, IProjectClient
{
    public ProjectClient(ClientContext context)
        : base(context)
    {
    }

    public Models.Project this[long id]
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                return GetProject(id, ProjectPermission.View).ToClientProject(Context.User);
            }
        }
    }

    public Models.Project this[string fullName]
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                return GetProject(fullName, ProjectPermission.View).ToClientProject(Context.User);
            }
        }
    }

    public IEnumerable<Models.Project> Accessible
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                return Server.AllProjects.Where(project => project.IsUserMember(Context.User)).Select(project => project.ToClientProject(Context.User)).ToList();
            }
        }
    }

    public IEnumerable<Models.Project> Owned
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                return Server.AllProjects.Where(project => project.IsUserOwner(Context.User)).Select(project => project.ToClientProject(Context.User)).ToList();
            }
        }
    }

    public IEnumerable<Models.Project> Visible
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                return Server.AllProjects.Where(project => project.CanUserViewProject(Context.User)).Select(project => project.ToClientProject(Context.User)).ToList();
            }
        }
    }

    public Models.Project Create(ProjectCreate project)
    {
        if (project.Topics != null && project.Tags != null)
            throw new InvalidOperationException("Cannot specify Topics and Tags. Use Topics only.");

        using (Context.BeginOperationScope())
        {
            project.Name ??= project.Path;

            var parentGroup = GetParentGroup(project.NamespaceId?.ToString(CultureInfo.InvariantCulture));

            var newProject = new Project(name: project.Name, path: project.Path)
            {
                Description = project.Description,
                Visibility = project.VisibilityLevel,
                Permissions =
                {
                    new(Context.User, AccessLevel.Owner),
                },
            };

            newProject.Topics = (project.Topics ?? project.Tags)?.ToArray() ?? newProject.Topics;

            // Note: GitLab ignores the DefaultBranch unless InitializeWithReadme = true.
            if (!string.IsNullOrEmpty(project.DefaultBranch) && project.InitializeWithReadme)
                newProject.DefaultBranch = project.DefaultBranch;

            if (project.BuildTimeout != null)
                newProject.BuildTimeout = TimeSpan.FromSeconds(project.BuildTimeout.Value);

            parentGroup.Projects.Add(newProject);
            return newProject.ToClientProject(Context.User);
        }
    }

    public async Task<Models.Project> CreateAsync(ProjectCreate project, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Create(project);
    }

    private Group GetParentGroup(string namespaceId)
    {
        Group parentGroup;
        if (namespaceId != null)
        {
            parentGroup = Server.AllGroups.FindGroup(namespaceId);
            if (parentGroup == null || !parentGroup.CanUserViewGroup(Context.User))
                throw GitLabException.NotFound();

            if (!parentGroup.CanUserAddProject(Context.User))
                throw GitLabException.Forbidden();
        }
        else
        {
            parentGroup = Context.User.Namespace;
        }

        return parentGroup;
    }

    public void Delete(long id)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(id, ProjectPermission.Delete);
            project.Remove();
        }
    }

    public async Task DeleteAsync(ProjectId projectId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        using (Context.BeginOperationScope())
        {
            var project = GetProject(projectId, ProjectPermission.Delete);
            project.Remove();
        }
    }

    public void Archive(long id)
    {
        var project = GetProject(id, ProjectPermission.Edit);
        project.Archived = true;
    }

    public void Unarchive(long id)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(id, ProjectPermission.Edit);
            project.Archived = false;
        }
    }

    public Models.Project Fork(string id, ForkProject forkProject)
    {
        EnsureUserIsAuthenticated();

        using (Context.BeginOperationScope())
        {
            var project = GetProject(id, ProjectPermission.View);
            var group = forkProject.Namespace != null ? GetParentGroup(forkProject.Namespace) : Context.User.Namespace;
            var newProject = project.Fork(group, Context.User, forkProject.Name);
            return newProject.ToClientProject(Context.User);
        }
    }

    public IEnumerable<Models.Project> Get(ProjectQuery query)
    {
        using (Context.BeginOperationScope())
        {
            if (query.MinAccessLevel != null
             || query.LastActivityAfter != null
             || query.Search != null
             || query.Statistics is true)
            {
                throw new NotImplementedException();
            }

            var projects = Server.AllProjects;

            if (query.Archived != null)
            {
                projects = projects.Where(p => query.Archived == p.Archived);
            }

            if (query.Visibility != null)
            {
                projects = projects.Where(p => query.Visibility == p.Visibility);
            }

            switch (query.Scope)
            {
                case ProjectQueryScope.Accessible:
                    projects = projects.Where(p => p.IsUserMember(Context.User));
                    break;
                case ProjectQueryScope.Owned:
                    projects = projects.Where(p => p.IsUserOwner(Context.User));
                    break;
                case ProjectQueryScope.All:
                    projects = projects.Where(p => p.CanUserViewProject(Context.User));
                    break;
            }

            if (query.Topics.Any())
            {
                projects = projects.Where(p => query.Topics.All(t => p.Topics.Contains(t, StringComparer.Ordinal)));
            }

            if (query.UserId != null)
            {
                projects = projects.Where(p => p.IsUserOwner(Context.User));
            }

            if (query.OrderBy is "id")
            {
                if (query.Ascending is null or true)
                {
                    projects = projects.OrderBy(p => p.Id);
                }
                else
                {
                    projects = projects.OrderByDescending(p => p.Id);
                }
            }
            else if (query.OrderBy is not null)
            {
                throw new NotImplementedException();
            }

            return projects.Select(project => project.ToClientProject(Context.User)).ToList();
        }
    }

    public Models.Project GetById(long id, SingleProjectQuery query) => this[id];

    public Task<Models.Project> GetByIdAsync(long id, SingleProjectQuery query, CancellationToken cancellationToken = default) =>
        GetAsync(new ProjectId(id), query, cancellationToken);

    public Task<Models.Project> GetByNamespacedPathAsync(string path, SingleProjectQuery query = null, CancellationToken cancellationToken = default) =>
        GetAsync(new ProjectId(path), query, cancellationToken);

    public async Task<Models.Project> GetAsync(ProjectId projectId, SingleProjectQuery query = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        using (Context.BeginOperationScope())
        {
            return GetProject(projectId, ProjectPermission.View).ToClientProject(Context.User);
        }
    }

    public IEnumerable<Models.Project> GetForks(string id, ForkedProjectQuery query)
    {
        using (Context.BeginOperationScope())
        {
            if (query.Archived != null
                || query.Membership != null
                || query.MinAccessLevel != null
                || query.OrderBy != null
                || query.PerPage != null
                || query.Search != null
                || query.Statistics != null
                || query.Visibility != null)
            {
                throw new NotImplementedException();
            }

            var upstream = GetProject(id, ProjectPermission.View);
            var matches = Server.AllProjects.Where(project => project.ForkedFrom?.Id == upstream.Id);
            matches = matches.Where(project => query.Owned == null || query.Owned == project.IsUserOwner(Context.User));

            return matches.Select(project => project.ToClientProject(Context.User)).ToList();
        }
    }

    public Task<ProjectMergeRequestTemplate> GetProjectMergeRequestTemplateAsync(ProjectId projectId, string name,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Dictionary<string, double> GetLanguages(string id)
    {
        // Basic implementation, the results are not expected to be accurrate
        using (Context.BeginOperationScope())
        {
            var project = GetProject(id, ProjectPermission.View);
            if (project.Repository.IsEmpty)
                return new(StringComparer.Ordinal);

            project.Repository.Checkout(project.DefaultBranch);

            var gitFolder = Path.Combine(project.Repository.FullPath, ".git");
            var files = Directory.GetFiles(project.Repository.FullPath, "*", SearchOption.AllDirectories)
                .Where(file => !file.StartsWith(gitFolder, StringComparison.Ordinal))
                .ToArray();

            Dictionary<string, double> result = new(StringComparer.Ordinal);
            AddByExtension("C#", ".cs");
            AddByExtension("HTML", ".html", ".htm");
            AddByExtension("JavaScript", ".js", ".jsx");
            AddByExtension("PowerShell", ".ps1");
            AddByExtension("TypeScript", ".ts", ".tsx");
            return result;

            void AddByExtension(string name, params string[] expectedExtensions)
            {
                var count = files.Count(file => expectedExtensions.Any(expectedExtension => file.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase)));
                if (count > 0)
                {
                    result.Add(name, count / (double)files.Length);
                }
            }
        }
    }

    public Models.Project Update(string id, ProjectUpdate projectUpdate)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(id, ProjectPermission.Edit);

            if (projectUpdate.Name != null)
            {
                project.Name = projectUpdate.Name;
            }

            if (projectUpdate.DefaultBranch != null)
            {
                project.DefaultBranch = projectUpdate.DefaultBranch;
            }

            if (projectUpdate.Description != null)
            {
                project.Description = projectUpdate.Description;
            }

            if (projectUpdate.Visibility.HasValue)
            {
                project.Visibility = projectUpdate.Visibility.Value;
            }

            if (projectUpdate.BuildTimeout.HasValue)
            {
                project.BuildTimeout = TimeSpan.FromSeconds(projectUpdate.BuildTimeout.Value);
            }

            if (projectUpdate.LfsEnabled.HasValue)
            {
                project.LfsEnabled = projectUpdate.LfsEnabled.Value;
            }

            if (projectUpdate.GroupRunnersEnabled.HasValue)
            {
                project.GroupRunnersEnabled = projectUpdate.GroupRunnersEnabled.Value;
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (projectUpdate.TagList != null)
            {
                project.Topics = projectUpdate.TagList.Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.Ordinal).ToArray();
            }
#pragma warning restore CS0618 // Type or member is obsolete

            if (projectUpdate.Topics is { Count: > 0 })
            {
                project.Topics = projectUpdate.Topics.Where(t => !string.IsNullOrEmpty(t)).Distinct(StringComparer.Ordinal).ToArray();
            }

            return project.ToClientProject(Context.User);
        }
    }

    public async Task<Models.Project> UpdateAsync(ProjectId projectId, ProjectUpdate projectUpdate, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Update(projectId.ValueAsString(), projectUpdate);
    }

    public UploadedProjectFile UploadFile(string id, FormDataContent data)
    {
        throw new NotImplementedException();
    }

    public GitLabCollectionResponse<Models.Project> GetAsync(ProjectQuery query)
    {
        return GitLabCollectionResponse.Create(Get(query));
    }

    [SuppressMessage("Design", "MA0042:Do not use blocking calls in an async method", Justification = "Would be an infinite recursion")]
    public async Task<Models.Project> ForkAsync(string id, ForkProject forkProject, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Fork(id, forkProject);
    }

    public GitLabCollectionResponse<Models.Project> GetForksAsync(string id, ForkedProjectQuery query)
    {
        return GitLabCollectionResponse.Create(GetForks(id, query));
    }

    public GitLabCollectionResponse<Models.Group> GetGroupsAsync(ProjectId projectId, ProjectGroupsQuery query)
    {
        throw new NotImplementedException();
    }

    public GitLabCollectionResponse<ProjectTemplate> GetProjectTemplatesAsync(ProjectId projectId, ProjectTemplateType projectTemplateType)
    {
        throw new NotImplementedException();
    }
}
