using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NGitLab.Models;
using NuGet.Versioning;
using NUnit.Framework;
using Polly;

namespace NGitLab.Tests.Docker;

public sealed class GitLabTestContext : IDisposable
{
    private const int MaxRetryCount = 10;

    private static readonly Policy s_gitlabRetryPolicy = Policy.Handle<GitLabException>()
        .WaitAndRetry(MaxRetryCount, _ => TimeSpan.FromSeconds(1), (exception, timespan, retryCount, context) =>
        {
            TestContext.Out.WriteLine($"[{TestContext.CurrentContext.Test.FullName}] {exception.Message} -> Polly Retry {retryCount} of {MaxRetryCount}...");
        });

    private static readonly HashSet<string> s_generatedValues = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim s_prepareRunnerLock = new(1, 1);

    private readonly GitLabTestContextRequestOptions _customRequestOptions = new();
    private readonly List<IGitLabClient> _clients = [];

    public GitLabDockerContainer DockerContainer { get; set; }

    private GitLabTestContext(GitLabDockerContainer container)
    {
        DockerContainer = container;
        AdminClient = CreateClient(DockerContainer.Credentials.AdminUserToken);
        Client = CreateClient(DockerContainer.Credentials.UserToken);

        HttpClient = new HttpClient
        {
            BaseAddress = DockerContainer.GitLabUrl,
        };

        AdminHttpClient = new HttpClient
        {
            BaseAddress = DockerContainer.GitLabUrl,
            DefaultRequestHeaders =
            {
                { "Cookie", "_gitlab_session=" + DockerContainer.Credentials.AdminCookies },
            },
        };
    }

