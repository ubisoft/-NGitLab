﻿using System;
using System.Net;
using NGitLab.Extensions;
using NGitLab.Models;

namespace NGitLab.Impl
{
    public class CommitClient : ICommitClient
    {
        private readonly API _api;
        private readonly string _repoPath;

        public CommitClient(API api, int projectId)
        {
            _api = api;

            var projectPath = Project.Url + "/" + projectId.ToStringInvariant();
            _repoPath = projectPath + "/repository";
        }

        public Commit GetCommit(string @ref)
        {
            return _api.Get().To<Commit>(_repoPath + $"/commits/{@ref}");
        }

        public JobStatus GetJobStatus(string branchName)
        {
            var encodedBranch = WebUtility.UrlEncode(branchName);

            var latestCommit = _api.Get().To<Commit>(_repoPath + $"/commits/{encodedBranch}?per_page=1");
            if (latestCommit == null)
            {
                return JobStatus.Unknown;
            }

            if (string.IsNullOrEmpty(latestCommit.Status))
            {
                return JobStatus.NoBuild;
            }

            if (!Enum.TryParse(latestCommit.Status, ignoreCase: true, result: out JobStatus result))
            {
                throw new NotSupportedException($"Status {latestCommit.Status} is unrecognised");
            }

            return result;
        }

        public Commit Create(CommitCreate commit) => _api.Post().With(commit).To<Commit>(_repoPath + "/commits");
    }
}
