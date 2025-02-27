using System;
using System.Collections.Generic;
using System.Linq;
using NGitLab.Mock.Internals;
using NGitLab.Models;

namespace NGitLab.Mock.Clients;

internal sealed class BranchClient : ClientBase, IBranchClient
{
    private readonly long _projectId;

    public BranchClient(ClientContext context, long projectId)
        : base(context)
    {
        _projectId = projectId;
    }

    public IEnumerable<Branch> Search(string search)
    {
        Func<string, bool> filterBranch;
        switch (search)
        {
            case null:
            case "":
                filterBranch = _ => true;
                break;

            case not null when search[0] == '^' && search[search.Length - 1] == '$':
                search = search[1..^1];
                filterBranch = branch => branch.Equals(search, StringComparison.OrdinalIgnoreCase);
                break;

            case not null when search[0] == '^':
                search = search[1..];
                filterBranch = branch => branch.StartsWith(search, StringComparison.OrdinalIgnoreCase);
                break;

            case not null when search[search.Length - 1] == '$':
                search = search[..^1];
                filterBranch = branch => branch.EndsWith(search, StringComparison.OrdinalIgnoreCase);
                break;

            default:
                filterBranch = branch => branch.Contains(search, StringComparison.OrdinalIgnoreCase);
                break;
        }

        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.View);
            return project.Repository.GetAllBranches()
                .Where(branch => filterBranch(branch.FriendlyName))
                .Select(branch => branch.ToBranchClient(project));
        }
    }

    public GitLabCollectionResponse<Branch> SearchAsync(string search)
    {
        return GitLabCollectionResponse.Create(Search(search));
    }

    public Branch this[string name]
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                var project = GetProject(_projectId, ProjectPermission.View);
                var branch = project.Repository.GetBranch(name);
                if (branch == null)
                    throw GitLabException.NotFound();

                return branch.ToBranchClient(project);
            }
        }
    }

    public IEnumerable<Branch> All
    {
        get
        {
            using (Context.BeginOperationScope())
            {
                var project = GetProject(_projectId, ProjectPermission.View);
                return project.Repository.GetAllBranches().Select(branch => branch.ToBranchClient(project)).ToList();
            }
        }
    }

    public Branch Create(BranchCreate branch)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.Contribute);
            if (branch.Ref != null)
            {
                return project.Repository.CreateBranch(branch.Name, branch.Ref).ToBranchClient(project);
            }

            return project.Repository.CreateBranch(branch.Name).ToBranchClient(project);
        }
    }

    public void Delete(string name)
    {
        using (Context.BeginOperationScope())
        {
            var project = GetProject(_projectId, ProjectPermission.Contribute);
            project.Repository.RemoveBranch(name);
        }
    }

    public Branch Protect(string name)
    {
        throw new NotImplementedException();
    }

    public Branch Unprotect(string name)
    {
        throw new NotImplementedException();
    }
}
