﻿using System;
using System.Collections.Generic;
using NGitLab.Models;

namespace NGitLab.Impl
{
    public class ClusterClient : IClusterClient
    {
        private readonly API _api;
        private readonly string _environmentsPath;

        [Obsolete("Use long or namespaced path string as projectId instead.")]
        public ClusterClient(API api, int projectId)
            : this(api, (long)projectId)
        {
        }

        public ClusterClient(API api, ProjectId projectId)
        {
            _api = api;
            _environmentsPath = $"{Project.Url}/{projectId.ValueAsUriParameter()}/clusters";
        }

        public IEnumerable<ClusterInfo> All => _api.Get().GetAll<ClusterInfo>(_environmentsPath);
    }
}