    public static async Task<GitLabTestContext> CreateAsync()
    {
        // Disable proxy
        Environment.SetEnvironmentVariable("http_proxy", "", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable("https_proxy", "", EnvironmentVariableTarget.Process);
        var container = await GitLabDockerContainer.GetOrCreateInstance().ConfigureAwait(false);
        return new GitLabTestContext(container);
    }

    public HttpClient HttpClient { get; }

    public HttpClient AdminHttpClient { get; }

    public IGitLabClient AdminClient { get; }

    public IGitLabClient Client { get; }

    public WebRequest LastRequest => _customRequestOptions.AllRequests[_customRequestOptions.AllRequests.Count - 1];

    private static bool IsUnique(string str)
    {
        lock (s_generatedValues)
        {
            return s_generatedValues.Add(str);
        }
    }

    public IGitLabClient CreateNewUserAsync() => CreateNewUser(out _);

    public IGitLabClient CreateNewUser(out User user)
    {
        var username = "user_" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
        var email = username + "@dummy.com";
        var password = "Pa$$w0rd";
        var client = AdminClient;

        user = client.Users.Create(new UserUpsert
        {
            Email = email,
            Username = username,
            Name = username,
            Password = password,
            CanCreateGroup = true,
            SkipConfirmation = true,
        });

        var token = client.Users.CreateToken(new UserTokenCreate
        {
            UserId = user.Id,
            Name = "UnitTest",
            Scopes = new[] { "api", "read_user" },
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        return CreateClient(token.Token);
    }

    public Project CreateProject(Action<ProjectCreate> configure = null, bool initializeWithCommits = false)
    {
        var client = Client;
        var defaultBranch = "main";

        var project = s_gitlabRetryPolicy.Execute(() =>
        {
            var projectCreate = new ProjectCreate
            {
                Name = GetUniqueRandomString(),
                DefaultBranch = defaultBranch,
                Description = "Test project",
                IssuesEnabled = true,
                MergeRequestsEnabled = true,
                SnippetsEnabled = true,
                VisibilityLevel = VisibilityLevel.Internal,
                WikiEnabled = true,
            };

            configure?.Invoke(projectCreate);
            return client.Projects.Create(projectCreate);
        });

        // When creating a project, GitLab's JSON response should indicate the 'default_branch'. However, it
        // currently returns null instead (at least in versions <= 13.10.3). This info would come in handy, since
        // a 'default_branch' might be specified at the GitLab instance, group, subgroup and/or project levels, and
        // we don't want to query those levels to determine what the default branch is for the current project.
        // Until https://gitlab.com/gitlab-org/gitlab/-/issues/330622 is resolved, we'll patch the value ourselves.
        project.DefaultBranch ??= defaultBranch;

        if (initializeWithCommits)
        {
            AddSomeCommits();
            s_gitlabRetryPolicy.Execute(() =>
            {
                var projectUpdate = new ProjectUpdate
                {
                    DefaultBranch = defaultBranch,
                };

                return client.Projects.Update(project.Id.ToString(CultureInfo.InvariantCulture), projectUpdate);
            });
        }

        return project;

        void AddSomeCommits()
        {
            s_gitlabRetryPolicy.Execute(() =>
                client.GetRepository(project.Id).Files.Create(new FileUpsert
                {
                    Branch = project.DefaultBranch,
                    CommitMessage = "add readme",
                    Path = "README.md",
                    RawContent = "this project should only live during the unit tests, you can delete if you find some",
                }));

            for (var i = 0; i < 3; i++)
            {
                s_gitlabRetryPolicy.Execute(() =>
                    client.GetRepository(project.Id).Files.Create(new FileUpsert
                    {
                        Branch = project.DefaultBranch,
                        CommitMessage = $"add test file {i.ToString(CultureInfo.InvariantCulture)}",
                        Path = $"TestFile{i.ToString(CultureInfo.InvariantCulture)}.txt",
                        RawContent = "this project should only live during the unit tests, you can delete if you find some",
                    }));
            }
        }
    }

    public Project CreateProject(long parentGroupId, Action<ProjectCreate> configure = null, bool initializeWithCommits = false) =>
        CreateProject(initializeWithCommits: initializeWithCommits, configure: p =>
        {
            p.NamespaceId = parentGroupId;
            configure?.Invoke(p);
        });

    public Project CreateProject(long parentGroupId, string slug, string name = null, Action<ProjectCreate> configure = null, bool initializeWithCommits = false) =>
        CreateProject(parentGroupId, initializeWithCommits: initializeWithCommits, configure: p =>
        {
            p.Path = slug;
            p.Name = name ?? slug;
            configure?.Invoke(p);
        });

    public Group CreateGroup(Action<GroupCreate> configure = null)
    {
        var client = Client;
        var name = GetUniqueRandomString();
        var groupCreate = new GroupCreate
        {
            Name = name,
            Path = name,
            Description = "Test group",
            Visibility = VisibilityLevel.Internal,
        };

        configure?.Invoke(groupCreate);
        return client.Groups.Create(groupCreate);
    }

    public Group CreateGroup(string slug, string name = null, Action<GroupCreate> configure = null) =>
        CreateGroup(g =>
        {
            g.Path = slug;
            g.Name = name ?? slug;
            configure?.Invoke(g);
        });

    public Group CreateSubgroup(long parentGroupId, Action<GroupCreate> configure = null) =>
        CreateGroup(g =>
        {
            g.ParentId = parentGroupId;
            configure?.Invoke(g);
        });

    public Group CreateSubgroup(long parentGroupId, string slug, string name = null, Action<GroupCreate> configure = null) =>
        CreateSubgroup(parentGroupId, g =>
        {
            g.Path = slug;
            g.Name = name ?? slug;
            configure?.Invoke(g);
        });

    public (Project Project, MergeRequest MergeRequest) CreateMergeRequest(Action<MergeRequestCreate> configure = null, Action<ProjectCreate> configureProject = null)
    {
        var client = Client;
        var project = CreateProject(configureProject, initializeWithCommits: true);

        const string BranchForMRName = "branch-for-mr";
        s_gitlabRetryPolicy.Execute(() => client.GetRepository(project.Id).Files.Create(new FileUpsert { Branch = project.DefaultBranch, CommitMessage = "test", Content = "test", Path = "test.md" }));
        s_gitlabRetryPolicy.Execute(() => client.GetRepository(project.Id).Branches.Create(new BranchCreate { Name = BranchForMRName, Ref = project.DefaultBranch }));

        // Restore the default branch because sometimes GitLab change the default branch to "branch-for-mr"
        project = client.Projects.Update(project.Id.ToString(CultureInfo.InvariantCulture), new ProjectUpdate
        {
            DefaultBranch = project.DefaultBranch,
        });

        var branch = client.GetRepository(project.Id).Branches.All.FirstOrDefault(b => string.Equals(b.Name, project.DefaultBranch, StringComparison.Ordinal));
        Assert.That(branch, Is.Not.Null, $"Branch '{project.DefaultBranch}' should exist");
        Assert.That(branch.Default, Is.True, $"Branch '{project.DefaultBranch}' should be the default one");

        branch = client.GetRepository(project.Id).Branches.All.FirstOrDefault(b => string.Equals(b.Name, BranchForMRName, StringComparison.Ordinal));
        Assert.That(branch, Is.Not.Null, $"Branch '{BranchForMRName}' should exist");
        Assert.That(branch.Protected, Is.False, $"Branch '{BranchForMRName}' should not be protected");

        s_gitlabRetryPolicy.Execute(() => client.GetRepository(project.Id).Files.Update(new FileUpsert { Branch = BranchForMRName, CommitMessage = "test", Content = "test2", Path = "test.md" }));

        var mergeRequestCreate = new MergeRequestCreate
        {
            SourceBranch = BranchForMRName,
            TargetBranch = project.DefaultBranch,
            Title = "test",
        };

        configure?.Invoke(mergeRequestCreate);
        var mr = client.GetMergeRequest(project.Id).Create(mergeRequestCreate);

        return (project, mr);
    }

    [SuppressMessage("Performance", "MA0038:Make method static", Justification = "By design")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "By design")]
    public string GetUniqueRandomString()
    {
        for (var i = 0; i < 1000; i++)
        {
            var result = "GitLabClientTests_" + Guid.NewGuid().ToString("N");
            if (IsUnique(result))
                return result;
        }

        throw new InvalidOperationException("Cannot generate a new random unique string");
    }

    [SuppressMessage("Design", "MA0038:Make method static", Justification = "By design")]
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "By design")]
    public int GetRandomNumber()
    {
        return RandomNumberGenerator.GetInt32(0, int.MaxValue);
    }

