#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NGitLab.Models;
using Commit = LibGit2Sharp.Commit;

#pragma warning disable RS0026 // Adding optional parameters to public methods
#pragma warning disable RS0037 // Activate nullable values in public API

namespace NGitLab.Mock.Config;

public static class GitLabHelpers
{
    public static GitLabConfig Configure(this GitLabConfig config, Action<GitLabConfig> configure)
    {
        configure(config);
        return config;
    }

    public static GitLabUser Configure(this GitLabUser user, Action<GitLabUser> configure)
    {
        return Configure<GitLabUser>(user, configure);
    }

    public static GitLabGroup Configure(this GitLabGroup group, Action<GitLabGroup> configure)
    {
        return Configure<GitLabGroup>(group, configure);
    }

    public static GitLabProject Configure(this GitLabProject project, Action<GitLabProject> configure)
    {
        return Configure<GitLabProject>(project, configure);
    }

    public static GitLabCommit Configure(this GitLabCommit commit, Action<GitLabCommit> configure)
    {
        return Configure<GitLabCommit>(commit, configure);
    }

    public static GitLabIssue Configure(this GitLabIssue issue, Action<GitLabIssue> configure)
    {
        return Configure<GitLabIssue>(issue, configure);
    }

    public static GitLabMergeRequest Configure(this GitLabMergeRequest mergeRequest, Action<GitLabMergeRequest> configure)
    {
        return Configure<GitLabMergeRequest>(mergeRequest, configure);
    }

    private static T Configure<T>(this T gitLabObject, Action<T> configure)
        where T : GitLabObject
    {
        configure(gitLabObject);
        return gitLabObject;
    }

    public static GitLabPipeline Configure(this GitLabPipeline pipeline, Action<GitLabPipeline> configure)
    {
        configure(pipeline);
        return pipeline;
    }

    /// <summary>
    /// Add a user description in config
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="username">Username (required)</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabConfig WithUser(this GitLabConfig config, string username, Action<GitLabUser> configure)
    {
        return Configure(config, _ =>
        {
            var user = new GitLabUser
            {
                Username = username ?? throw new ArgumentNullException(nameof(username)),
            };

            config.Users.Add(user);
            configure(user);
        });
    }

    /// <summary>
    /// Add a user description in config
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="username">Username (required)</param>
    /// <param name="name">Name.</param>
    /// <param name="email">Email.</param>
    /// <param name="avatarUrl">Avatar URL.</param>
    /// <param name="isAdmin">Indicates if user is admin in GitLab server</param>
    /// <param name="isDefault">Define as default user for next descriptions</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabConfig WithUser(this GitLabConfig config, string username, string? name = null, string? email = null, string? avatarUrl = null, bool isAdmin = false, bool isDefault = false, Action<GitLabUser>? configure = null)
    {
        return WithUser(config, username, user =>
        {
            user.Name = name;
            user.Email = email;
            user.AvatarUrl = avatarUrl;
            user.IsAdmin = isAdmin;
            if (isDefault)
            {
                user.AsDefaultUser();
            }

            configure?.Invoke(user);
        });
    }

    /// <summary>
    /// Define a user as default for next descriptions
    /// </summary>
    /// <param name="user">User.</param>
    public static GitLabUser AsDefaultUser(this GitLabUser user)
    {
        return Configure(user, _ =>
        {
            user.Parent.DefaultUser = user.Username;
        });
    }

    /// <summary>
    /// Define a user as default for next descriptions
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="username">Username.</param>
    public static GitLabConfig SetDefaultUser(this GitLabConfig config, string username)
    {
        return Configure(config, _ =>
        {
            config.DefaultUser = username;
        });
    }

    /// <summary>
    /// Define default branch for next descriptions
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="branchName">Branch name.</param>
    /// <returns></returns>
    public static GitLabConfig SetDefaultBranch(this GitLabConfig config, string branchName)
    {
        return Configure(config, _ =>
        {
            config.DefaultBranch = branchName;
        });
    }

    /// <summary>
    /// Define default groups and projects visibility
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="visibility">Visibility.</param>
    /// <returns></returns>
    public static GitLabConfig SetDefaultVisibility(this GitLabConfig config, VisibilityLevel visibility)
    {
        return Configure(config, _ =>
        {
            config.DefaultVisibility = visibility;
        });
    }

    /// <summary>
    /// Add an explicit group description in config
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="name">Name.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabConfig WithGroup(this GitLabConfig config, string? name, Action<GitLabGroup> configure)
    {
        return Configure(config, _ =>
        {
            var group = new GitLabGroup
            {
                Name = name ?? Guid.NewGuid().ToString("D"),
            };

            config.Groups.Add(group);
            configure(group);
        });
    }

    /// <summary>
    /// Add an explicit group description in config
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="name">Name.</param>
    /// <param name="id">Explicit ID (config increment)</param>
    /// <param name="namespace">Parent namespace.</param>
    /// <param name="description">Description.</param>
    /// <param name="visibility">Visibility.</param>
    /// <param name="addDefaultUserAsMaintainer">Define default user as maintainer.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabConfig WithGroup(this GitLabConfig config, string? name = null, long id = default, string? @namespace = null, string? description = null, VisibilityLevel? visibility = null, bool addDefaultUserAsMaintainer = false, Action<GitLabGroup>? configure = null)
    {
        return WithGroup(config, name, group =>
        {
            if (id != default)
                group.Id = id;

            group.Namespace = @namespace;
            group.Description = description;
            group.Visibility = visibility;

            if (addDefaultUserAsMaintainer)
            {
                if (string.IsNullOrEmpty(group.Parent.DefaultUser))
                    throw new InvalidOperationException("Default user not configured");

                WithUserPermission(group, group.Parent.DefaultUser, AccessLevel.Maintainer);
            }

            configure?.Invoke(group);
        });
    }

    /// <summary>
    /// Add a group with the given full path, i.e., "{namespace}/{path}".
    /// The namespace is optional, and leading or trailing slashes are ignored.
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="fullPath">The fully qualified path of the group.</param>
    /// <param name="name">Optional name. Defaults to the path.</param>
    /// <param name="id">Optional explicit ID (config increment)</param>
    /// <param name="description">Optional description.</param>
    /// <param name="visibility">Optional visibility.</param>
    /// <param name="addDefaultUserAsMaintainer">Optionally define default user as maintainer.</param>
    /// <param name="configure">Optional configuration method</param>
    public static GitLabConfig WithGroupOfFullPath(this GitLabConfig config, string fullPath, string? name = null, long id = default, string? description = null, VisibilityLevel? visibility = null, bool addDefaultUserAsMaintainer = false, Action<GitLabGroup>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentNullException(nameof(fullPath));

        var span = fullPath.AsSpan().Trim('/');
        var slash = span.LastIndexOf('/');
        var path = slash == -1 ? span.ToString() : span[(slash + 1)..].ToString();
        var @namespace = slash == -1 ? null : span[..slash].ToString();

        return WithGroup(config, name ?? path, id, @namespace, description, visibility, addDefaultUserAsMaintainer, configure: group =>
        {
            group.Path = path;
            configure?.Invoke(group);
        });
    }

    /// <summary>
    /// Add a project description in config
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="name">Name.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabConfig WithProject(this GitLabConfig config, string? name, Action<GitLabProject> configure)
    {
        return Configure(config, _ =>
        {
            var project = new GitLabProject
            {
                Name = name,
            };

            config.Projects.Add(project);
            configure(project);
        });
    }

