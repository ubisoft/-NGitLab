﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NGitLab.Mock
{
    public sealed class UserCollection : Collection<User>
    {
        public UserCollection(GitLabObject container)
            : base(container)
        {
        }

        public User GetById(string id)
        {
            if (int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                return GetById(value);

            return null;
        }

        public User GetById(int id) => this.FirstOrDefault(user => user.Id == id);

        public User AddNew(string name = null)
        {
            var userName = name ?? "user" + Guid.NewGuid().ToString("N");
            return Add(userName);
        }

        public User Add(string userName)
        {
            var user = new User(userName);
            Add(user);
            return user;
        }

        public override void Add(User user)
        {
            if (user is null)
                throw new ArgumentNullException(nameof(user));

            if (user.Id == default)
            {
                user.Id = GetNewId();
            }
            else if (GetById(user.Id) != null)
            {
                throw new GitLabException("User already exists");
            }

            Server.Groups.Add(new Group(user));

            base.Add(user);
        }

        private int GetNewId()
        {
            return this.Select(user => user.Id).DefaultIfEmpty().Max() + 1;
        }

        internal IEnumerable<User> SearchByUsername(string username)
        {
            return this.Where(user => string.Equals(user.UserName, username, StringComparison.OrdinalIgnoreCase));
        }

        internal IEnumerable<User> Get(UserQuery query)
        {
            var users = this.AsQueryable();

            if (query.IsActive == true)
            {
                users = users.Where(u => u.State == UserState.active);
            }

            if (query.IsBlocked == true)
            {
                users = users.Where(u => u.State == UserState.blocked);
            }

            if (query.IsExternal == true)
            {
                users = users.Where(u => u.Identities.Any(i => !string.IsNullOrEmpty(i.ExternUid)));
            }

            if (query.ExcludeExternal == true)
            {
                users = users.Where(u => !u.Identities.Any() || u.Identities.All(i => string.IsNullOrEmpty(i.ExternUid)));
            }

            if (!string.IsNullOrEmpty(query.Search))
            {
                users = users.Where(u => u.UserName.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                                         u.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase) ||
                                         u.Email.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(query.Username))
            {
                users = users.Where(u => string.Equals(u.UserName, query.Search, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(query.OrderBy))
            {
                var sortAsc = !string.IsNullOrEmpty(query.Sort) && string.Equals(query.Sort, "asc", StringComparison.Ordinal);

                users = query.OrderBy switch
                {
                    "id" => sortAsc ? users.OrderBy(u => u.Id) : users.OrderByDescending(u => u.Id),
                    "name" => sortAsc ? users.OrderBy(u => u.Name) : users.OrderByDescending(u => u.Name),
                    "username" => sortAsc ? users.OrderBy(u => u.UserName) : users.OrderByDescending(u => u.UserName),
                    "created_at" => sortAsc ? users.OrderBy(u => u.CreatedAt) : users.OrderByDescending(u => u.CreatedAt),
                    _ => throw new InvalidOperationException($"Ordering by '{query.OrderBy}' is not supported"),
                };
            }

            return users;
        }
    }
}
