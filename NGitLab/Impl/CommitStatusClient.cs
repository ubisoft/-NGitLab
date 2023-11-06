﻿using System;
using System.Collections.Generic;
using NGitLab.Models;

namespace NGitLab.Impl
{
    public class CommitStatusClient : ICommitStatusClient
    {
        private readonly API _api;
        private readonly string _statusCreatePath;
        private readonly string _statusPath;

        [Obsolete("Use long or namespaced path string as projectId instead.")]
        public CommitStatusClient(API api, int projectId)
            : this(api, (long)projectId)
        {
        }

        public CommitStatusClient(API api, ProjectId projectId)
        {
            _api = api;

            var projectPath = $"{Project.Url}/{projectId.ValueAsUriParameter()}";
            _statusCreatePath = $"{projectPath}/statuses";
            _statusPath = $"{projectPath}/repository/commits";
        }

        public IEnumerable<CommitStatus> AllBySha(string commitSha) => _api.Get().GetAll<CommitStatus>($"{_statusPath}/{commitSha}/statuses");

        public CommitStatusCreate AddOrUpdate(CommitStatusCreate status) => _api.Post().With(status).To<CommitStatusCreate>($"{_statusCreatePath}/{status.CommitSha}");
    }
}
