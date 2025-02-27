using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;
using static NGitLab.Extensions.NumberExtensions;

namespace NGitLab.Tests;

public class GraphQLTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_invalid_request()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();

        var exception = Assert.ThrowsAsync<GitLabException>(() => context.Client.GraphQL.ExecuteAsync<ProjectResponse>(new GraphQLQuery
        {
            Query = """
                {
                  project(fullPath: $path) {
                    unknownProperty
                  }
                }
                """,
        }));

        Assert.That(exception.Message, Does.Contain("Field 'unknownProperty' doesn't exist on type 'Project'"));
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_get_project()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject();

        var response = await context.Client.GraphQL.ExecuteAsync<ProjectResponse>(new GraphQLQuery
        {
            Query = """
                query($path: ID!)
                {
                  project(fullPath: $path) {
                    id
                  }
                }
                """,
            Variables =
            {
                ["path"] = project.PathWithNamespace,
            },
        });

        Assert.That(response.Project.Id, Is.EqualTo("gid://gitlab/Project/" + project.Id.ToStringInvariant()));
    }

    private sealed class ProjectResponse
    {
        [JsonPropertyName("project")]
        public ProjecInfoResponse Project { get; set; }
    }

    private sealed class ProjecInfoResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
    }
}
