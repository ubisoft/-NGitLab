﻿using System;
using System.Collections.Generic;
using System.Linq;
using NGitLab.Models;

namespace NGitLab.Mock.Clients
{
    internal sealed class ProjectClient : ClientBase, IProjectClient
    {
        public ProjectClient(ClientContext context)
            : base(context)
        {
        }

        public Models.Project this[int id]
        {
            get
            {
                using (Context.BeginOperationScope())
                {
                    var project = GetProject(id, ProjectPermission.View);
                    if (project == null || !project.CanUserViewProject(Context.User))
                        throw new GitLabNotFoundException();

                    return project.ToClientProject();
                }
            }
        }

        public Models.Project this[string fullName]
        {
            get
            {
                using (Context.BeginOperationScope())
                {
                    var project = GetProject(fullName, ProjectPermission.View);
                    return project.ToClientProject();
                }
            }
        }

        public IEnumerable<Models.Project> Accessible
        {
            get
            {
                using (Context.BeginOperationScope())
                {
                    return Server.AllProjects.Where(project => project.IsUserMember(Context.User)).Select(project => project.ToClientProject()).ToList();
                }
            }
        }

        public IEnumerable<Models.Project> Owned
        {
            get
            {
                using (Context.BeginOperationScope())
                {
                    return Server.AllProjects.Where(project => project.IsUserOwner(Context.User)).Select(project => project.ToClientProject()).ToList();
                }
            }
        }

        public IEnumerable<Models.Project> Visible
        {
            get
            {
                using (Context.BeginOperationScope())
                {
                    return Server.AllProjects.Where(project => project.CanUserViewProject(Context.User)).Select(project => project.ToClientProject()).ToList();
                }
            }
        }

        public Models.Project Create(ProjectCreate project)
        {
            using (Context.BeginOperationScope())
            {
                var parentGroup = GetParentGroup(project.NamespaceId);

                var newProject = new Project(project.Name)
                {
                    Description = project.Description,
                    Visibility = project.VisibilityLevel,
                    Permissions =
                    {
                        new Permission(Context.User, AccessLevel.Owner),
                    },
                };

                parentGroup.Projects.Add(newProject);
                return newProject.ToClientProject();
            }
        }

        private Group GetParentGroup(string namespaceId)
        {
            Group parentGroup;
            if (namespaceId != null)
            {
                parentGroup = Server.AllGroups.FindGroup(namespaceId);
                if (parentGroup == null || !parentGroup.CanUserViewGroup(Context.User))
                    throw new GitLabNotFoundException();

                if (!parentGroup.CanUserAddProject(Context.User))
                    throw new GitLabForbiddenException();
            }
            else
            {
                parentGroup = Context.User.Namespace;
            }

            return parentGroup;
        }

        public void Delete(int id)
        {
            using (Context.BeginOperationScope())
            {
                var project = GetProject(id, ProjectPermission.Delete);
                project.Remove();
            }
        }

        public void Archive(int id)
        {
            var project = GetProject(id, ProjectPermission.Edit);
            project.Archived = true;
        }

        public void Unarchive(int id)
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
                return newProject.ToClientProject();
            }
        }

        public IEnumerable<Models.Project> Get(ProjectQuery query)
        {
            throw new NotImplementedException();
        }

        public Models.Project GetById(int id, SingleProjectQuery query)
        {
            using (Context.BeginOperationScope())
            {
                return GetProject(id, ProjectPermission.View).ToClientProject();
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
                    || query.Simple != null
                    || query.Statistics != null
                    || query.Visibility != null)
                    throw new NotImplementedException();

                var idAsInt = long.Parse(id);
                var matches = Server.AllProjects.Where(project => project.ForkedFrom?.Id == idAsInt);
                matches = matches.Where(project => query.Owned == null || query.Owned == project.IsUserOwner(Context.User));

                return matches.Select(project => project.ToClientProject()).ToList();
            }
        }

        private bool IsQueryEqual<T>(T? query, T value)
            where T : struct
        {
            return query == null || Equals(query.Value, value);
        }

        public Dictionary<string, double> GetLanguages(string id)
        {
            throw new NotImplementedException();
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
                    project.BuildTimeout = TimeSpan.FromMinutes(projectUpdate.BuildTimeout.Value);
                }

                if (projectUpdate.LfsEnabled.HasValue)
                {
                    project.LfsEnabled = projectUpdate.LfsEnabled.Value;
                }

                return project.ToClientProject();
            }
        }

        public UploadedProjectFile UploadFile(string id, FormDataContent data)
        {
            throw new NotImplementedException();
        }
    }
}
