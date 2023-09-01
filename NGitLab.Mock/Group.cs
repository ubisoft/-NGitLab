﻿using System;
using System.Collections.Generic;
using System.Linq;
using NGitLab.Models;

namespace NGitLab.Mock
{
    public sealed class Group : GitLabObject
    {
        private string _path;
        private string _name;

        public Group()
            : this(Guid.NewGuid().ToString("N"))
        {
        }

        public Group(string name)
        {
            Groups = new GroupCollection(this);
            Projects = new ProjectCollection(this);
            Permissions = new PermissionCollection(this);
            Badges = new BadgeCollection(this);
            Labels = new LabelsCollection(this);
            Milestones = new MilestoneCollection(this);
            Name = name;
        }

        public Group(User user)
            : this(user.Name)
        {
            Path = user.UserName;
            Permissions.Add(new Permission(user, AccessLevel.Owner));
            IsUserNamespace = true;
        }

        public int Id { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                if (IsUserNamespace)
                    throw new InvalidOperationException("Cannot change the name of a user namespace");

                _name = value;
            }
        }

        public string Description { get; set; }

        public VisibilityLevel Visibility { get; set; }

        public bool IsUserNamespace { get; }

        public TimeSpan ExtraSharedRunnersLimit { get; set; }

        public TimeSpan SharedRunnersLimit { get; set; }

        public bool LfsEnabled { get; set; }

        public bool RequestAccessEnabled { get; set; }

        public new Group Parent => base.Parent as Group;

        public GroupCollection Groups { get; }

        public ProjectCollection Projects { get; }

        public PermissionCollection Permissions { get; }

        public BadgeCollection Badges { get; }

        public LabelsCollection Labels { get; }

        public MilestoneCollection Milestones { get; }

        public IEnumerable<MergeRequest> MergeRequests => AllProjects.SelectMany(project => project.MergeRequests);

        public string Path
        {
            get
            {
                _path ??= Slug.Create(Name);

                return _path;
            }

            set
            {
                if (IsUserNamespace)
                    throw new InvalidOperationException("Cannot change the name of a user namespace");

                _path = value;
            }
        }

        public string PathWithNameSpace => Parent == null ? Path : (Parent.PathWithNameSpace + "/" + Path);

        public string FullName => Parent == null ? Name : (Parent.FullName + "/" + Name);

        public IEnumerable<Group> DescendantGroups
        {
            get
            {
                foreach (var group in Groups)
                {
                    yield return group;
                    foreach (var subGroup in group.DescendantGroups)
                    {
                        yield return subGroup;
                    }
                }
            }
        }

        public IEnumerable<Project> AllProjects => Projects.Concat(DescendantGroups.SelectMany(group => group.Projects));

        public EffectivePermissions GetEffectivePermissions() => GetEffectivePermissions(includeInheritedPermissions: true);

        public EffectivePermissions GetEffectivePermissions(bool includeInheritedPermissions)
        {
            var result = new Dictionary<User, AccessLevel>();

            if (Parent != null && includeInheritedPermissions)
            {
                foreach (var effectivePermission in Parent.GetEffectivePermissions(includeInheritedPermissions).Permissions)
                {
                    Add(effectivePermission.User, effectivePermission.AccessLevel);
                }
            }

            foreach (var permission in Permissions)
            {
                if (permission.User != null)
                {
                    Add(permission.User, permission.AccessLevel);
                }
                else
                {
                    foreach (var effectivePermission in permission.Group.GetEffectivePermissions(includeInheritedPermissions).Permissions)
                    {
                        Add(effectivePermission.User, effectivePermission.AccessLevel);
                    }
                }
            }

            return new EffectivePermissions(result.Select(kvp => new EffectiveUserPermission(kvp.Key, kvp.Value)).ToList());

            void Add(User user, AccessLevel accessLevel)
            {
                if (result.TryGetValue(user, out var existingAccessLevel))
                {
                    if (accessLevel > existingAccessLevel)
                    {
                        result[user] = accessLevel;
                    }
                }
                else
                {
                    result[user] = accessLevel;
                }
            }
        }

        public bool IsUserOwner(User user)
        {
            var accessLevel = GetEffectivePermissions().GetAccessLevel(user);
            return accessLevel >= AccessLevel.Owner;
        }

        public bool CanUserViewGroup(User user)
        {
            if (Visibility == VisibilityLevel.Public)
                return true;

            if (Visibility == VisibilityLevel.Internal && user != null)
                return true;

            if (user == null)
                return false;

            if (user.IsAdmin)
                return true;

            var accessLevel = GetEffectivePermissions().GetAccessLevel(user);
            if (accessLevel.HasValue)
                return true;

            return false;
        }

        public bool CanUserDeleteGroup(User user)
        {
            if (user == null)
                return false;

            if (user.IsAdmin)
                return true;

            var accessLevel = GetEffectivePermissions().GetAccessLevel(user);
            return accessLevel.HasValue && accessLevel.Value == AccessLevel.Owner;
        }

        public bool CanUserEditGroup(User user)
        {
            if (user == null)
                return false;

            if (user.IsAdmin)
                return true;

            var accessLevel = GetEffectivePermissions().GetAccessLevel(user);
            return accessLevel.HasValue && accessLevel.Value >= AccessLevel.Maintainer;
        }

        public bool CanUserAddGroup(User user)
        {
            if (user == null)
                return false;

            if (user.IsAdmin)
                return true;

            var accessLevel = GetEffectivePermissions().GetAccessLevel(user);
            return accessLevel.HasValue && accessLevel.Value >= AccessLevel.Developer;
        }

        public bool CanUserAddProject(User user)
        {
            if (user == null)
                return false;

            if (user.IsAdmin)
                return true;

            var accessLevel = GetEffectivePermissions().GetAccessLevel(user);
            return accessLevel.HasValue && accessLevel.Value >= AccessLevel.Developer;
        }

        public Models.Group ToClientGroup(User currentUser)
        {
            return new Models.Group
            {
                Id = Id,
                Name = Name,
                Visibility = Visibility,
                ParentId = Parent?.Id,
                Projects = Projects.Select(p => p.ToClientProject(currentUser)).ToArray(),
                FullName = FullName,
                FullPath = PathWithNameSpace,
                Path = Path,
                Description = Description,
                RequestAccessEnabled = RequestAccessEnabled,
                LfsEnabled = LfsEnabled,
                ExtraSharedRunnersMinutesLimit = (int)ExtraSharedRunnersLimit.TotalMinutes,
                SharedRunnersMinutesLimit = (int)SharedRunnersLimit.TotalMinutes,
            };
        }

        /// <summary>
        /// https://docs.gitlab.com/ee/user/group/settings/group_access_tokens.html#bot-users-for-groups
        /// </summary>
        /// <param name="accessLevel">AccessLevel to give to the bot user</param>
        /// <returns>Bot user that have been added to the group</returns>
        public User CreateBotUser(AccessLevel accessLevel)
        {
            var botUsername = $"group_{Id}_bot_{Guid.NewGuid():D}";
            var bot = new User(botUsername)
            {
                Email = $"{botUsername}@noreply.example.com",
            };
            Permissions.Add(new Permission(bot, accessLevel));
            Server.Users.Add(bot);
            return bot;
        }
    }
}
