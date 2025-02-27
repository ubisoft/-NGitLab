using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests;

public class GroupsTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_groups_is_not_empty()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        Assert.That(groupClient.Accessible, Is.Not.Empty);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_projects_are_set_in_a_group_by_id()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();
        var project = context.Client.Projects.Create(new ProjectCreate { Name = "test", NamespaceId = group.Id });

        group = groupClient[group.Id];
        Assert.That(group, Is.Not.Null);
        Assert.That(group.Projects, Is.Not.Empty);
        Assert.That(group.Projects[0].Id, Is.EqualTo(project.Id));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_group_by_fullpath()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        group = groupClient[group.FullPath];
        Assert.That(group, Is.Not.Null);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_group_created_on_date_is_now()
    {
        using var context = await GitLabTestContext.CreateAsync();

        var t1 = DateTime.UtcNow;
        var group = context.CreateGroup();
        var t2 = DateTime.UtcNow;

        Assert.That(group.CreatedAt, Is.GreaterThanOrEqualTo(t1));
        Assert.That(group.CreatedAt, Is.LessThanOrEqualTo(t2));

        var group2 = await context.Client.Groups.GetByIdAsync(group.Id);
        Assert.That(group2.CreatedAt, Is.EqualTo(group.CreatedAt));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_create_delete_group()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        // Search
        var searchedGroup = groupClient.Search(group.Name).Single();
        Assert.That(searchedGroup.Id, Is.EqualTo(group.Id));

        // Delete (operation is asynchronous so we have to retry until the project is deleted)
        // Group can be marked for deletion (https://docs.gitlab.com/ee/user/admin_area/settings/visibility_and_access_controls.html#default-deletion-adjourned-period-premium-only)
        groupClient.Delete(group.Id);
        await GitLabTestContext.RetryUntilAsync(() => TryGetGroup(groupClient, group.Id), group => group == null || group.MarkedForDeletionOn != null, TimeSpan.FromMinutes(2));
    }

    private static Group TryGetGroup(IGroupsClient groupClient, long groupId)
    {
        try
        {
            return groupClient[groupId];
        }
        catch (GitLabException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_nulls_does_not_throws()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        // Arrange
        var groupQueryNull = new GroupQuery();

        // Act & Assert
        Assert.That(groupClient.Get(groupQueryNull).Take(10).ToList(), Is.Not.Null);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_SkipGroups_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group1 = context.CreateGroup();
        var group2 = context.CreateGroup();
        var group3 = context.CreateGroup();

        // Arrange
        var skippedGroupIds = new[] { group2.Id };

        // Act
        var resultSkip = groupClient.Get(new GroupQuery { SkipGroups = skippedGroupIds }).ToList();

        // Assert
        foreach (var skippedGroup in skippedGroupIds)
        {
            Assert.That(resultSkip.Exists(group => group.Id == skippedGroup), Is.False, $"Group {skippedGroup} found in results");
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_Search_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group1 = context.CreateGroup();
        var group2 = context.CreateGroup();

        // Arrange
        var groupQueryNull = new GroupQuery
        {
            Search = group1.Name,
        };

        // Act
        var result = groupClient.Get(groupQueryNull).Count(g => string.Equals(g.Name, group1.Name, StringComparison.Ordinal));

        // Assert
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_AllAvailable_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        // Arrange
        var groupQueryAllAvailable = new GroupQuery
        {
            AllAvailable = true,
        };

        // Act
        var result = groupClient.Get(groupQueryAllAvailable);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_OrderBy_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        // Arrange
        var groupQueryOrderByName = new GroupQuery
        {
            OrderBy = "name",
        };
        var groupQueryOrderByPath = new GroupQuery
        {
            OrderBy = "path",
        };
        var groupQueryOrderById = new GroupQuery
        {
            OrderBy = "id",
        };

        // Act
        var resultByName = groupClient.Get(groupQueryOrderByName);
        var resultByPath = groupClient.Get(groupQueryOrderByPath);
        var resultById = groupClient.Get(groupQueryOrderById);

        // Assert
        Assert.That(resultByName.Any(), Is.True);
        Assert.That(resultByPath.Any(), Is.True);
        Assert.That(resultById.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_Sort_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        // Arrange
        var groupQueryAsc = new GroupQuery
        {
            Sort = "asc",
        };
        var groupQueryDesc = new GroupQuery
        {
            Sort = "desc",
        };

        // Act
        var resultAsc = groupClient.Get(groupQueryAsc);
        var resultDesc = groupClient.Get(groupQueryDesc);

        // Assert
        Assert.That(resultAsc.Any(), Is.True);
        Assert.That(resultDesc.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_Statistics_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        var groupQueryWithStats = new GroupQuery
        {
            Statistics = true,
        };

        // Act
        var result = groupClient.Get(groupQueryWithStats);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_WithCustomAttributes_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        var groupQueryWithCustomAttributes = new GroupQuery
        {
            WithCustomAttributes = true,
        };

        // Act
        var result = groupClient.Get(groupQueryWithCustomAttributes);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_Owned_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        var groupQueryOwned = new GroupQuery
        {
            Owned = true,
        };

        // Act
        var result = groupClient.Get(groupQueryOwned);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_by_group_query_groupQuery_MinAccessLevel_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        var groupQuery10 = new GroupQuery
        {
            MinAccessLevel = AccessLevel.Guest,
        };
        var groupQuery20 = new GroupQuery
        {
            MinAccessLevel = AccessLevel.Reporter,
        };
        var groupQuery30 = new GroupQuery
        {
            MinAccessLevel = AccessLevel.Developer,
        };
        var groupQuery40 = new GroupQuery
        {
            MinAccessLevel = AccessLevel.Maintainer,
        };
        var groupQuery50 = new GroupQuery
        {
            MinAccessLevel = AccessLevel.Owner,
        };

        // Act
        var result10 = groupClient.Get(groupQuery10);
        var result20 = groupClient.Get(groupQuery20);
        var result30 = groupClient.Get(groupQuery30);
        var result40 = groupClient.Get(groupQuery40);
        var result50 = groupClient.Get(groupQuery50);

        // Assert
        Assert.That(result10.Any(), Is.True);
        Assert.That(result20.Any(), Is.True);
        Assert.That(result30.Any(), Is.True);
        Assert.That(result40.Any(), Is.True);
        Assert.That(result50.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_page_group_returns_expected_pages()
    {
        // Arrange
        // Create at least 1 group.
        // Note: This test runs with many existing groups, so all we can reliably
        // test is that the correct number of pages are returned.
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();

        var perPage = 3;

        // Act - Read first page
        (var firstPage, var total1) = await groupClient.PageAsync(new(page: 1, perPage: perPage));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(firstPage, Has.Count.AtLeast(1));
            Assert.That(firstPage, Has.Count.AtMost(perPage));
            Assert.That(total1, Is.AtLeast(firstPage.Count));
        });

        // Act - Read last page
        var totalPages = ((total1.Value - 1) / perPage) + 1;
        var expectedLastPageCount =
            total1.Value % perPage > 0
            ? total1.Value % perPage
            : perPage;

        (var lastPage, var total2) = await groupClient.PageAsync(new(page: totalPages, perPage: perPage));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(lastPage, Has.Count.EqualTo(expectedLastPageCount));
            Assert.That(total2, Is.EqualTo(total1));
        });

        // Act - Read past last page
        (var pastLastPage, var total3) = await groupClient.PageAsync(new(page: totalPages + 1, perPage: perPage));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(pastLastPage, Is.Empty);
            Assert.That(total3, Is.EqualTo(total1));
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_page_group_query_TopLevelOnly_does_not_return_children()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parent = context.CreateGroup();
        var child1 = context.CreateSubgroup(parent.Id, "child1");

        // Act
        var topLevelOnly = await PageAll(groupClient, new(perPage: 100, query: new() { TopLevelOnly = true }));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(child1.ParentId, Is.Not.Null);
            Assert.That(topLevelOnly.Where(g => g.ParentId != null), Is.Empty);
        });

        static async Task<List<Group>> PageAll(IGroupsClient groupClient, PageQuery<GroupQuery> query)
        {
            query.Page = PageQuery.FirstPage;
            var all = new List<Group>();
            while (true)
            {
                (var groups, _) = await groupClient.PageAsync(query).ConfigureAwait(false);
                if (groups.Count == 0)
                    break;

                all.AddRange(groups);
                ++query.Page;
            }

            return all;
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_group_projects_query_returns_archived()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();
        var project = context.CreateProject(group.Id, "test");

        var projectClient = context.Client.Projects;
        projectClient.Archive(project.Id);

        var projects = groupClient.GetProjectsAsync(group.Id, new GroupProjectsQuery
        {
            Archived = true,
        }).ToArray();

        group = groupClient[group.Id];
        Assert.That(group, Is.Not.Null);
        Assert.That(projects, Is.Not.Empty);

        var projectResult = projects.Single();
        Assert.That(projectResult.Id, Is.EqualTo(project.Id));
        Assert.That(projectResult.Archived, Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_group_projects_query_returns_searched_project()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();
        var project = context.CreateProject(group.Id);
        context.CreateProject(group.Id, "this-is-another-project");

        var projects = groupClient.GetProjectsAsync(group.Id, new GroupProjectsQuery
        {
            Search = project.Name,
        }).ToArray();

        Assert.That(projects.Select(p => p.Id), Is.EquivalentTo(new[] { project.Id }));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_page_group_projects_returns_expected_pages()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var group = context.CreateGroup();
        var allProjects = new[]
            {
                context.CreateProject(group.Id, "test1"),
                context.CreateProject(group.Id, "test2"),
                context.CreateProject(group.Id, "test3"),
            }
            .Select(p => p.Name)
            .ToArray();

        (var page1, var total1) = await groupClient.PageProjectsAsync(group.Id, new(page: 1, perPage: 2, new() { OrderBy = "name", Sort = "asc" }));
        (var page2, var total2) = await groupClient.PageProjectsAsync(group.Id, new(page: 2, perPage: 2, new() { OrderBy = "name", Sort = "asc" }));
        (var page3, var total3) = await groupClient.PageProjectsAsync(group.Id, new(page: 3, perPage: 2, new() { OrderBy = "name", Sort = "asc" }));

        Assert.Multiple(() =>
        {
            Assert.That(page1.Select(p => p.Name), Is.EquivalentTo(allProjects.Take(2)));
            Assert.That(total1, Is.EqualTo(3));
            Assert.That(page2.Select(p => p.Name), Is.EquivalentTo(allProjects.Skip(2)));
            Assert.That(total3, Is.EqualTo(3));
            Assert.That(page3.Select(p => p.Name), Is.Empty);
            Assert.That(total3, Is.EqualTo(3));
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroupOne = context.CreateGroup();
        var parentGroupTwo = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroupOne.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroupOne.Id);
        var subGroupThree = context.CreateSubgroup(parentGroupTwo.Id);

        var subgroups = groupClient.GetSubgroupsByIdAsync(parentGroupOne.Id);
        Assert.That(subgroups.Count(), Is.EqualTo(2));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroupOne = context.CreateGroup();
        var parentGroupTwo = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroupOne.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroupOne.Id);
        var subGroupThree = context.CreateSubgroup(parentGroupTwo.Id);

        var subgroups = groupClient.GetSubgroupsByFullPathAsync(parentGroupOne.FullPath);
        Assert.That(subgroups.Count(), Is.EqualTo(2));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_SkipGroups_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var skippedGroupIds = new[] { subGroupTwo.Id };

        // Act
        var resultSkip = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, new SubgroupQuery { SkipGroups = skippedGroupIds }).ToList();

        // Assert
        foreach (var skippedGroup in skippedGroupIds)
        {
            Assert.That(resultSkip.Exists(group => group.Id == skippedGroup), Is.False, $"Group {skippedGroup} found in results");
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_SkipGroups_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var skippedGroupIds = new[] { subGroupTwo.Id };

        // Act
        var resultSkip = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, new SubgroupQuery { SkipGroups = skippedGroupIds }).ToList();

        // Assert
        foreach (var skippedGroup in skippedGroupIds)
        {
            Assert.That(resultSkip.Exists(group => group.Id == skippedGroup), Is.False, $"Group {skippedGroup} found in results");
        }
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_groupQuery_Search_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var groupQuery = new SubgroupQuery
        {
            Search = subGroupOne.Name,
        };

        // Act
        var result = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQuery).Count(g => string.Equals(g.Name, subGroupOne.Name, StringComparison.Ordinal));

        // Assert
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_groupQuery_Search_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var groupQuery = new SubgroupQuery
        {
            Search = subGroupOne.Name,
        };

        // Act
        var result = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQuery).Count(g => string.Equals(g.Name, subGroupOne.Name, StringComparison.Ordinal));

        // Assert
        Assert.That(result, Is.EqualTo(1));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_groupQuery_AllAvailable_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var groupQueryAllAvailable = new SubgroupQuery
        {
            AllAvailable = true,
        };

        // Act
        var result = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryAllAvailable);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_query_groupQuery_AllAvailable_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var groupQueryAllAvailable = new SubgroupQuery
        {
            AllAvailable = true,
        };

        // Act
        var result = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryAllAvailable);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_groupQuery_OrderBy_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var groupQueryOrderByName = new SubgroupQuery
        {
            OrderBy = "name",
        };
        var groupQueryOrderByPath = new SubgroupQuery
        {
            OrderBy = "path",
        };
        var groupQueryOrderById = new SubgroupQuery
        {
            OrderBy = "id",
        };

        // Act
        var resultByName = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryOrderByName);
        var resultByPath = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryOrderByPath);
        var resultById = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryOrderById);

        // Assert
        Assert.That(resultByName.Any(), Is.True);
        Assert.That(resultByPath.Any(), Is.True);
        Assert.That(resultById.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_groupQuery_OrderBy_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var groupQueryOrderByName = new SubgroupQuery
        {
            OrderBy = "name",
        };
        var groupQueryOrderByPath = new SubgroupQuery
        {
            OrderBy = "path",
        };
        var groupQueryOrderById = new SubgroupQuery
        {
            OrderBy = "id",
        };

        // Act
        var resultByName = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryOrderByName);
        var resultByPath = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryOrderByPath);
        var resultById = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryOrderById);

        // Assert
        Assert.That(resultByName.Any(), Is.True);
        Assert.That(resultByPath.Any(), Is.True);
        Assert.That(resultById.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_groupQuery_Sort_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var groupQueryAsc = new SubgroupQuery
        {
            Sort = "asc",
        };
        var groupQueryDesc = new SubgroupQuery
        {
            Sort = "desc",
        };

        // Act
        var resultAsc = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryAsc);
        var resultDesc = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryDesc);

        // Assert
        Assert.That(resultAsc.Any(), Is.True);
        Assert.That(resultDesc.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_groupQuery_Sort_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        // Arrange
        var groupQueryAsc = new SubgroupQuery
        {
            Sort = "asc",
        };
        var groupQueryDesc = new SubgroupQuery
        {
            Sort = "desc",
        };

        // Act
        var resultAsc = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryAsc);
        var resultDesc = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryDesc);

        // Assert
        Assert.That(resultAsc.Any(), Is.True);
        Assert.That(resultDesc.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_groupQuery_Statistics_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        var groupQueryWithStats = new SubgroupQuery
        {
            Statistics = true,
        };

        // Act
        var result = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryWithStats);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_groupQuery_Statistics_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        var groupQueryWithStats = new SubgroupQuery
        {
            Statistics = true,
        };

        // Act
        var result = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryWithStats);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_groupQuery_WithCustomAttributes_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        var groupQueryWithCustomAttributes = new SubgroupQuery
        {
            WithCustomAttributes = true,
        };

        // Act
        var result = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryWithCustomAttributes);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_groupQuery_WithCustomAttributes_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        var groupQueryWithCustomAttributes = new SubgroupQuery
        {
            WithCustomAttributes = true,
        };

        // Act
        var result = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryWithCustomAttributes);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_groupQuery_Owned_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        var groupQueryOwned = new SubgroupQuery
        {
            Owned = true,
        };

        // Act
        var result = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryOwned);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_groupQuery_Owned_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        var groupQueryOwned = new SubgroupQuery
        {
            Owned = true,
        };

        // Act
        var result = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryOwned);

        // Assert
        Assert.That(result.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_id_groupQuery_MinAccessLevel_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        var groupQueryGuest = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Guest,
        };
        var groupQueryReporter = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Reporter,
        };
        var groupQueryDeveloper = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Developer,
        };
        var groupQueryMantainer = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Maintainer,
        };
        var groupQueryOwner = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Owner,
        };

        // Act
        var resultGuest = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryGuest);
        var resultReporter = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryReporter);
        var resultDeveloper = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryDeveloper);
        var resultMantainer = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryMantainer);
        var resultOwner = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, groupQueryOwner);

        // Assert
        Assert.That(resultGuest.Any(), Is.True);
        Assert.That(resultReporter.Any(), Is.True);
        Assert.That(resultDeveloper.Any(), Is.True);
        Assert.That(resultMantainer.Any(), Is.True);
        Assert.That(resultOwner.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_subgroups_by_fullpath_groupQuery_MinAccessLevel_returns_groups()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parentGroup = context.CreateGroup();

        var subGroupOne = context.CreateSubgroup(parentGroup.Id);
        var subGroupTwo = context.CreateSubgroup(parentGroup.Id);
        var subGroupThree = context.CreateSubgroup(parentGroup.Id);

        var groupQueryGuest = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Guest,
        };
        var groupQueryReporter = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Reporter,
        };
        var groupQueryDeveloper = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Developer,
        };
        var groupQueryMantainer = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Maintainer,
        };
        var groupQueryOwner = new SubgroupQuery
        {
            MinAccessLevel = AccessLevel.Owner,
        };

        // Act
        var resultGuest = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryGuest);
        var resultReporter = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryReporter);
        var resultDeveloper = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryDeveloper);
        var resultMantainer = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryMantainer);
        var resultOwner = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, groupQueryOwner);

        // Assert
        Assert.That(resultGuest.Any(), Is.True);
        Assert.That(resultReporter.Any(), Is.True);
        Assert.That(resultDeveloper.Any(), Is.True);
        Assert.That(resultMantainer.Any(), Is.True);
        Assert.That(resultOwner.Any(), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_page_subgroup_returns_expected_pages()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parent = context.CreateGroup();
        var child1 = context.CreateSubgroup(parent.Id, "child1");
        var child2 = context.CreateSubgroup(parent.Id, "child2");
        var child3 = context.CreateSubgroup(parent.Id, "child3");
        var grandchild = context.CreateSubgroup(child1.Id, "grandchild");

        var allChildren = new[] { child1, child2, child3, }
            .Select(g => g.Name)
            .ToArray();

        var firstPageQuery = new PageQuery<SubgroupQuery>(page: 1, perPage: 2, new() { OrderBy = "name", Sort = "asc" });
        var lastPageQuery = new PageQuery<SubgroupQuery>(page: 2, perPage: 2, new() { OrderBy = "name", Sort = "asc" });
        var allInOnePageQuery = new PageQuery<SubgroupQuery>(page: 1, perPage: 100, new() { OrderBy = "name", Sort = "asc" });
        var pageZeroQuery = new PageQuery<SubgroupQuery>(page: 0, query: new() { OrderBy = "name", Sort = "asc" });
        var defaultQuery = new PageQuery<SubgroupQuery>(query: new() { OrderBy = "name", Sort = "asc" });

        // Act
        (var firstPage, var total1) = await groupClient.PageSubgroupsAsync(parent.Id, firstPageQuery);
        (var lastPage, var total2) = await groupClient.PageSubgroupsAsync(parent.FullPath, lastPageQuery);
        (var allInOnePage, var total3) = await groupClient.PageSubgroupsAsync(parent.Id, allInOnePageQuery);
        (var pageZero, var total4) = await groupClient.PageSubgroupsAsync(parent.FullPath, pageZeroQuery);
        (var defaultPage, var total5) = await groupClient.PageSubgroupsAsync(parent.Id, defaultQuery);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(firstPage.Select(g => g.Name), Is.EquivalentTo(allChildren.Skip(0).Take(2)));
            Assert.That(total1, Is.EqualTo(allChildren.Length));
        });

        Assert.Multiple(() =>
        {
            Assert.That(lastPage.Select(g => g.Name), Is.EquivalentTo(allChildren.Skip(2).Take(2)));
            Assert.That(total2, Is.EqualTo(allChildren.Length));
        });

        Assert.Multiple(() =>
        {
            Assert.That(allInOnePage.Select(g => g.Name), Is.EquivalentTo(allChildren));
            Assert.That(total3, Is.EqualTo(allChildren.Length));
        });

        Assert.Multiple(() =>
        {
            Assert.That(pageZero.Select(g => g.Name), Is.EquivalentTo(allChildren));
            Assert.That(total4, Is.EqualTo(allChildren.Length));
        });

        Assert.Multiple(() =>
        {
            Assert.That(defaultPage.Select(g => g.Name), Is.EquivalentTo(allChildren));
            Assert.That(total5, Is.EqualTo(allChildren.Length));
        });
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_page_subgroup_including_descendants_returns_expected_pages()
    {
        // Arrange
        using var context = await GitLabTestContext.CreateAsync();
        var groupClient = context.Client.Groups;
        var parent = context.CreateGroup();
        var child1 = context.CreateSubgroup(parent.Id, "child1");
        var child2 = context.CreateSubgroup(parent.Id, "child2");
        var child3 = context.CreateSubgroup(parent.Id, "child3");
        var grandchild1 = context.CreateSubgroup(child1.Id, "grandchild1");
        var grandchild2 = context.CreateSubgroup(child2.Id, "grandchild2");

        var allDescendants = new[] { child1, child2, child3, grandchild1, grandchild2 }
            .Select(g => g.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var firstPageQuery = new PageQuery<SubgroupQuery>(page: 1, perPage: 3, new() { IncludeDescendants = true, OrderBy = "name" });
        var lastPageQuery = new PageQuery<SubgroupQuery>(page: 2, perPage: 3, new() { IncludeDescendants = true, OrderBy = "name" });
        var allInOnePageQuery = new PageQuery<SubgroupQuery>(page: 1, perPage: 100, new() { IncludeDescendants = true, OrderBy = "name" });
        var pageZeroQuery = new PageQuery<SubgroupQuery>(page: 0, query: new() { IncludeDescendants = true, OrderBy = "name" });
        var defaultQuery = new PageQuery<SubgroupQuery>(query: new() { IncludeDescendants = true, OrderBy = "name" });

        // Act
        (var firstPage, var total1) = await groupClient.PageSubgroupsAsync(parent.Id, firstPageQuery);
        (var lastPage, var total2) = await groupClient.PageSubgroupsAsync(parent.FullPath, lastPageQuery);
        (var allInOnePage, var total3) = await groupClient.PageSubgroupsAsync(parent.Id, allInOnePageQuery);
        (var pageZero, var total4) = await groupClient.PageSubgroupsAsync(parent.FullPath, pageZeroQuery);
        (var defaultPage, var total5) = await groupClient.PageSubgroupsAsync(parent.Id, defaultQuery);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(firstPage.Select(g => g.Name), Is.EquivalentTo(allDescendants.Skip(0).Take(3)));
            Assert.That(total1, Is.EqualTo(allDescendants.Length));
        });

        Assert.Multiple(() =>
        {
            Assert.That(lastPage.Select(g => g.Name), Is.EquivalentTo(allDescendants.Skip(3).Take(3)));
            Assert.That(total2, Is.EqualTo(allDescendants.Length));
        });

        Assert.Multiple(() =>
        {
            Assert.That(allInOnePage.Select(g => g.Name), Is.EquivalentTo(allDescendants));
            Assert.That(total3, Is.EqualTo(allDescendants.Length));
        });

        Assert.Multiple(() =>
        {
            Assert.That(pageZero.Select(g => g.Name), Is.EquivalentTo(allDescendants));
            Assert.That(total4, Is.EqualTo(allDescendants.Length));
        });

        Assert.Multiple(() =>
        {
            Assert.That(defaultPage.Select(g => g.Name), Is.EquivalentTo(allDescendants));
            Assert.That(total5, Is.EqualTo(allDescendants.Length));
        });
    }
}
