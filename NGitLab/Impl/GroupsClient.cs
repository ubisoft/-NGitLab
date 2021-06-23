﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NGitLab.Models;

namespace NGitLab.Impl
{
    /// <summary>
    /// https://docs.gitlab.com/ce/api/groups.html
    /// </summary>
    public class GroupsClient : IGroupsClient
    {
        private readonly API _api;

        public const string Url = "/groups";

        public GroupsClient(API api)
        {
            _api = api;
        }

        public IEnumerable<Group> Accessible => _api.Get().GetAll<Group>(Utils.AddOrderBy(Url));

        public IEnumerable<Group> Get(GroupQuery query)
        {
            var url = Group.Url;

            if (query.SkipGroups != null && query.SkipGroups.Any())
            {
                foreach (var skipGroup in query.SkipGroups)
                {
                    url = Utils.AddParameter(url, "skip_groups[]", skipGroup);
                }
            }

            if (query.AllAvailable != null)
            {
                url = Utils.AddParameter(url, "all_available", query.AllAvailable);
            }

            if (!string.IsNullOrEmpty(query.Search))
            {
                url = Utils.AddParameter(url, "search", query.Search);
            }

            url = Utils.AddOrderBy(url, query.OrderBy);

            if (query.Sort != null)
            {
                url = Utils.AddParameter(url, "sort", query.Sort);
            }

            if (query.Statistics != null)
            {
                url = Utils.AddParameter(url, "statistics", query.Statistics);
            }

            if (query.WithCustomAttributes != null)
            {
                url = Utils.AddParameter(url, "with_custom_attributes", query.WithCustomAttributes);
            }

            if (query.Owned != null)
            {
                url = Utils.AddParameter(url, "owned", query.Owned);
            }

            if (query.MinAccessLevel != null)
            {
                url = Utils.AddParameter(url, "min_access_level", (int)query.MinAccessLevel);
            }

            return _api.Get().GetAll<Group>(url);
        }

        public IEnumerable<Group> Search(string search)
        {
            return _api.Get().GetAll<Group>(Utils.AddOrderBy(Url + $"?search={Uri.EscapeDataString(search)}"));
        }

        public Group this[int id] => _api.Get().To<Group>(Url + "/" + Uri.EscapeDataString(id.ToString(CultureInfo.InvariantCulture)));

        public Group this[string fullPath] => _api.Get().To<Group>(Url + "/" + Uri.EscapeDataString(fullPath));

        public IEnumerable<Project> SearchProjects(int groupId, string search)
        {
            var url = Url + "/" + Uri.EscapeDataString(groupId.ToString(CultureInfo.InvariantCulture)) + "/projects";
            url = Utils.AddParameter(url, "search", search);
            url = Utils.AddOrderBy(url);
            return _api.Get().GetAll<Project>(url);
        }

        public Group Create(GroupCreate group) => _api.Post().With(group).To<Group>(Url);

        public void Delete(int id)
        {
            _api.Delete().Execute(Url + "/" + Uri.EscapeDataString(id.ToString(CultureInfo.InvariantCulture)));
        }

        public Group Update(int id, GroupUpdate groupUpdate) => _api.Put().With(groupUpdate).To<Group>(Url + "/" + Uri.EscapeDataString(id.ToString(CultureInfo.InvariantCulture)));

        public void Restore(int id) => _api.Post().Execute(Url + "/" + Uri.EscapeDataString(id.ToString(CultureInfo.InvariantCulture)) + "/restore");
    }
}
