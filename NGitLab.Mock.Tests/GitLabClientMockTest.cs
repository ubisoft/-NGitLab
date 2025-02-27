using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NGitLab.Mock.Config;
using NGitLab.Models;
using NUnit.Framework;

namespace NGitLab.Mock.Tests;


public class GitLabClientMockTest
{
    public static IEnumerable ProjectClientTestCases
    {
        get
        {
            static TestCaseData TestCase(string name, Func<IGitLabClient, ProjectId, object> getClient) => new TestCaseData(getClient).SetArgDisplayNames(name);

            // Create a test case for each method in IGitLabClient that has a single parameter of type ProjectId
            foreach (var method in GetMethods<ProjectId>())
            {
                yield return TestCase(method.Name, (client, id) => method.Invoke(client, new object[] { id }));
            }
        }
    }

    public static IEnumerable GroupClientTestCases
    {
        get
        {
            static TestCaseData TestCase(string name, Func<IGitLabClient, GroupId, object> getClient) => new TestCaseData(getClient).SetArgDisplayNames(name);

            // Create a test case for each method in IGitLabClient that has a single parameter of type GroupId
            foreach (var method in GetMethods<GroupId>())
            {
                // GetGroupMergeRequest() is not implemented in the mock MergeRequestClient and will throw a NotImplementedException
                // To avoid a test failure, exclude this method from the test cases (this is the only such case)
                // This can be removed if a mock for group merge requests is implemented
                if (string.Equals(method.Name, nameof(IGitLabClient.GetGroupMergeRequest), StringComparison.Ordinal))
                {
                    continue;
                }

                yield return TestCase(method.Name, (client, id) => method.Invoke(client, new object[] { id }));
            }

        }
    }

    [TestCaseSource(nameof(ProjectClientTestCases))]
    public void Test_can_get_project_client(Func<IGitLabClient, ProjectId, object> getClient)
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithGroup("test-group")
            .WithProject("test-project", id: 1, @namespace: "test-group")
            .BuildServer();

        var client = server.CreateClient();

        Assert.Multiple(() =>
        {
            Assert.That(getClient(client, 1), Is.Not.Null);
            Assert.That(getClient(client, "test-group/test-project"), Is.Not.Null);
        });
    }

    [TestCaseSource(nameof(GroupClientTestCases))]
    public void Test_can_get_group_client(Func<IGitLabClient, GroupId, object> getClient)
    {
        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithGroup("test-group", group =>
            {
                group.Namespace = "parent-group";
                group.Id = 1;
            })
            .BuildServer();

        var client = server.CreateClient();

        Assert.Multiple(() =>
        {
            Assert.That(getClient(client, 1), Is.Not.Null);
            Assert.That(getClient(client, "parent-group/test-group"), Is.Not.Null);
        });
    }

    [Test]
    public void Test_getting_MergeRequestClient_for_group_is_not_implemented()
    {
        // GetGroupMergeRequest() is not implemented in the mock MergeRequestClient and will throw a NotImplementedException
        // For that reason, this method is excluded from the test cases retruend by GroupClientTestCases
        //
        // This test checkes that this assumption still holds true and the method is rightly is skipped in GroupClientTestCases
        // When a mock for GetGroupMergeRequest(), this test will fail.
        // In this case, this test special logic for GetGroupMergeRequest() in GroupClientTestCases can be removed

        using var server = new GitLabConfig()
            .WithUser("user1", isDefault: true)
            .WithGroup("test-group", group =>
            {
                group.Namespace = "parent-group";
                group.Id = 1;
            })
            .BuildServer();

        var client = server.CreateClient();

        Assert.Multiple(() =>
        {
            Assert.Throws<NotImplementedException>(() => client.GetGroupMergeRequest(1));
            Assert.Throws<NotImplementedException>(() => client.GetGroupMergeRequest("parent-group/test-group"));
        });
    }

    private static IEnumerable<MethodInfo> GetMethods<TParameter>()
    {
        return typeof(IGitLabClient)
            .GetMethods()
            .Where(method => method.IsPublic &&
                             method.GetParameters().Length == 1 &&
                             method.GetParameters()[0].ParameterType == typeof(TParameter));
    }
}