    public bool IsGitLabVersionInRange(VersionRange versionRange, out string gitLabVersion)
    {
        var currentVersion = Client.Version.Get();
        gitLabVersion = currentVersion.Version;

        // Although a GitLab version is not a NuGet version, let's consider it as one to determine range inclusion
        return NuGetVersion.TryParse(gitLabVersion, out var nuGetVersion) &&
               versionRange.Satisfies(nuGetVersion);
    }

    public bool IsGitLabMajorVersion(int major)
    {
        var currentVersion = Client.Version.Get();
        var gitLabVersion = currentVersion.Version;

        return NuGetVersion.TryParse(gitLabVersion, out var nuGetVersion) && nuGetVersion.Major == major;
    }

    public void ReportTestAsInconclusiveIfGitLabVersionOutOfRange(VersionRange versionRange)
    {
        if (!IsGitLabVersionInRange(versionRange, out var gitLabVersion))
            Assert.Inconclusive($"Test supported by GitLab '{versionRange}', but currently running against '{gitLabVersion}'");
    }

    private IGitLabClient CreateClient(string token)
    {
        var client = new GitLabClient(DockerContainer.GitLabUrl.ToString(), token, _customRequestOptions);
        _clients.Add(client);
        return client;
    }

    internal static bool IsContinuousIntegration()
    {
        return string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Environment.GetEnvironmentVariable("GITLAB_CI"), "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IDisposable> StartRunnerForOneJobAsync(long projectId)
    {
        // Download runner (windows / linux, GitLab version)
        await s_prepareRunnerLock.WaitAsync().ConfigureAwait(false);

        try
        {
            // Version availables: https://gitlab.com/gitlab-org/gitlab-runner/-/releases
            var version = "15.6.3";
            var path = Path.Combine(Path.GetTempPath(), "GitLabClient", "Runners", version, "gitlab-runner.exe");
            if (!File.Exists(path))
            {
                Uri url;
                if (OperatingSystem.IsWindows())
                {
                    url = new Uri($"https://gitlab-runner-downloads.s3.amazonaws.com/v{version}/binaries/gitlab-runner-windows-amd64.exe");
                }
                else if (OperatingSystem.IsLinux())
                {
                    url = new Uri($"https://gitlab-runner-downloads.s3.amazonaws.com/v{version}/binaries/gitlab-runner-linux-amd64");
                }
                else
                {
                    throw new InvalidOperationException($"OS '{RuntimeInformation.OSDescription}' is not supported");
                }

                var stream = await HttpClient.GetStreamAsync(url).ConfigureAwait(false);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                    var fs = File.OpenWrite(path);
                    try
                    {
                        await stream.CopyToAsync(fs).ConfigureAwait(false);
                    }
                    finally
                    {
                        await fs.DisposeAsync().ConfigureAwait(false);
                    }
                }
                finally
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
            }

            TestContext.Out.WriteLine("Test runner downloaded");
            if (OperatingSystem.IsLinux())
            {
                using var chmodProcess = Process.Start("chmod", "+x \"" + path + "\"");
                chmodProcess.WaitForExit();
                if (chmodProcess.ExitCode != 0)
                    throw new InvalidOperationException("chmod failed");

                TestContext.Out.WriteLine("chmod run");
            }

            if (!IsContinuousIntegration())
            {
                // Update the git configuration to remove any proxy for this host
                // git config --global http.http://localhost:48624.proxy ""
                using var gitConfigProcess = Process.Start("git", "config --global http." + DockerContainer.GitLabUrl.ToString() + ".proxy \"\"");
                gitConfigProcess.WaitForExit();
                if (gitConfigProcess.ExitCode != 0)
                    throw new InvalidOperationException("git config failed");

                TestContext.Out.WriteLine("git config changed");
            }

            var project = AdminClient.Projects[projectId];
            if (project.RunnersToken == null)
                throw new InvalidOperationException("Project runner token is null");

            var runner = AdminClient.Runners.Register(new RunnerRegister { Token = project.RunnersToken, Description = "test" });
            if (runner.Token == null)
                throw new InvalidOperationException("Runner token is null");

            TestContext.Out.WriteLine($"Runner registered '{runner.Token}'");

            // Use run-single, so we don't need to manage the configuration file.
            // Also, I don't think there is a need to run multiple jobs in a test
            var buildDir = Path.Combine(Path.GetTempPath(), "GitLabClient", "Runners", "build");
            Directory.CreateDirectory(buildDir);
            var psi = new ProcessStartInfo
            {
                FileName = path,
                ArgumentList =
                {
                    "run-single",
                    "--url", DockerContainer.GitLabUrl.ToString(),
                    "--executor", "shell",
                    "--shell", OperatingSystem.IsWindows() ? "powershell" : "pwsh",
                    "--builds-dir", buildDir,
                    "--wait-timeout", "240", // in seconds
                    "--token", runner.Token,
                },
                CreateNoWindow = true,
                ErrorDialog = false,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            var process = Process.Start(psi) ?? throw new InvalidOperationException("Cannot start the runner");

            // Give some time to ensure the runner doesn't stop immediately
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            if (process.HasExited)
                throw new InvalidOperationException("The runner has exited");

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();
            process.ErrorDataReceived += (sender, e) => Console.Error.WriteLine(e.Data);
            process.OutputDataReceived += (sender, e) => Console.Error.WriteLine(e.Data);

            TestContext.Out.WriteLine($"Runner started for project '{project.Id}' on '{DockerContainer.GitLabUrl}'");
            return new ProcessKill(process);
        }
        finally
        {
            s_prepareRunnerLock.Release();
        }
    }

    public static async Task<T> RetryUntilAsync<T>(Func<T> action, Func<T, bool> predicate, TimeSpan timeSpan)
    {
        using var cts = new CancellationTokenSource(timeSpan);
        return await RetryUntilAsync(action, predicate, cts.Token).ConfigureAwait(false);
    }

    public static async Task<T> RetryUntilAsync<T>(Func<T> action, Func<T, bool> predicate, CancellationToken cancellationToken)
    {
        var retryCount = 1;
        var result = action();
        while (!predicate(result))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TestContext.Out.WriteLine($"[{TestContext.CurrentContext.Test.FullName}] RetryUntilAsync {retryCount++}...");
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

            result = action();
        }

        return result;
    }

    public void Dispose()
    {
    }

    private sealed class ProcessKill : IDisposable
    {
        private readonly Process _process;

        public ProcessKill(Process process)
        {
            _process = process;
        }

        public void Dispose()
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit();
        }
    }
}
