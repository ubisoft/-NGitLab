﻿using System.Collections.Generic;
using NGitLab.Extensions;
using NGitLab.Models;

namespace NGitLab.Impl
{
    public class ClusterClient : IClusterClient
    {
        private readonly API _api;
        private readonly string _environmentsPath;

        public ClusterClient(API api, int projectId)
        {
            _api = api;
            _environmentsPath = $"{Project.Url}/{projectId.ToStringInvariant()}/clusters";
        }

        public IEnumerable<ClusterInfo> All => _api.Get().GetAll<ClusterInfo>(_environmentsPath);
    }
}
