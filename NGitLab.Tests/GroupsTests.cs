using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests
{
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
            var project = context.Client.Projects.Create(new ProjectCreate { Name = "test", NamespaceId = group.Id.ToString(CultureInfo.InvariantCulture) });

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

        private static Group TryGetGroup(IGroupsClient groupClient, int groupId)
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
                Assert.That(resultSkip.Any(group => group.Id == skippedGroup), Is.False, $"Group {skippedGroup} found in results");
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
        public async Task Test_group_projects_query_returns_archived()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var groupClient = context.Client.Groups;
            var group = context.CreateGroup();

            var projectClient = context.Client.Projects;
            var project = projectClient.Create(new ProjectCreate { Name = "test", NamespaceId = group.Id.ToString(CultureInfo.InvariantCulture) });
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

            var projectClient = context.Client.Projects;
            var project = projectClient.Create(new ProjectCreate { Name = "test", NamespaceId = group.Id.ToString(CultureInfo.InvariantCulture) });
            projectClient.Create(new ProjectCreate { Name = "this is another project", NamespaceId = group.Id.ToString(CultureInfo.InvariantCulture) });

            var projects = groupClient.GetProjectsAsync(group.Id, new GroupProjectsQuery
            {
                Search = "test",
            }).ToArray();

            group = groupClient[group.Id];
            Assert.That(group, Is.Not.Null);
            Assert.That(projects, Is.Not.Empty);

            var projectResult = projects.Single();
            Assert.That(projectResult.Id, Is.EqualTo(project.Id));
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_subgroups_by_id()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var groupClient = context.Client.Groups;
            var parentGroupOne = context.CreateGroup();
            var parentGroupTwo = context.CreateGroup();

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroupOne.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroupOne.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroupTwo.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroupOne.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroupOne.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroupTwo.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

            // Arrange
            var skippedGroupIds = new[] { subGroupTwo.Id };

            // Act
            var resultSkip = groupClient.GetSubgroupsByIdAsync(parentGroup.Id, new SubgroupQuery { SkipGroups = skippedGroupIds }).ToList();

            // Assert
            foreach (var skippedGroup in skippedGroupIds)
            {
                Assert.That(resultSkip.Any(group => group.Id == skippedGroup), Is.False, $"Group {skippedGroup} found in results");
            }
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_subgroups_by_fullpath_SkipGroups_returns_groups()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var groupClient = context.Client.Groups;
            var parentGroup = context.CreateGroup();

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

            // Arrange
            var skippedGroupIds = new[] { subGroupTwo.Id };

            // Act
            var resultSkip = groupClient.GetSubgroupsByFullPathAsync(parentGroup.FullPath, new SubgroupQuery { SkipGroups = skippedGroupIds }).ToList();

            // Assert
            foreach (var skippedGroup in skippedGroupIds)
            {
                Assert.That(resultSkip.Any(group => group.Id == skippedGroup), Is.False, $"Group {skippedGroup} found in results");
            }
        }

        [Test]
        [NGitLabRetry]
        public async Task Test_get_subgroups_by_id_groupQuery_Search_returns_groups()
        {
            using var context = await GitLabTestContext.CreateAsync();
            var groupClient = context.Client.Groups;
            var parentGroup = context.CreateGroup();

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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

            var subGroupOne = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupTwo = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);
            var subGroupThree = context.CreateGroup(configure: group => group.ParentId = parentGroup.Id);

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
    }
}
