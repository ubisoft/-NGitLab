﻿using System.Collections.Generic;
using NGitLab.Extensions;
using NGitLab.Models;

namespace NGitLab.Impl
{
    public class ProjectLevelApprovalRulesClient : IProjectLevelApprovalRulesClient
    {
        private readonly API _api;
        private readonly int _projectId;
        private readonly string _approvalRulesUrl;

        public ProjectLevelApprovalRulesClient(API api, int projectId)
        {
            _api = api;
            _projectId = projectId;
            _approvalRulesUrl = $"{Project.Url}/{_projectId.ToStringInvariant()}/approval_rules";
        }

        public List<ApprovalRule> GetProjectLevelApprovalRules()
        {
            return _api.Get().To<List<ApprovalRule>>(_approvalRulesUrl);
        }

        public ApprovalRule UpdateProjectLevelApprovalRule(int approvalRuleIdToUpdate, ApprovalRuleUpdate approvalRuleUpdate)
        {
            return _api.Put().With(approvalRuleUpdate).To<ApprovalRule>(_approvalRulesUrl + $"/{approvalRuleIdToUpdate.ToStringInvariant()}");
        }

        public ApprovalRule CreateProjectLevelRule(ApprovalRuleCreate approvalRuleCreate)
        {
            return _api.Post().With(approvalRuleCreate).To<ApprovalRule>(_approvalRulesUrl);
        }

        public void DeleteProjectLevelRule(int approvalRuleIdToDelete)
        {
            _api.Delete().Execute(_approvalRulesUrl + $"/{approvalRuleIdToDelete.ToStringInvariant()}");
        }
    }
}
