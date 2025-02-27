using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NGitLab.Models;

namespace NGitLab;

public interface IProjectClient
{
    /// <summary>
    /// Gets a list of projects for which the authenticated user is a member.
    /// </summary>
    IEnumerable<Project> Accessible { get; }

    /// <summary>
    /// Gets a list of projects owned by the authenticated user.
    /// </summary>
    IEnumerable<Project> Owned { get; }

    /// <summary>
    /// Gets a list of projects which the authenticated user can see.
    /// </summary>
    IEnumerable<Project> Visible { get; }

    /// <summary>
    /// Gets a list of all GitLab projects (admin only).
    /// </summary>
    IEnumerable<Project> Get(ProjectQuery query);

    /// <inheritdoc cref="Get(ProjectQuery)"/>
    GitLabCollectionResponse<Project> GetAsync(ProjectQuery query);

    Project this[long id] { get; }

    /// <summary>
    /// Returns the project with the provided full name in the form Namespace/Name.
    /// </summary>
    /// <remarks>https://github.com/gitlabhq/gitlabhq/blob/master/doc/api/projects.md#get-single-project</remarks>
    Project this[string fullName] { get; }

    Project Create(ProjectCreate project);

    Task<Project> CreateAsync(ProjectCreate project, CancellationToken cancellationToken = default);

    Project Update(string id, ProjectUpdate projectUpdate);

    Task<Project> UpdateAsync(ProjectId projectId, ProjectUpdate projectUpdate, CancellationToken cancellationToken = default);

    void Delete(long id);

    Task DeleteAsync(ProjectId projectId, CancellationToken cancellationToken = default);

    void Archive(long id);

    void Unarchive(long id);

    /// <summary>
    /// Uploads a file to the specified project to be used in an issue or merge request description, or a comment.
    /// </summary>
    UploadedProjectFile UploadFile(string id, FormDataContent data);

    Project GetById(long id, SingleProjectQuery query);

    Task<Project> GetByIdAsync(long id, SingleProjectQuery query, CancellationToken cancellationToken = default);

    Task<Project> GetByNamespacedPathAsync(string path, SingleProjectQuery query = null, CancellationToken cancellationToken = default);

    Task<Project> GetAsync(ProjectId projectId, SingleProjectQuery query = null, CancellationToken cancellationToken = default);

    Project Fork(string id, ForkProject forkProject);

    Task<Project> ForkAsync(string id, ForkProject forkProject, CancellationToken cancellationToken = default);

    IEnumerable<Project> GetForks(string id, ForkedProjectQuery query);

    GitLabCollectionResponse<Project> GetForksAsync(string id, ForkedProjectQuery query);

    /// <summary>
    /// Gets a list of ancestor groups for this project.
    /// See https://docs.gitlab.com/ee/api/projects.html#list-a-projects-groups
    /// </summary>
    /// <param name="projectId">The project's id or path.</param>
    /// <param name="query">The query parameters</param>
    /// <returns>All ancestor groups.</returns>
    GitLabCollectionResponse<Group> GetGroupsAsync(ProjectId projectId, ProjectGroupsQuery query);

    GitLabCollectionResponse<ProjectTemplate> GetProjectTemplatesAsync(ProjectId projectId, ProjectTemplateType projectTemplateType);

    Task<ProjectMergeRequestTemplate> GetProjectMergeRequestTemplateAsync(ProjectId projectId, string name, CancellationToken cancellationToken = default);

    Dictionary<string, double> GetLanguages(string id);
}