    /// <summary>
    /// Add a project description in config
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="name">Name.</param>
    /// <param name="id">Explicit ID (config increment)</param>
    /// <param name="namespace">Group fullname.</param>
    /// <param name="description">Description.</param>
    /// <param name="defaultBranch">Repository default branch.</param>
    /// <param name="visibility">Visibility.</param>
    /// <param name="initialCommit">Indicates if an initial commit is added.</param>
    /// <param name="addDefaultUserAsMaintainer">Define default user as maintainer.</param>
    /// <param name="clonePath">Path where to clone repository after server resolving</param>
    /// <param name="cloneParameters">Parameters passed to clone command</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabConfig WithProject(this GitLabConfig config, string? name = null, long id = default, string? @namespace = null, string? description = null,
                                           string? defaultBranch = null, VisibilityLevel visibility = VisibilityLevel.Internal, bool initialCommit = false,
                                           bool addDefaultUserAsMaintainer = false, string? clonePath = null, string? cloneParameters = null, Action<GitLabProject>? configure = null)
    {
        return WithProject(config, name, project =>
        {
            if (id != default)
                project.Id = id;

            project.Namespace = @namespace;
            project.Description = description;
            project.DefaultBranch = defaultBranch ?? config.DefaultBranch ?? "main";
            project.Visibility = visibility;
            project.ClonePath = clonePath;
            project.CloneParameters = cloneParameters;

            if (initialCommit)
            {
                WithCommit(project, "Initial commit", user: null, commit =>
                {
                    WithFile(commit, "README.md", $"# {name}{Environment.NewLine}");
                });
            }

            if (addDefaultUserAsMaintainer)
            {
                if (string.IsNullOrEmpty(project.Parent.DefaultUser))
                    throw new InvalidOperationException("Default user not configured");

                WithUserPermission(project, project.Parent.DefaultUser, AccessLevel.Maintainer);
            }

            configure?.Invoke(project);
        });
    }

    /// <summary>
    /// Add a project with the given full path, i.e., "{namespace}/{path}".
    /// If the namespace is not specified, the project will be created in a new top-level group.
    /// Leading or trailing slashes are ignored.
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="fullPath">The fully qualified path of the project.</param>
    /// <param name="name">Optional name. Defaults to the path.</param>
    /// <param name="id">Optional explicit ID (config increment)</param>
    /// <param name="description">Optional description.</param>
    /// <param name="defaultBranch">Optional repository default branch.</param>
    /// <param name="visibility">Optional visibility.</param>
    /// <param name="initialCommit">Indicates if an initial commit is added.</param>
    /// <param name="addDefaultUserAsMaintainer">Define default user as maintainer.</param>
    /// <param name="clonePath">Path where to clone repository after server resolving</param>
    /// <param name="cloneParameters">Parameters passed to clone command</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabConfig WithProjectOfFullPath(this GitLabConfig config, string? fullPath, string? name = null, long id = default, string? description = null,
                                                     string? defaultBranch = null, VisibilityLevel visibility = VisibilityLevel.Internal, bool initialCommit = false,
                                                     bool addDefaultUserAsMaintainer = false, string? clonePath = null, string? cloneParameters = null, Action<GitLabProject>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentNullException(nameof(fullPath));

        var span = fullPath.AsSpan().Trim('/');
        var slash = span.LastIndexOf('/');
        var path = slash == -1 ? span.ToString() : span[(slash + 1)..].ToString();
        var @namespace = slash == -1 ? null : span[..slash].ToString();

        return config.WithProject(
            name: name ?? path,
            id: id,
            @namespace: @namespace,
            description: description,
            defaultBranch: defaultBranch,
            visibility: visibility,
            initialCommit: initialCommit,
            addDefaultUserAsMaintainer: addDefaultUserAsMaintainer,
            clonePath: clonePath,
            cloneParameters: cloneParameters,
            configure: project =>
        {
            project.Path = path;
            configure?.Invoke(project);
        });
    }

    /// <summary>
    /// Add a group description in group
    /// </summary>
    /// <param name="group">Group.</param>
    /// <param name="name">Name (required)</param>
    /// <param name="color">Color in RGB hex format (example: #A1F8C3)</param>
    /// <param name="description">Description.</param>
    public static GitLabGroup WithLabel(this GitLabGroup group, string name, string? color = null, string? description = null)
    {
        return Configure(group, _ =>
        {
            var label = new GitLabLabel
            {
                Name = name ?? throw new ArgumentNullException(nameof(name)),
                Color = color,
                Description = description,
            };

            group.Labels.Add(label);
        });
    }

    /// <summary>
    /// Add a label description in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="name">Name (required)</param>
    /// <param name="color">Color in RGB hex format (example: #A1F8C3)</param>
    /// <param name="description">Description.</param>
    public static GitLabProject WithLabel(this GitLabProject project, string name, string? color = null, string? description = null)
    {
        return Configure(project, _ =>
        {
            var label = new GitLabLabel
            {
                Name = name ?? throw new ArgumentNullException(nameof(name)),
                Color = color,
                Description = description,
            };

            project.Labels.Add(label);
        });
    }

    /// <summary>
    /// Add a commit description in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="message">Message (required)</param>
    /// <param name="user">Author username (required if default user not defined)</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithCommit(this GitLabProject project, string? message, string? user, Action<GitLabCommit> configure)
    {
        return Configure(project, _ =>
        {
            var commit = new GitLabCommit
            {
                Message = message,
                User = user,
            };

            project.Commits.Add(commit);
            configure(commit);
        });
    }

    /// <summary>
    /// Add a commit description in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="message">Message (required)</param>
    /// <param name="user">Author username (required if default user not defined)</param>
    /// <param name="sourceBranch">Source branch (required if checkout or merge)</param>
    /// <param name="targetBranch">Target branch (required if merge)</param>
    /// <param name="fromBranch">From branch</param>
    /// <param name="tags">Tags.</param>
    /// <param name="alias">Alias to reference it in pipeline.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithCommit(this GitLabProject project, string? message = null, string? user = null, string? sourceBranch = null, string? targetBranch = null, string? fromBranch = null, IEnumerable<string>? tags = null, string? alias = null, Action<GitLabCommit>? configure = null)
    {
        return WithCommit(project, message, user, commit =>
        {
            commit.SourceBranch = sourceBranch;
            commit.TargetBranch = targetBranch;
            commit.FromBranch = fromBranch;
            commit.Alias = alias;
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    commit.Tags.Add(tag);
                }
            }

            configure?.Invoke(commit);
        });
    }

    /// <summary>
    /// Add a merge commit in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="sourceBranch">Source branch (required)</param>
    /// <param name="targetBranch">Target branch</param>
    /// <param name="user">Author username (required if default user not defined)</param>
    /// <param name="deleteSourceBranch">Indicates if source branch must be deleted after merge.</param>
    /// <param name="tags">Tags.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithMergeCommit(this GitLabProject project, string sourceBranch, string? targetBranch = null, string? user = null, bool deleteSourceBranch = false, IEnumerable<string>? tags = null, Action<GitLabCommit>? configure = null)
    {
        return WithCommit(project, $"Merge '{sourceBranch}' into '{targetBranch ?? project.DefaultBranch}'", user, commit =>
        {
            commit.SourceBranch = sourceBranch ?? throw new ArgumentNullException(nameof(sourceBranch));
            commit.TargetBranch = targetBranch ?? project.DefaultBranch;
            commit.DeleteSourceBranch = deleteSourceBranch;
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    commit.Tags.Add(tag);
                }
            }

            configure?.Invoke(commit);
        });
    }

    /// <summary>
    /// Add a tag description in commit
    /// </summary>
    /// <param name="commit">Commit.</param>
    /// <param name="name">Name (required)</param>
    public static GitLabCommit WithTag(this GitLabCommit commit, string name)
    {
        return Configure(commit, _ =>
        {
            commit.Tags.Add(name ?? throw new ArgumentNullException(nameof(name)));
        });
    }

    /// <summary>
    /// Add a file description in commit
    /// </summary>
    /// <param name="commit">Commit.</param>
    /// <param name="relativePath">Relative path (required)</param>
    /// <param name="content">File Content</param>
    public static GitLabCommit WithFile(this GitLabCommit commit, string relativePath, string content = "")
    {
        return Configure(commit, _ =>
        {
            commit.Files.Add(new GitLabFileDescriptor
            {
                Path = relativePath ?? throw new ArgumentNullException(nameof(relativePath)),
                Content = content,
            });
        });
    }

    /// <summary>
    /// Add a submodule in commit
    /// </summary>
    public static GitLabCommit WithSubModule(this GitLabCommit commit, string projectName)
    {
        return Configure(commit, _ =>
        {
            commit.SubModules.Add(new GitLabSubModuleDescriptor
            {
                ProjectName = projectName,
            });
        });
    }

    /// <summary>
    /// Add an issue description in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="title">Title.</param>
    /// <param name="author">Author username (required if default user not defined)</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithIssue(this GitLabProject project, string? title, string? author, Action<GitLabIssue> configure)
    {
        return Configure(project, _ =>
        {
            var issue = new GitLabIssue
            {
                Title = title,
                Author = author,
            };

            project.Issues.Add(issue);
            configure(issue);
        });
    }

    /// <summary>
    /// Add an issue description in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="title">Title.</param>
    /// <param name="id">Explicit ID (project increment)</param>
    /// <param name="description">Description.</param>
    /// <param name="author">Author username (required if default user nor defined)</param>
    /// <param name="assignee">Assignee username (allow multiple separated by ',')</param>
    /// <param name="milestone">Milestone title (create it if not exists)</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="updatedAt">Update date time.</param>
    /// <param name="closedAt">Close date time.</param>
    /// <param name="labels">Labels names.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithIssue(this GitLabProject project, string? title = null, long id = default, string? description = null, string? author = null, string? assignee = null, string? milestone = null, DateTime? createdAt = null, DateTime? updatedAt = null, DateTime? closedAt = null, IEnumerable<string>? labels = null, Action<GitLabIssue>? configure = null)
    {
        return WithIssue(project, title, author, issue =>
        {
            if (id != default)
                issue.Id = id;

            issue.Description = description;
            issue.Assignee = assignee;
            issue.Milestone = milestone;
            issue.CreatedAt = createdAt;
            issue.UpdatedAt = updatedAt;
            issue.ClosedAt = closedAt;
            if (labels != null)
            {
                foreach (var label in labels)
                {
                    WithLabel(issue, label);
                }
            }

            configure?.Invoke(issue);
        });
    }

    public static GitLabProject WithRelease(this GitLabProject project, string author, string tagName, DateTime? createdAt = null, DateTime? releasedAt = null)
    {
        return Configure(project, _ =>
        {
            var date = DateTime.UtcNow;
            var release = new GitLabReleaseInfo
            {
                Author = author,
                TagName = tagName,
                CreatedAt = createdAt ?? date,
                ReleasedAt = releasedAt ?? date,
            };

            project.Releases.Add(release);
        });
    }

    /// <summary>
    /// Add label in issue (create it if not exists)
    /// </summary>
    /// <param name="issue">Issue.</param>
    /// <param name="label">Label name (required)</param>
    public static GitLabIssue WithLabel(this GitLabIssue issue, string label)
    {
        return Configure(issue, _ =>
        {
            issue.Labels.Add(label ?? throw new ArgumentNullException(nameof(label)));
        });
    }

    /// <summary>
    /// Add merge request description in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="sourceBranch">Source branch.</param>
    /// <param name="title">Title.</param>
    /// <param name="author">Author username (required if default user not defined)</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithMergeRequest(this GitLabProject project, string? sourceBranch, string? title, string? author, Action<GitLabMergeRequest> configure)
    {
        return Configure(project, _ =>
        {
            var mergeRequest = new GitLabMergeRequest
            {
                Title = title,
                Author = author,
                SourceBranch = sourceBranch,
                TargetBranch = project.DefaultBranch,
            };

            project.MergeRequests.Add(mergeRequest);
            configure(mergeRequest);
        });
    }

    /// <summary>
    /// Add merge request description in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="sourceBranch">Source branch (required)</param>
    /// <param name="title">Title.</param>
    /// <param name="id">Explicit ID (project increment)</param>
    /// <param name="targetBranch">Target branch (use default branch if null)</param>
    /// <param name="description">Description.</param>
    /// <param name="author">Author username (required if default user not defined)</param>
    /// <param name="assignee">Assignee username.</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="updatedAt">Update date time.</param>
    /// <param name="closedAt">Close date time.</param>
    /// <param name="mergedAt">Merge date time.</param>
    /// <param name="approvers">Approvers usernames.</param>
    /// <param name="labels">Labels names.</param>
    /// <param name="milestone">Milestone name.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithMergeRequest(this GitLabProject project, string? sourceBranch = null, string? title = null, long id = default, string? targetBranch = null, string? description = null, string? author = null, string? assignee = null, DateTime? createdAt = null, DateTime? updatedAt = null, DateTime? closedAt = null, DateTime? mergedAt = null, IEnumerable<string>? approvers = null, IEnumerable<string>? labels = null, string? milestone = null, Action<GitLabMergeRequest>? configure = null)
    {
        return WithMergeRequest(project, sourceBranch, title, author, mergeRequest =>
        {
            if (id != default)
                mergeRequest.Id = id;

            mergeRequest.Description = description;
            mergeRequest.Assignee = assignee;
            mergeRequest.TargetBranch = targetBranch;
            mergeRequest.CreatedAt = createdAt;
            mergeRequest.UpdatedAt = updatedAt;
            mergeRequest.ClosedAt = closedAt;
            mergeRequest.MergedAt = mergedAt;
            mergeRequest.Milestone = milestone;

            if (labels != null)
            {
                foreach (var label in labels)
                {
                    WithLabel(mergeRequest, label);
                }
            }

            if (approvers != null)
            {
                foreach (var approver in approvers)
                {
                    WithApprover(mergeRequest, approver);
                }
            }

            configure?.Invoke(mergeRequest);
        });
    }

    /// <summary>
    /// Add label in merge request (create it if not exists)
    /// </summary>
    /// <param name="mergeRequest">Merge request.</param>
    /// <param name="label">Label name (required)</param>
    public static GitLabMergeRequest WithLabel(this GitLabMergeRequest mergeRequest, string label)
    {
        return Configure(mergeRequest, _ =>
        {
            mergeRequest.Labels.Add(label ?? throw new ArgumentNullException(nameof(label)));
        });
    }

    /// <summary>
    /// Add approver in merge request
    /// </summary>
    /// <param name="mergeRequest">Merge request.</param>
    /// <param name="approver">Approver username (required)</param>
    public static GitLabMergeRequest WithApprover(this GitLabMergeRequest mergeRequest, string approver)
    {
        return Configure(mergeRequest, _ =>
        {
            mergeRequest.Approvers.Add(approver ?? throw new ArgumentNullException(nameof(approver)));
        });
    }

    /// <summary>
    /// Add user permission in group
    /// </summary>
    /// <param name="group">Group.</param>
    /// <param name="user">Username (required)</param>
    /// <param name="level">Access level (required)</param>
    public static GitLabGroup WithUserPermission(this GitLabGroup group, string user, AccessLevel level)
    {
        return Configure(group, _ =>
        {
            var permission = new GitLabPermission
            {
                User = user ?? throw new ArgumentNullException(nameof(user)),
                Level = level,
            };

            group.Permissions.Add(permission);
        });
    }

    /// <summary>
    /// Add user permission in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="user">Username (required)</param>
    /// <param name="level">Access level (required)</param>
    public static GitLabProject WithUserPermission(this GitLabProject project, string user, AccessLevel level)
    {
        return Configure(project, _ =>
        {
            var permission = new GitLabPermission
            {
                User = user ?? throw new ArgumentNullException(nameof(user)),
                Level = level,
            };

            project.Permissions.Add(permission);
        });
    }

    /// <summary>
    /// Add group permission in group
    /// </summary>
    /// <param name="grp">Group.</param>
    /// <param name="groupName">Group fullname (required)</param>
    /// <param name="level">Access level (required)</param>
    public static GitLabGroup WithGroupPermission(this GitLabGroup grp, string groupName, AccessLevel level)
    {
        return Configure(grp, _ =>
        {
            var permission = new GitLabPermission
            {
                Group = groupName ?? throw new ArgumentNullException(nameof(groupName)),
                Level = level,
            };

            grp.Permissions.Add(permission);
        });
    }

    /// <summary>
    /// Add group permission in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="groupName">Group fullname (required)</param>
    /// <param name="level">Access level (required)</param>
    public static GitLabProject WithGroupPermission(this GitLabProject project, string groupName, AccessLevel level)
    {
        return Configure(project, _ =>
        {
            var permission = new GitLabPermission
            {
                Group = groupName ?? throw new ArgumentNullException(nameof(groupName)),
                Level = level,
            };

            project.Permissions.Add(permission);
        });
    }

    /// <summary>
    /// Add milestone in group
    /// </summary>
    /// <param name="group">Group.</param>
    /// <param name="title">Title (required)</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabGroup WithMilestone(this GitLabGroup group, string title, Action<GitLabMilestone> configure)
    {
        return Configure(group, _ =>
        {
            var milestone = new GitLabMilestone
            {
                Title = title ?? throw new ArgumentNullException(nameof(title)),
            };

            group.Milestones.Add(milestone);
            configure(milestone);
        });
    }

    /// <summary>
    /// Add milestone in group
    /// </summary>
    /// <param name="group">Group.</param>
    /// <param name="title">Title (required)</param>
    /// <param name="id">Explicit ID (config increment)</param>
    /// <param name="description">Description.</param>
    /// <param name="dueDate">Due date time.</param>
    /// <param name="startDate">Start date time.</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="updatedAt">Update date time.</param>
    /// <param name="closedAt">Close date time.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabGroup WithMilestone(this GitLabGroup group, string title, long id = default, string? description = null, DateTime? dueDate = null, DateTime? startDate = null, DateTime? createdAt = null, DateTime? updatedAt = null, DateTime? closedAt = null, Action<GitLabMilestone>? configure = null)
    {
        return WithMilestone(group, title, milestone =>
        {
            if (id != default)
                milestone.Id = id;

            milestone.Description = description;
            milestone.DueDate = dueDate;
            milestone.StartDate = startDate;
            milestone.CreatedAt = createdAt;
            milestone.UpdatedAt = updatedAt;
            milestone.ClosedAt = closedAt;

            configure?.Invoke(milestone);
        });
    }

    /// <summary>
    /// Add milestone in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="title">Title (required)</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithMilestone(this GitLabProject project, string title, Action<GitLabMilestone> configure)
    {
        return Configure(project, _ =>
        {
            var milestone = new GitLabMilestone
            {
                Title = title ?? throw new ArgumentNullException(nameof(title)),
            };

            project.Milestones.Add(milestone);
            configure(milestone);
        });
    }

    /// <summary>
    /// Add milestone in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="title">Title (required)</param>
    /// <param name="id">Explicit ID (config increment)</param>
    /// <param name="description">Description.</param>
    /// <param name="dueDate">Due date time.</param>
    /// <param name="startDate">Start date time.</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="updatedAt">Update date time.</param>
    /// <param name="closedAt">Close date time.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithMilestone(this GitLabProject project, string title, long id = default, string? description = null, DateTime? dueDate = null, DateTime? startDate = null, DateTime? createdAt = null, DateTime? updatedAt = null, DateTime? closedAt = null, Action<GitLabMilestone>? configure = null)
    {
        return WithMilestone(project, title, milestone =>
        {
            if (id != default)
                milestone.Id = id;

            milestone.Description = description;
            milestone.DueDate = dueDate;
            milestone.StartDate = startDate;
            milestone.CreatedAt = createdAt;
            milestone.UpdatedAt = updatedAt;
            milestone.ClosedAt = closedAt;

            configure?.Invoke(milestone);
        });
    }

    /// <summary>
    /// Add comment in issue
    /// </summary>
    /// <param name="issue">Issue.</param>
    /// <param name="message">Message.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabIssue WithComment(this GitLabIssue issue, string? message, Action<GitLabComment> configure)
    {
        return WithComment<GitLabIssue>(issue, message, configure);
    }

    /// <summary>
    /// Add comment in merge request
    /// </summary>
    /// <param name="mergeRequest">Merge request.</param>
    /// <param name="message">Message.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabMergeRequest WithComment(this GitLabMergeRequest mergeRequest, string? message, Action<GitLabComment> configure)
    {
        return WithComment<GitLabMergeRequest>(mergeRequest, message, configure);
    }

    private static T WithComment<T>(this T obj, string? message, Action<GitLabComment> configure)
        where T : GitLabObject
    {
        return Configure(obj, _ =>
        {
            var comment = new GitLabComment
            {
                Message = message,
            };

            switch (obj)
            {
                case GitLabIssue issue:
                    issue.Comments.Add(comment);
                    break;
                case GitLabMergeRequest mergeRequest:
                    mergeRequest.Comments.Add(comment);
                    break;
                default:
                    throw new InvalidOperationException($"Cannot add comment in {typeof(T).Name}");
            }

            configure(comment);
        });
    }

    /// <summary>
    /// Add comment in issue
    /// </summary>
    /// <param name="issue">Issue.</param>
    /// <param name="message">Message (required)</param>
    /// <param name="id">Explicit ID (issue increment)</param>
    /// <param name="author">Author username (required if default user not defined)</param>
    /// <param name="system">Indicates if comment is from GitLab system.</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="updatedAt">Update date time.</param>
    /// <param name="thread">Comment thread (all comments with same thread are grouped)</param>
    /// <param name="resolvable">Indicates if comment is resolvable.</param>
    /// <param name="resolved">Indicates if comment is resolved.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabIssue WithComment(this GitLabIssue issue, string? message = null, long id = default, string? author = null, bool system = false, DateTime? createdAt = null, DateTime? updatedAt = null, string? thread = null, bool resolvable = false, bool resolved = false, Action<GitLabComment>? configure = null)
    {
        return WithComment<GitLabIssue>(issue, message, id, author, system, createdAt, updatedAt, thread, resolvable, resolved, configure);
    }

    /// <summary>
    /// Add comment in issue
    /// </summary>
    /// <param name="mergeRequest">Merge request.</param>
    /// <param name="message">Message (required)</param>
    /// <param name="id">Explicit ID (merge request increment)</param>
    /// <param name="author">Author username (required if default user not defined)</param>
    /// <param name="system">Indicates if comment is from GitLab system.</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="updatedAt">Update date time.</param>
    /// <param name="thread">Comment thread (all comments with same thread are grouped)</param>
    /// <param name="resolvable">Indicates if comment is resolvable.</param>
    /// <param name="resolved">Indicates if comment is resolved.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabMergeRequest WithComment(this GitLabMergeRequest mergeRequest, string? message = null, long id = default, string? author = null, bool system = false, DateTime? createdAt = null, DateTime? updatedAt = null, string? thread = null, bool resolvable = false, bool resolved = false, Action<GitLabComment>? configure = null)
    {
        return WithComment<GitLabMergeRequest>(mergeRequest, message, id, author, system, createdAt, updatedAt, thread, resolvable, resolved, configure);
    }

    private static T WithComment<T>(this T obj, string? message, long id = default, string? author = null, bool system = false, DateTime? createdAt = null, DateTime? updatedAt = null, string? thread = null, bool resolvable = false, bool resolved = false, Action<GitLabComment>? configure = null)
        where T : GitLabObject
    {
        return WithComment(obj, message, comment =>
        {
            if (id != default)
                comment.Id = id;

            comment.Author = author;
            comment.System = system;
            comment.CreatedAt = createdAt;
            comment.UpdatedAt = updatedAt;
            comment.Thread = thread;
            comment.Resolvable = resolvable;
            comment.Resolved = resolved;

            configure?.Invoke(comment);
        });
    }

    /// <summary>
    /// Add commit mention comment in issue
    /// </summary>
    /// <param name="issue">Issue.</param>
    /// <param name="message">Message.</param>
    /// <param name="innerHtml">Inner HTML.</param>
    /// <param name="id">Explicit ID (issue increment)</param>
    /// <param name="author">Author username (required if default user not defined)</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="updatedAt">Update date time.</param>
    public static GitLabIssue WithSystemComment(this GitLabIssue issue, string? message = null, string? innerHtml = null, long id = default, string? author = null, DateTime? createdAt = null, DateTime? updatedAt = null)
    {
        return WithSystemComment<GitLabIssue>(issue, message, innerHtml, id, author, createdAt, updatedAt);
    }

    /// <summary>
    /// Add commit mention comment in merge request
    /// </summary>
    /// <param name="mergeRequest">Merge request.</param>
    /// <param name="message">Message.</param>
    /// <param name="innerHtml">Inner HTML.</param>
    /// <param name="id">Explicit ID (merge request increment)</param>
    /// <param name="author">Author username (required if default user not defined)</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="updatedAt">Update date time.</param>
    public static GitLabMergeRequest WithSystemComment(this GitLabMergeRequest mergeRequest, string? message = null, string? innerHtml = null, long id = default, string? author = null, DateTime? createdAt = null, DateTime? updatedAt = null)
    {
        return WithSystemComment<GitLabMergeRequest>(mergeRequest, message, innerHtml, id, author, createdAt, updatedAt);
    }

    private static T WithSystemComment<T>(this T obj, string? message, string? innerHtml, long id, string? author, DateTime? createdAt, DateTime? updatedAt)
        where T : GitLabObject
    {
        var body = innerHtml == null ? message : $"{message}\n\n{innerHtml}";
        return WithComment(obj, body, id: id, author: author, system: true, createdAt: createdAt, updatedAt: updatedAt);
    }

    /// <summary>
    /// Add pipeline in project
    /// </summary>
    /// <param name="project">Project.</param>
    /// <param name="ref">Commit alias reference.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabProject WithPipeline(this GitLabProject project, string @ref, Action<GitLabPipeline> configure)
    {
        return Configure(project, _ =>
        {
            var pipeline = new GitLabPipeline(project)
            {
                Commit = @ref ?? throw new ArgumentNullException(nameof(@ref)),
            };

            project.Pipelines.Add(pipeline);
            configure(pipeline);
        });
    }

    /// <summary>
    /// Add job in pipeline
    /// </summary>
    /// <param name="pipeline">Pipeline.</param>
    /// <param name="name">Name.</param>
    /// <param name="stage">Stage (build by default)</param>
    /// <param name="status">Status (Manual by default)</param>
    /// <param name="createdAt">Creation date time.</param>
    /// <param name="startedAt">Start date time.</param>
    /// <param name="finishedAt">Finish date time.</param>
    /// <param name="allowFailure">Indicates if failure is allowed.</param>
    /// <param name="downstreamPipeline">Downstream pipeline.</param>
    /// <param name="configure">Configuration method</param>
    public static GitLabPipeline WithJob(this GitLabPipeline pipeline, string? name = null, string? stage = null, JobStatus status = JobStatus.Unknown, DateTime? createdAt = null, DateTime? startedAt = null, DateTime? finishedAt = null, bool allowFailure = false, GitLabPipeline? downstreamPipeline = null, Action<GitLabJob>? configure = null)
    {
        return Configure(pipeline, _ =>
        {
            if (pipeline.Parent == null)
                throw new InvalidOperationException($"Parent project not set on pipeline {pipeline}");

            var job = new GitLabJob
            {
                Name = name,
                Stage = stage,
                Status = status,
                CreatedAt = createdAt,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                AllowFailure = allowFailure,
                DownstreamPipeline = downstreamPipeline,
            };

            pipeline.Jobs.Add(job);
            configure?.Invoke(job);
        });
    }

    /// <summary>
    /// Create and fill server from config
    /// </summary>
    /// <param name="config">Config.</param>
    public static GitLabServer BuildServer(this GitLabConfig config)
    {
        var server = CreateServer(config);
        foreach (var user in config.Users)
        {
            CreateUser(server, user);
        }

        foreach (var group in config.Groups.OrderBy(x =>
            string.IsNullOrEmpty(x.Namespace) ? x.Name : $"{x.Namespace}/{x.Name}", StringComparer.Ordinal))
        {
            CreateGroup(server, group);
        }

        foreach (var project in config.Projects)
        {
            CreateProject(server, project);
        }

        return server;
    }

    /// <summary>
    /// Create client from config
    /// </summary>
    /// <param name="config">Config.</param>
    /// <param name="username">Username that use client (if not defined, use default user or the first in the list)</param>
    public static IGitLabClient BuildClient(this GitLabConfig config, string? username = null)
    {
        return CreateClient(BuildServer(config), username ?? config.DefaultUser ?? config.Users.FirstOrDefault()?.Username ?? throw new InvalidOperationException("No user configured"));
    }

    /// <summary>
    /// Create client from server
    /// </summary>
    /// <param name="server">Server.</param>
    /// <param name="username">Username that use client (if not defined, use the first in the list)</param>
    public static IGitLabClient CreateClient(this GitLabServer server, string? username = null)
    {
        username ??= server.Users.FirstOrDefault()?.UserName ?? throw new InvalidOperationException("No user configured");
        return server.CreateClient(server.Users.First(x => string.Equals(x.UserName, username, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Create config from server
    /// </summary>
    /// <param name="server">Server.</param>
    public static GitLabConfig ToConfig(this GitLabServer server)
    {
        var config = new GitLabConfig();
        foreach (var user in server.Users)
        {
            config.Users.Add(ToConfig(user));
        }

        foreach (var group in server.AllGroups.Where(x => !x.IsUserNamespace))
        {
            config.Groups.Add(ToConfig(group));
        }

        foreach (var project in server.AllProjects)
        {
            config.Projects.Add(ToConfig(project));
        }

        return config;
    }

    private static GitLabServer CreateServer(GitLabConfig config)
    {
        return new GitLabServer
        {
            DefaultBranchName = config.DefaultBranch ?? "main",
            DefaultForkVisibilityLevel = config.DefaultVisibility,
            Url = new Uri(config.Url ?? Path.GetTempPath()),
        };
    }

    private static void CreateUser(GitLabServer server, GitLabUser user)
    {
        server.Users.Add(new User(user.Username ?? throw new ArgumentException(@"user.Username == null", nameof(user)))
        {
            Id = user.Id,
            Name = user.Name ?? user.Username,
            Email = user.Email ?? $"{user.Username}@example.com",
            AvatarUrl = user.AvatarUrl,
            IsAdmin = user.IsAdmin,
        });
    }

    private static void CreateGroup(GitLabServer server, GitLabGroup group)
    {
        var grp = new Group(group.Name ?? throw new ArgumentException(@"group.Name == null", nameof(group)))
        {
            Id = group.Id,
            Path = group.Path,
            Description = group.Description,
            Visibility = group.Visibility ?? group.Parent.DefaultVisibility,
        };

        if (string.IsNullOrEmpty(group.Namespace))
        {
            server.Groups.Add(grp);
        }
        else
        {
            var parent = GetOrCreateGroup(server, group.Namespace);
            parent.Groups.Add(grp);
        }

        foreach (var label in group.Labels)
        {
            CreateLabel(grp, label);
        }

        foreach (var permission in group.Permissions)
        {
            CreatePermission(server, grp, permission);
        }

        foreach (var milestone in group.Milestones)
        {
            CreateMilestone(grp, milestone);
        }
    }

    private static void CreateProject(GitLabServer server, GitLabProject project)
    {
        var projectName = project.Name ?? project.Path ?? Guid.NewGuid().ToString("D");
        var prj = new Project(projectName, project.Path)
        {
            Id = project.Id,
            Description = project.Description,
            DefaultBranch = project.DefaultBranch ?? server.DefaultBranchName,
            Visibility = project.Visibility ?? project.Parent.DefaultVisibility,
            ForkingAccessLevel = project.ForkingAccessLevel,
        };

        var group = GetOrCreateGroup(server, project.Namespace ?? Guid.NewGuid().ToString("D"));
        group.Projects.Add(prj);

        var aliases = new Dictionary<string, Commit>(StringComparer.Ordinal);
        foreach (var commit in project.Commits)
        {
            var cmt = CreateCommit(server, prj, commit);
            if (!string.IsNullOrEmpty(commit.Alias))
                aliases[commit.Alias] = cmt;
        }

        foreach (var label in project.Labels)
        {
            CreateLabel(prj, label);
        }

        foreach (var milestone in project.Milestones)
        {
            CreateMilestone(prj, milestone);
        }

        foreach (var issue in project.Issues)
        {
            CreateIssue(server, prj, issue);
        }

        foreach (var release in project.Releases)
        {
            CreateRelease(server, prj, release);
        }

        for (var i = 0; i < project.MergeRequests.Count; i++)
        {
            var mergeRequest = project.MergeRequests[i];
            var maxCreatedAt = project.MergeRequests
                .Skip(i + 1)
                .SelectMany(x => new[] { x.CreatedAt ?? default, x.UpdatedAt ?? default, x.MergedAt ?? default, x.ClosedAt ?? default }
                    .Concat(x.Comments.SelectMany(c => new[] { c.CreatedAt ?? default, c.UpdatedAt ?? default })))
                .Where(x => x != default)
                .DefaultIfEmpty(DateTime.UtcNow).Min();

            CreateMergeRequest(server, prj, mergeRequest, maxCreatedAt);
        }

        foreach (var permission in project.Permissions)
        {
            CreatePermission(server, prj, permission);
        }

        foreach (var pipeline in project.Pipelines)
        {
            CreatePipeline(server, prj, pipeline, aliases);
        }

        if (!string.IsNullOrEmpty(project.ClonePath))
        {
            var folderPath = Path.GetDirectoryName(Path.GetFullPath(project.ClonePath));
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath!);

            // libgit2sharp cannot clone with an other folder name
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"-c protocol.file.allow=always clone {project.CloneParameters} \"{prj.SshUrl}\" \"{Path.GetFileName(project.ClonePath)}\" --recursive",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = folderPath,
                },
            };

            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new GitLabException($"Cannot clone '{prj.PathWithNamespace}' in '{project.ClonePath}': {error}");
            }
        }
    }

    private static void CreateRelease(GitLabServer server, Project project, GitLabReleaseInfo gitLabRelease)
    {
        var user = GetOrCreateUser(server, gitLabRelease.Author);
        var release = new ReleaseInfo
        {
            Author = new UserRef(user),
            TagName = gitLabRelease.TagName,
            CreatedAt = gitLabRelease.CreatedAt,
            ReleasedAt = gitLabRelease.ReleasedAt,
        };

        project.Releases.Add(release);
    }

    private static Commit CreateCommit(GitLabServer server, Project prj, GitLabCommit commit)
    {
        var username = commit.User ?? commit.Parent.Parent.DefaultUser ?? throw new InvalidOperationException("Default user is required when author not set");
        var user = GetOrCreateUser(server, username);
        var targetBranch = commit.TargetBranch;
        Commit cmt;
        if (!string.IsNullOrEmpty(commit.FromBranch))
        {
            prj.Repository.Checkout(commit.FromBranch);
        }

        if (string.IsNullOrEmpty(targetBranch))
        {
            var branchExists = string.IsNullOrEmpty(commit.SourceBranch) || prj.Repository.GetAllBranches().Any(x => string.Equals(x.FriendlyName, commit.SourceBranch, StringComparison.Ordinal));
            if (!branchExists)
                prj.Repository.CreateBranch(commit.SourceBranch);

            var files = commit.Files.Count == 0
                ? [File.CreateFromText("test.txt", Guid.NewGuid().ToString())]
                : commit.Files.Select(x => File.CreateFromText(x.Path, x.Content ?? string.Empty)).ToList();

            var submodules = CreateSubModules(server, prj, commit);

            cmt = prj.Repository.Commit(user, commit.Message ?? Guid.NewGuid().ToString("D"), commit.SourceBranch, files, submodules);
        }
        else
        {
            cmt = prj.Repository.Merge(user, commit.SourceBranch, targetBranch);
            if (commit.DeleteSourceBranch)
                prj.Repository.RemoveBranch(commit.SourceBranch);
        }

        foreach (var tag in commit.Tags)
        {
            prj.Repository.CreateTag(tag);
        }

        return cmt;
    }

    private static IEnumerable<string> CreateSubModules(GitLabServer server, Project prj, GitLabCommit commit)
    {
        List<string> submodules = [];
        foreach (var submodule in commit.SubModules)
        {
            var subModuleProject = server.AllProjects.FirstOrDefault(x =>
                x.Name.Equals(submodule.ProjectName, StringComparison.OrdinalIgnoreCase));
            if (subModuleProject is null)
            {
                throw new GitLabException($"Project {submodule.ProjectName} can't be found.");
            }

            if (!subModuleProject.Repository.GetCommits().Any())
            {
                throw new GitLabException("Project added as a module must have least one commit.");
            }

            using var process = Process.Start(
                new ProcessStartInfo("git", $"-c protocol.file.allow=always submodule add \"{subModuleProject.SshUrl}\" \"{subModuleProject.Name}\"")
                {
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = prj.Repository.FullPath,
                }) ?? throw new GitLabException("Unable to start 'git add' process.");

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new GitLabException($"Cannot add submodule: {error}");
            }

            submodules.Add(subModuleProject.Name);
        }

        return submodules;
    }

    private static void CreateLabel(Group group, GitLabLabel label)
    {
        group.Labels.Add(new Label
        {
            Name = label.Name ?? throw new ArgumentException(@"label.Name == null", nameof(label)),
            Color = label.Color ?? "#d9534f",
            Description = label.Description,
        });
    }

    private static void CreateLabel(Project project, GitLabLabel label)
    {
        project.Labels.Add(new Label
        {
            Name = label.Name ?? throw new ArgumentException(@"label.Name == null", nameof(label)),
            Color = label.Color ?? "#d9534f",
            Description = label.Description,
        });
    }

    private static void CreateLabel(Project project, string label)
    {
        var model = project.Labels.Concat(GetLabels(project.Group)).FirstOrDefault(x => string.Equals(x.Name, label, StringComparison.Ordinal));
        if (model == null)
            project.Labels.Add(label);

        static IEnumerable<Label> GetLabels(Group group)
        {
            return group.Parent != null ? group.Labels.Concat(GetLabels(group.Parent)) : group.Labels;
        }
    }

    private static void CreateIssue(GitLabServer server, Project project, GitLabIssue issue)
    {
        foreach (var label in issue.Labels)
        {
            CreateLabel(project, label);
        }

        var issueAuthor = issue.Author ?? issue.Parent.Parent.DefaultUser ?? throw new InvalidOperationException("Default user is required when author not set");
        var issueAssignee = issue.Assignee;
        var updatedAt = issue.UpdatedAt ?? issue.Comments
            .SelectMany(x => new[] { x.CreatedAt ?? default, x.UpdatedAt ?? default })
            .Where(x => x != default)
            .DefaultIfEmpty(DateTime.UtcNow)
            .Max();

        var iss = new Issue
        {
            Iid = issue.Id,
            Title = issue.Title ?? Guid.NewGuid().ToString("D"),
            Description = issue.Description,
            Labels = issue.Labels.ToArray(),
            Author = new UserRef(GetOrCreateUser(server, issueAuthor)),
            Assignees = string.IsNullOrEmpty(issueAssignee) ? Array.Empty<UserRef>() : issueAssignee.Split(',').Select(a => new UserRef(GetOrCreateUser(server, a.Trim()))).ToArray(),
            Milestone = string.IsNullOrEmpty(issue.Milestone) ? null : GetOrCreateMilestone(project, issue.Milestone),
            UpdatedAt = updatedAt.ToDateTimeOffsetAssumeUtc(),
            ClosedAt = issue.ClosedAt,
        };

        if (iss.ClosedAt != null && iss.UpdatedAt > iss.ClosedAt)
            iss.UpdatedAt = (DateTimeOffset)iss.ClosedAt;

        foreach (var comment in issue.Comments)
        {
            CreateComment(server, iss, comment);
        }

        project.Issues.Add(iss);
    }

    private static void CreateMergeRequest(GitLabServer server, Project project, GitLabMergeRequest mergeRequest, DateTime maxCreatedAt)
    {
        foreach (var label in mergeRequest.Labels)
        {
            CreateLabel(project, label);
        }

        var mergeRequestAuthor = mergeRequest.Author ?? mergeRequest.Parent.Parent.DefaultUser ?? throw new InvalidOperationException("Default user is required when author not set");
        var mergeRequestAssignee = mergeRequest.Assignee;
        var createdAt = mergeRequest.CreatedAt ?? mergeRequest.Comments
            .SelectMany(x => new[] { x.CreatedAt ?? default, x.UpdatedAt ?? default })
            .Where(x => x != default)
            .DefaultIfEmpty(maxCreatedAt)
            .Min();
        var updatedAt = mergeRequest.UpdatedAt ?? mergeRequest.Comments
            .SelectMany(x => new[] { x.CreatedAt ?? default, x.UpdatedAt ?? default })
            .Where(x => x != default)
            .DefaultIfEmpty(maxCreatedAt)
            .Max();

        var request = new MergeRequest
        {
            Iid = mergeRequest.Id,
            Title = mergeRequest.Title ?? Guid.NewGuid().ToString("D"),
            Description = mergeRequest.Description,
            Author = new UserRef(GetOrCreateUser(server, mergeRequestAuthor)),
            Assignees = string.IsNullOrEmpty(mergeRequestAssignee) ? Array.Empty<UserRef>() : mergeRequestAssignee.Split(',').Select(a => new UserRef(GetOrCreateUser(server, a.Trim()))).ToArray(),
            SourceBranch = mergeRequest.SourceBranch ?? Guid.NewGuid().ToString("D"),
            TargetBranch = mergeRequest.TargetBranch ?? server.DefaultBranchName,
            CreatedAt = createdAt.ToDateTimeOffsetAssumeUtc(),
            UpdatedAt = updatedAt.ToDateTimeOffsetAssumeUtc(),
            ClosedAt = mergeRequest.ClosedAt,
            MergedAt = mergeRequest.MergedAt,
            SourceProject = project,
            Milestone = string.IsNullOrEmpty(mergeRequest.Milestone) ? null : GetOrCreateMilestone(project, mergeRequest.Milestone),
        };

        var endedAt = request.MergedAt ?? request.ClosedAt;
        if (endedAt != null && mergeRequest.UpdatedAt == null && request.UpdatedAt > endedAt)
            request.UpdatedAt = (DateTimeOffset)endedAt;

        if (request.CreatedAt > request.UpdatedAt)
            request.CreatedAt = request.UpdatedAt;

        foreach (var label in mergeRequest.Labels)
        {
            request.Labels.Add(label);
        }

        foreach (var approver in mergeRequest.Approvers)
        {
            request.Approvers.Add(new UserRef(GetOrCreateUser(server, approver)));
        }

        for (var i = 0; i < mergeRequest.Comments.Count; i++)
        {
            var comment = mergeRequest.Comments[i];
            var maxCommentCreatedAt = mergeRequest.Comments
                .Skip(i + 1)
                .SelectMany(x => new[] { x.CreatedAt ?? default, x.UpdatedAt ?? default })
                .Where(x => x != default)
                .DefaultIfEmpty(updatedAt)
                .Min();

            CreateComment(server, request, comment, maxCommentCreatedAt.ToDateTimeOffsetAssumeUtc());
        }

        project.MergeRequests.Add(request);
    }

    private static void CreatePermission(GitLabServer server, Project project, GitLabPermission permission)
    {
        project.Permissions.Add(CreatePermission(server, permission));
    }

    private static void CreatePermission(GitLabServer server, Group group, GitLabPermission permission)
    {
        group.Permissions.Add(CreatePermission(server, permission));
    }

    private static Permission CreatePermission(GitLabServer server, GitLabPermission permission)
    {
        return string.IsNullOrEmpty(permission.User)
            ? new Permission(GetOrCreateGroup(server, permission.Group), permission.Level)
            : new Permission(GetOrCreateUser(server, permission.User), permission.Level);
    }

    private static void CreateMilestone(Group group, GitLabMilestone milestone)
    {
        var mlt = CreateMilestone(milestone);
        group.Milestones.Add(mlt);
    }

    private static void CreateMilestone(Project project, GitLabMilestone milestone)
    {
        var mlt = CreateMilestone(milestone);
        project.Milestones.Add(mlt);
    }

    private static Milestone CreateMilestone(GitLabMilestone milestone)
    {
        var mlt = new Milestone
        {
            Id = milestone.Id,
            Title = milestone.Title,
            Description = milestone.Description,
            DueDate = milestone.DueDate ?? DateTimeOffset.UtcNow,
            StartDate = milestone.StartDate ?? DateTimeOffset.UtcNow,
            UpdatedAt = milestone.UpdatedAt ?? DateTimeOffset.UtcNow,
            ClosedAt = milestone.ClosedAt,
        };

        if (mlt.ClosedAt != null && mlt.UpdatedAt > mlt.ClosedAt)
        {
            mlt.UpdatedAt = (DateTimeOffset)mlt.ClosedAt;
        }

        return mlt;
    }

    private static void CreateComment(GitLabServer server, Issue issue, GitLabComment comment)
    {
        var commentAuthor = comment.Author ?? ((GitLabIssue)comment.ParentObject).Parent.Parent.DefaultUser ?? throw new InvalidOperationException("Default user is required when author not set");
        var cmt = new ProjectIssueNote
        {
            Id = comment.Id,
            Author = new UserRef(GetOrCreateUser(server, commentAuthor)),
            Body = comment.Message ?? Guid.NewGuid().ToString("D"),
            System = comment.System,
            CreatedAt = comment.CreatedAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = comment.UpdatedAt ?? comment.CreatedAt ?? DateTimeOffset.UtcNow,
            ThreadId = comment.Thread,
            Resolvable = comment.Resolvable,
            Resolved = comment.Resolved,
        };
        issue.Notes.Add(cmt);

        if (cmt.CreatedAt > cmt.UpdatedAt)
            cmt.CreatedAt = cmt.UpdatedAt;
    }

    private static void CreateComment(GitLabServer server, MergeRequest mergeRequest, GitLabComment comment, DateTimeOffset maxCreatedAt)
    {
        var commentAuthor = comment.Author ?? ((GitLabMergeRequest)comment.ParentObject).Parent.Parent.DefaultUser ?? throw new InvalidOperationException("Default user is required when author not set");
        var cmt = new MergeRequestComment
        {
            Id = comment.Id,
            Author = new UserRef(GetOrCreateUser(server, commentAuthor)),
            Body = comment.Message ?? Guid.NewGuid().ToString("D"),
            System = comment.System,
            CreatedAt = comment.CreatedAt ?? maxCreatedAt,
            UpdatedAt = comment.UpdatedAt ?? comment.CreatedAt ?? maxCreatedAt,
            ThreadId = comment.Thread,
            Resolvable = comment.Resolvable,
            Resolved = comment.Resolved,
        };
        mergeRequest.Comments.Add(cmt);

        if (cmt.CreatedAt > cmt.UpdatedAt)
            cmt.CreatedAt = cmt.UpdatedAt;
    }

    private static Pipeline CreatePipeline(GitLabServer server, Mock.GitLabObject parent, GitLabPipeline pipeline, Dictionary<string, Commit> aliases)
    {
        if (!aliases.TryGetValue(pipeline.Commit ?? throw new ArgumentException("pipeline.Commit == null", nameof(pipeline)), out var commit))
            throw new InvalidOperationException($"Cannot find commit from alias '{pipeline.Commit}'");

        var ppl = new Pipeline(commit.Sha)
        {
            Id = pipeline.Id,
            User = server.Users.First(x => string.Equals(x.Email, commit.Author.Email, StringComparison.Ordinal)),
            CommittedAt = commit.Author.When,
            Status = pipeline.Jobs.Count == 0 ? JobStatus.NoBuild : JobStatus.Success,
        };

        if (parent is Project project)
        {
            project.Pipelines.Add(ppl);
        }
        else if (parent is Pipeline parentPipeline)
        {
            ppl.IsDownStreamPipeline = true;
            parentPipeline.Project.Pipelines.Add(ppl);
        }

        var jobs = new List<Job>();
        for (var i = 0; i < pipeline.Jobs.Count; i++)
        {
            var job = pipeline.Jobs[i];
            var maxCreatedAt = string.IsNullOrEmpty(job.Name)
                ? DateTime.UtcNow
                : pipeline.Jobs
                    .Skip(i + 1)
                    .Where(x => string.Equals(x.Name, job.Name, StringComparison.Ordinal) && string.Equals(x.Stage, job.Stage, StringComparison.Ordinal))
                    .SelectMany(x => new[] { x.CreatedAt ?? default, x.StartedAt ?? default, x.FinishedAt ?? default })
                    .Where(x => x != default)
                    .DefaultIfEmpty(DateTime.UtcNow)
                    .Min();

            jobs.Add(CreateJob(server, ppl, job, maxCreatedAt, aliases));
        }

        var statuses = jobs
            .GroupBy(x => $"{x.Stage}:{x.Name}", StringComparer.Ordinal)
            .Select(x => x.OrderByDescending(j => j.CreatedAt).First().Status)
            .ToList();
        if (statuses.Contains(JobStatus.Created) || statuses.Contains(JobStatus.Running) || statuses.Contains(JobStatus.Pending))
            ppl.Status = JobStatus.Running;
        else if (statuses.Contains(JobStatus.Canceled))
            ppl.Status = JobStatus.Canceled;
        else if (statuses.Contains(JobStatus.Failed))
            ppl.Status = JobStatus.Failed;

        ppl.CreatedAt = jobs.Select(x => x.CreatedAt).DefaultIfEmpty(DateTime.UtcNow).Min().ToDateTimeOffsetAssumeUtc();
        var dateTimes = jobs.Where(x => x.StartedAt != default).Select(x => x.StartedAt).ToArray();
        ppl.StartedAt = dateTimes.Length == 0 ? null : dateTimes.Min().ToDateTimeOffsetAssumeUtc();
        ppl.FinishedAt = jobs.Exists(x => x.Status is JobStatus.Created or JobStatus.Pending or JobStatus.Preparing or JobStatus.WaitingForResource or JobStatus.Running)
            ? null
            : jobs.Where(x => x.FinishedAt != default).Select(x => (DateTimeOffset)x.FinishedAt).DefaultIfEmpty(ppl.CreatedAt).Max();

        if (ppl.FinishedAt != null && (ppl.StartedAt == null || ppl.StartedAt > ppl.FinishedAt))
            ppl.StartedAt = ppl.FinishedAt;

        if (ppl.StartedAt != null && ppl.CreatedAt > ppl.StartedAt)
            ppl.CreatedAt = (DateTimeOffset)ppl.StartedAt;

        return ppl;
    }

    private static Job CreateJob(GitLabServer server, Pipeline pipeline, GitLabJob job, DateTime maxCreatedAt, Dictionary<string, Commit> aliases)
    {
        var jb = new Job
        {
            Id = job.Id,
            Name = job.Name ?? Guid.NewGuid().ToString("D"),
            Stage = job.Stage ?? "build",
            Status = job.Status == JobStatus.Unknown ? JobStatus.Manual : job.Status,
            CreatedAt = job.CreatedAt ?? maxCreatedAt,
            StartedAt = job.StartedAt ?? (job.Status is JobStatus.Success or JobStatus.Failed or JobStatus.Canceled or JobStatus.Running ? maxCreatedAt : default),
            FinishedAt = job.FinishedAt ?? (job.Status is JobStatus.Success or JobStatus.Failed or JobStatus.Canceled ? maxCreatedAt : default),
            AllowFailure = job.AllowFailure,
            TagList = job.TagList ?? Array.Empty<string>(),
            User = pipeline.User,
            DownstreamPipeline = job.DownstreamPipeline == null ? null : CreatePipeline(server, pipeline, job.DownstreamPipeline, aliases),
        };

        if (pipeline.Parent is Pipeline ppl)
        {
            ppl.AddJob(jb);
        }
        else
        {
            jb.Pipeline = pipeline;
            pipeline.Project.Jobs.Add(jb);
        }

        if (jb.FinishedAt != default && (jb.StartedAt == default || jb.StartedAt > jb.FinishedAt))
            jb.StartedAt = jb.FinishedAt;

        if (jb.StartedAt != default && jb.CreatedAt > jb.StartedAt)
            jb.CreatedAt = jb.StartedAt;

        return jb;
    }

    private static User GetOrCreateUser(GitLabServer server, string username)
    {
        return server.Users.FirstOrDefault(x => string.Equals(x.UserName, username, StringComparison.Ordinal)) ?? server.Users.AddNew(username);
    }

    private static Milestone GetOrCreateMilestone(Project project, string title)
    {
        var milestone = project.Milestones.FirstOrDefault(x => string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase));

        var group = project.Group;
        while (milestone == null && group != null)
        {
            milestone = group.Milestones.FirstOrDefault(x => string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase));
            group = group.Parent;
        }

        if (milestone == null)
        {
            milestone = new Milestone
            {
                Title = title,
            };

            project.Milestones.Add(milestone);
        }

        return milestone;
    }

    private static Group GetOrCreateGroup(GitLabServer server, string @namespace)
    {
        var names = @namespace.Split('/');
        var group = server.Users.FirstOrDefault(x => string.Equals(x.UserName, names[0], StringComparison.Ordinal))?.Namespace ??
                    server.Groups.FirstOrDefault(x => string.Equals(x.Name, names[0], StringComparison.Ordinal));

        if (group == null)
            return GetOrCreateGroup(server.Groups, names.ToArray());

        return names.Length > 1 ? GetOrCreateGroup(group.Groups, names.Skip(1).ToArray()) : group;
    }

    private static Group GetOrCreateGroup(GroupCollection parent, IReadOnlyList<string> names)
    {
        var group = parent.FirstOrDefault(x => string.Equals(x.Name, names[0], StringComparison.Ordinal));
        if (group == null)
        {
            group = new Group(names[0])
            {
                Visibility = VisibilityLevel.Internal,
            };

            parent.Add(group);
        }

        return names.Count > 1 ? GetOrCreateGroup(group.Groups, names.Skip(1).ToArray()) : group;
    }

    private static GitLabUser ToConfig(User user)
    {
        return new GitLabUser
        {
            Id = user.Id,
            Username = user.UserName,
            Name = user.Name,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            IsAdmin = user.IsAdmin,
        };
    }

    private static GitLabGroup ToConfig(Group group)
    {
        var grp = new GitLabGroup
        {
            Id = group.Id,
            Name = group.Name,
            Namespace = group.Parent == null ? null : GetNamespace(group.Parent),
            Description = group.Description,
            Visibility = group.Visibility,
        };

        foreach (var label in group.Labels)
        {
            grp.Labels.Add(ToConfig(label));
        }

        foreach (var permission in group.Permissions)
        {
            grp.Permissions.Add(ToConfig(permission));
        }

        return grp;
    }

    private static GitLabProject ToConfig(Project project)
    {
        var prj = new GitLabProject
        {
            Id = project.Id,
            Name = project.Name,
            Namespace = GetNamespace(project.Group),
            Description = project.Description,
            DefaultBranch = project.DefaultBranch,
            Visibility = project.Visibility,
        };

        foreach (var label in project.Labels)
        {
            prj.Labels.Add(ToConfig(label));
        }

        foreach (var commit in project.Repository.GetCommits())
        {
            prj.Commits.Add(ToConfig(commit));
        }

        foreach (var milestone in project.Milestones)
        {
            prj.Milestones.Add(ToConfig(milestone));
        }

        foreach (var issue in project.Issues)
        {
            prj.Issues.Add(ToConfig(issue));
        }

        foreach (var mergeRequest in project.MergeRequests)
        {
            prj.MergeRequests.Add(ToConfig(mergeRequest));
        }

        foreach (var permission in project.Permissions)
        {
            prj.Permissions.Add(ToConfig(permission));
        }

        foreach (var pipeline in project.Pipelines)
        {
            prj.Pipelines.Add(ToConfig(pipeline));
        }

        return prj;
    }

    private static GitLabLabel ToConfig(Label label)
    {
        return new GitLabLabel
        {
            Name = label.Name,
            Color = label.Color,
            Description = label.Description,
        };
    }

    private static GitLabCommit ToConfig(Commit commit)
    {
        return new GitLabCommit
        {
            User = commit.Author.Name,
            Message = commit.Message,
            Alias = commit.Sha,
        };
    }

    private static GitLabPermission ToConfig(Permission permission)
    {
        return new GitLabPermission
        {
            User = permission.User?.UserName,
            Group = permission.Group == null ? null : GetNamespace(permission.Group),
            Level = permission.AccessLevel,
        };
    }

    private static GitLabMilestone ToConfig(Milestone milestone)
    {
        return new GitLabMilestone
        {
            Id = milestone.Id,
            Title = milestone.Title,
            Description = milestone.Description,
            DueDate = milestone.DueDate.Date,
            StartDate = milestone.StartDate.Date,
            CreatedAt = milestone.CreatedAt.Date,
            UpdatedAt = milestone.UpdatedAt.Date,
            ClosedAt = milestone.ClosedAt?.Date,
        };
    }

    private static GitLabIssue ToConfig(Issue issue)
    {
        var iss = new GitLabIssue
        {
            Id = issue.Iid,
            Title = issue.Title,
            Description = issue.Description,
            Author = issue.Author?.UserName ?? throw new InvalidOperationException($"Author required in issue '{issue.Title}'"),
            Assignee = issue.Assignee?.UserName,
            CreatedAt = issue.CreatedAt.UtcDateTime,
            UpdatedAt = issue.UpdatedAt.UtcDateTime,
            ClosedAt = issue.ClosedAt?.UtcDateTime,
        };

        foreach (var label in issue.Labels ?? Enumerable.Empty<string>())
        {
            iss.Labels.Add(label);
        }

        foreach (var comment in issue.Notes ?? Enumerable.Empty<ProjectIssueNote>())
        {
            iss.Comments.Add(ToConfig(comment));
        }

        return iss;
    }

    private static GitLabMergeRequest ToConfig(MergeRequest mergeRequest)
    {
        var mrg = new GitLabMergeRequest
        {
            Id = mergeRequest.Iid,
            Title = mergeRequest.Title,
            Description = mergeRequest.Description,
            Author = mergeRequest.Author?.UserName ?? throw new InvalidOperationException($"Author required in merge request '{mergeRequest.Title}'"),
            Assignee = mergeRequest.Assignee?.UserName,
            SourceBranch = mergeRequest.SourceBranch,
            TargetBranch = mergeRequest.TargetBranch,
            CreatedAt = mergeRequest.CreatedAt.UtcDateTime,
            UpdatedAt = mergeRequest.UpdatedAt.UtcDateTime,
            ClosedAt = mergeRequest.ClosedAt?.UtcDateTime,
            MergedAt = mergeRequest.MergedAt?.UtcDateTime,
        };

        foreach (var label in mergeRequest.Labels)
        {
            mrg.Labels.Add(label);
        }

        foreach (var approver in mergeRequest.Approvers)
        {
            mrg.Approvers.Add(approver.UserName);
        }

        foreach (var comment in mergeRequest.Comments)
        {
            mrg.Comments.Add(ToConfig(comment));
        }

        return mrg;
    }

    private static GitLabPipeline ToConfig(Pipeline pipeline)
    {
        var ppl = new GitLabPipeline
        {
            Id = pipeline.Id,
            Commit = pipeline.Ref,
        };

        foreach (var job in pipeline.Project.Jobs.Where(x => x.Pipeline.Id == pipeline.Id))
        {
            ppl.Jobs.Add(ToConfig(job));
        }

        return ppl;
    }

    private static GitLabJob ToConfig(Job job)
    {
        return new GitLabJob
        {
            Id = job.Id,
            Name = job.Name,
            Stage = job.Stage,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt == default ? null : job.StartedAt,
            FinishedAt = job.FinishedAt == default ? null : job.FinishedAt,
            AllowFailure = job.AllowFailure,
            TagList = job.TagList,
        };
    }

    private static GitLabComment ToConfig(ProjectIssueNote comment)
    {
        return new GitLabComment
        {
            Id = (int)comment.Id,
            Author = comment.Author?.UserName,
            Message = comment.Body,
            System = comment.System,
            CreatedAt = comment.CreatedAt.UtcDateTime,
            UpdatedAt = comment.UpdatedAt.UtcDateTime,
            Thread = comment.ThreadId,
            Resolvable = comment.Resolvable,
            Resolved = comment.Resolved,
        };
    }

    private static GitLabComment ToConfig(MergeRequestComment comment)
    {
        return new GitLabComment
        {
            Id = (int)comment.Id,
            Author = comment.Author?.UserName,
            Message = comment.Body,
            System = comment.System,
            CreatedAt = comment.CreatedAt.UtcDateTime,
            UpdatedAt = comment.UpdatedAt.UtcDateTime,
            Thread = comment.ThreadId,
            Resolvable = comment.Resolvable,
            Resolved = comment.Resolved,
        };
    }

    private static string GetNamespace(Group group)
    {
        return group.Parent == null ? group.Name : $"{GetNamespace(group.Parent)}/{group.Name}";
    }
}
