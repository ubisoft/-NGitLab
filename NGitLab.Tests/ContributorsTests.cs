using System;
using System.Linq;
using System.Threading.Tasks;
using NGitLab.Models;
using NGitLab.Tests.Docker;
using NUnit.Framework;

namespace NGitLab.Tests;

public class ContributorsTests
{
    [Test]
    [NGitLabRetry]
    public async Task Test_can_get_contributors()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);
        var contributorsClient = context.Client.GetRepository(project.Id).Contributors;
        var currentUser = context.Client.Users.Current;

        var contributor = contributorsClient.All;
        Assert.That(contributor, Is.Not.Null);
        Assert.That(contributor.Any(x => string.Equals(x.Email, currentUser.Email, StringComparison.Ordinal)), Is.True);
    }

    [Test]
    [NGitLabRetry]
    public async Task Test_can_get_MultipleContributors()
    {
        using var context = await GitLabTestContext.CreateAsync();
        var project = context.CreateProject(initializeWithCommits: true);
        var contributorsClient = context.Client.GetRepository(project.Id).Contributors;
        var currentUser = context.Client.Users.Current;

        var randomString = context.GetUniqueRandomString();
        var userUpsert = new UserUpsert
        {
            Email = $"{randomString}@example.com",
            Bio = "bio",
            CanCreateGroup = true,
            IsAdmin = true,
            Linkedin = null,
            Name = $"NGitLab Test Contributor {randomString}",
            Password = "!@#$QWDRQW@",
            ProjectsLimit = 1000,
            Provider = "provider",
            ExternalUid = "external_uid_" + randomString,
            Skype = "skype",
            Twitter = "twitter",
            Username = $"ngitlabtestcontributor{randomString}",
            WebsiteURL = "https://www.example.com",
        };

        var user = context.AdminClient.Users.Create(userUpsert);
        context.Client.GetCommits(project.Id).Create(new CommitCreate
        {
            AuthorName = userUpsert.Name,
            AuthorEmail = userUpsert.Email,
            Branch = project.DefaultBranch,
            StartBranch = project.DefaultBranch,
            CommitMessage = "test",
        });

        var contributors = await GitLabTestContext.RetryUntilAsync(() => contributorsClient.All.ToList(), c => c.Count >= 2, TimeSpan.FromMinutes(2));

        Assert.That(contributors.Exists(x => string.Equals(x.Email, currentUser.Email, StringComparison.Ordinal)), Is.True);
        Assert.That(contributors.Exists(x => string.Equals(x.Email, userUpsert.Email, StringComparison.Ordinal)), Is.True);

        context.AdminClient.Users.Delete(user.Id);
    }
}
