#pragma warning disable MA0004
#pragma warning disable MA0006
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Playwright;
using NGitLab.Models;
using NuGet.Versioning;
using NUnit.Framework;
using Polly;

namespace NGitLab.Tests.Docker;

public class GitLabDockerContainer
{
    public const string ContainerName = "NGitLabClientTests";
    public const string ImageName = "gitlab/gitlab-ee";

    /// <summary>
    /// GitLab docker image version to spawn.
    /// Used only on local environment (CI should already have a running GitLab instance from its services)
    /// </summary>
    /// <remarks>
    /// <para>Keep in sync with .github/workflows/ci.yml, use the lowest supported version</para>
    /// <para>List of available versions: https://hub.docker.com/r/gitlab/gitlab-ee/tags/</para>
    /// </remarks>
    private const string LocalGitLabDockerVersion = "15.11.9-ee.0";

    /// <summary>
    /// Resolved GitLab version taken from the help page once logged in
    /// </summary>
    private static string ResolvedGitLabVersion;

    private static string s_creationErrorMessage;
    private static readonly SemaphoreSlim s_setupLock = new(initialCount: 1, maxCount: 1);
    private static GitLabDockerContainer s_instance;

    public string Host { get; private set; } = "localhost";

    public int HttpPort { get; private set; } = 48624;

    public string AdminUserName { get; } = "root";

    public static string AdminPassword
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("GITLAB_ROOT_PASSWORD");
            if (!string.IsNullOrEmpty(env))
                return env;

            return "Pa$$w0rd";
        }
    }

    public string LicenseFile { get; set; }

    public Uri GitLabUrl => new("http://" + Host + ":" + HttpPort.ToString(CultureInfo.InvariantCulture));

    public GitLabCredential Credentials { get; set; }

    public static async Task<GitLabDockerContainer> GetOrCreateInstance()
    {
        await s_setupLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (s_instance == null)
            {
                if (s_creationErrorMessage != null)
                {
                    Assert.Fail(s_creationErrorMessage);
                }

                try
                {
                    var instance = new GitLabDockerContainer();
                    await instance.SetupAsync().ConfigureAwait(false);
                    s_instance = instance;
                }
                catch (Exception ex)
                {
                    s_creationErrorMessage = ex.ToString();
                    throw;
                }
            }

            return s_instance;
        }
        finally
        {
            s_setupLock.Release();
        }
    }

    private async Task SetupAsync()
    {
        if (GitLabTestContext.IsContinuousIntegration())
        {
            await WaitForCiGitLabInstance().ConfigureAwait(false);
        }
        else
        {
            await SpawnDockerContainerAsync().ConfigureAwait(false);
        }

        EnsureChromiumIsInstalled();

        // Use Playwright to launch Chromium
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            // Headless = false,   // Uncomment to have browser window visible
            // SlowMo = 1000,      // Slows down Playwright operations by the specified amount of ms.
        });
        await using var browserContext = await browser.NewContextAsync();

        await LoginAsync(browserContext);
        await ResolveGitLabVersionAsync(browserContext).ConfigureAwait(false);

        await LoadCredentialsAsync().ConfigureAwait(false);

        if (Credentials != null)
        {
            Console.WriteLine("Using credentials from persisted credential file");
            return;
        }

        await GenerateCredentialsAsync(browserContext).ConfigureAwait(false);
        PersistCredentialsAsync();

        static void EnsureChromiumIsInstalled()
        {
            TestContext.Progress.WriteLine("Making sure Chromium is installed");

            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "--force", "chromium", "--with-deps" });
            if (exitCode != 0)
                throw new InvalidOperationException($"Cannot install browser (exit code: {exitCode})");

            TestContext.Progress.WriteLine("Chromium installed");
        }
    }

    private static async Task ValidateDockerIsEnabled(DockerClient client)
    {
        try
        {
            await client.Images.ListImagesAsync(new ImagesListParameters()).ConfigureAwait(false);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.Message.StartsWith("The added or subtracted value results in an un-representable DateTime.", StringComparison.Ordinal))
        {
            // Ignore https://github.com/rancher-sandbox/rancher-desktop/issues/5145
        }
        catch (Exception ex)
        {
            s_creationErrorMessage = "Cannot connect to Docker service. Make sure it's running on your machine before launching any tests.\nDetails: " + ex;
            Assert.Fail(s_creationErrorMessage);
        }
    }

    private async Task SpawnDockerContainerAsync()
    {
        Console.WriteLine($"Executing tests locally. Spawning GitLab docker image version '{LocalGitLabDockerVersion}'");
        using var httpClient = new HttpClient();

        // Spawn the container
        // https://docs.gitlab.com/omnibus/settings/configuration.html
        using var conf = new DockerClientConfiguration(new Uri(OperatingSystem.IsWindows() ? "npipe://./pipe/docker_engine" : "unix:///var/run/docker.sock"));
        using var client = conf.CreateClient();
        await ValidateDockerIsEnabled(client);

        TestContext.Progress.WriteLine("Looking up GitLab Docker containers");
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true }).ConfigureAwait(false);
        var container = containers.FirstOrDefault(c => c.Names.Contains("/" + ContainerName, StringComparer.Ordinal));
        if (container != null)
        {
            TestContext.Progress.WriteLine("Verifying if the GitLab Docker container is using the right image");
            var inspect = await client.Containers.InspectContainerAsync(container.ID).ConfigureAwait(false);
            var inspectImage = await client.Images.InspectImageAsync(ImageName + ":" + LocalGitLabDockerVersion).ConfigureAwait(false);
            if (inspect.Image != inspectImage.ID)
            {
                TestContext.Progress.WriteLine("Ending GitLab Docker container, as it's using the wrong image");
                await client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters { Force = true }).ConfigureAwait(false);
                container = null;
            }
        }

        if (container == null)
        {
            // Download GitLab images
            TestContext.Progress.WriteLine("Making sure the right GitLab Docker image is available locally");
            await client.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = ImageName, Tag = LocalGitLabDockerVersion }, new AuthConfig(), new Progress<JSONMessage>()).ConfigureAwait(false);

            // Create the container
            TestContext.Progress.WriteLine("Creating the GitLab Docker container");
            var hostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>(StringComparer.Ordinal)
                {
                    {  HttpPort.ToString(CultureInfo.InvariantCulture) + "/tcp", new List<PortBinding> { new PortBinding { HostPort = HttpPort.ToString(CultureInfo.InvariantCulture) } } },
                },
            };

            var response = await client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Hostname = "localhost",
                Image = ImageName + ":" + LocalGitLabDockerVersion,
                Name = ContainerName,
                Tty = false,
                HostConfig = hostConfig,
                ExposedPorts = new Dictionary<string, EmptyStruct>(StringComparer.Ordinal)
                {
                    { HttpPort.ToString(CultureInfo.InvariantCulture) + "/tcp", default },
                },
                Env =
                [
                    "GITLAB_OMNIBUS_CONFIG=external_url 'http://localhost:" + HttpPort.ToString(CultureInfo.InvariantCulture) + "/'",
                    "GITLAB_ROOT_PASSWORD=" + AdminPassword,
                ],
            }).ConfigureAwait(false);

            containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true }).ConfigureAwait(false);
            container = containers.First(c => c.ID == response.ID);
        }

        // Start the container
        if (container.State != "running")
        {
            TestContext.Progress.WriteLine("Starting the GitLab Docker container");
            var started = await client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters()).ConfigureAwait(false);
            if (!started)
            {
                Assert.Fail("Cannot start the Docker container");
            }
        }

        // Wait for the container to be ready.
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            TestContext.Progress.WriteLine($@"Waiting for the GitLab Docker container to be ready ({stopwatch.Elapsed:mm\:ss})");
            var status = await client.Containers.InspectContainerAsync(container.ID);
            if (!status.State.Running)
                throw new InvalidOperationException($"Container '{status.ID}' is not running");

            var healthState = status.State.Health.Status;

            // unhealthy is valid as long as the container is running as it may indicate a slow creation
            if (healthState is "starting" or "unhealthy")
            {
            }
            else if (healthState is "healthy")
            {
                // A healthy container doesn't mean the service is actually running.
                // GitLab has lots of configuration steps that are still running when the container is healthy.
                try
                {
                    using var response = await httpClient.GetAsync(GitLabUrl).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                        break;
                }
                catch
                {
                }
            }
            else
            {
                throw new InvalidOperationException($"Container status '{healthState}' is not supported");
            }

            await Task.Delay(5000);
        }

        TestContext.Progress.WriteLine("GitLab Docker container is ready");
    }

    private async Task GenerateCredentialsAsync(IBrowserContext browserContext)
    {
        Console.WriteLine("Requesting credentials from GitLab instance");

        var credentials = new GitLabCredential();
        await GenerateAdminToken(credentials).ConfigureAwait(false);
        if (credentials.AdminUserToken != null)
        {
            GenerateUserToken();
        }

        Credentials = credentials;

        async Task GenerateAdminToken(GitLabCredential credentials)
        {
            TestContext.Progress.WriteLine("Generating Credentials");

            var gitLabVersionAsNuGetVersion = NuGetVersion.Parse(ResolvedGitLabVersion);
            var isMajorVersion15 = VersionRange.Parse("[15.0,16.0)").Satisfies(gitLabVersionAsNuGetVersion);
            var isMajorVersionAtLeast16 = VersionRange.Parse("[16.0,)").Satisfies(gitLabVersionAsNuGetVersion);

            TestContext.Progress.WriteLine("Creating root token");

            var page = await browserContext.NewPageAsync();
            await page.GotoAsync(GitLabUrl + "/-/profile/personal_access_tokens");

            var formLocator = page.Locator("main#content-body form");

            var tokenName = "GitLabClientTest-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

            if (isMajorVersion15)
            {
                // Try the "old" 15.x.y way
                formLocator = page.Locator("main#content-body form");
                await formLocator.GetByLabel("Token name").FillAsync(tokenName);
            }
            else if (isMajorVersionAtLeast16)
            {
                await SkipVersionReminder(page);

                await page.Locator("main[id='content-body'] button[data-testid='add-new-token-button']").ClickAsync(new LocatorClickOptions { Timeout = 5_000 });
                formLocator = page.Locator("main[id='content-body'] form[id='js-new-access-token-form']");
                await formLocator.Locator("input[data-testid='access-token-name-field']").FillAsync(tokenName);
            }
            else
            {
                s_creationErrorMessage = $"Unable to generate an admin token: resolved GitLab version '{ResolvedGitLabVersion}' doesn't match any supported range in '{nameof(GenerateCredentialsAsync)}'.";
                Assert.Fail(s_creationErrorMessage);
            }

            foreach (var checkbox in await formLocator.GetByRole(AriaRole.Checkbox).AllAsync())
            {
                await checkbox.CheckAsync(new LocatorCheckOptions { Force = true });
            }

            await formLocator.GetByRole(AriaRole.Button, new() { Name = "Create personal access token" }).ClickAsync();

            var token = await page.Locator("button[title='Copy personal access token']").GetAttributeAsync("data-clipboard-text");
            credentials.AdminUserToken = token;

            // Get admin login cookie
            // result.Cookie: experimentation_subject_id=XXX; _gitlab_session=XXXX; known_sign_in=XXXX
            TestContext.Progress.WriteLine("Extracting GitLab session cookie");
            var cookies = await browserContext.CookiesAsync(new[] { GitLabUrl.AbsoluteUri });
            foreach (var cookie in cookies)
            {
                if (cookie.Name == "_gitlab_session")
                {
                    credentials.AdminCookies = cookie.Value;
                    break;
                }
            }
        }

        void GenerateUserToken()
        {
            var retryPolicy = Policy.Handle<GitLabException>().WaitAndRetry(10, _ => TimeSpan.FromSeconds(1));
            var client = new GitLabClient(GitLabUrl.ToString(), credentials.AdminUserToken);
            var user = retryPolicy.Execute(() => client.Users.Get("common_user")).FirstOrDefault();
            if (user == null)
            {
                try
                {
                    user = retryPolicy.Execute(() => client.Users.Create(new UserUpsert
                    {
                        Username = "common_user",
                        Email = "common_user@example.com",
                        IsAdmin = false,
                        Name = "common_user",
                        SkipConfirmation = true,
                        ResetPassword = false,
                        Password = AdminPassword,
                        IsPrivateProfile = true, // Set profile to private for LastActivity test cases
                    }));
                }
                catch (GitLabException)
                {
                    user = retryPolicy.Execute(() => client.Users.Get("common_user")).FirstOrDefault();
                    if (user == null)
                        throw new InvalidOperationException("Cannot create the common user");
                }
            }

            var token = retryPolicy.Execute(() => client.Users.CreateToken(new UserTokenCreate
            {
                UserId = user.Id,
                Name = "common_user",
                Scopes = new[] { "api" },
                ExpiresAt = DateTime.UtcNow.AddDays(7),
            }));

            credentials.UserToken = token.Token;
        }
    }

    private static async Task SkipVersionReminder(IPage page)
    {
        try
        {
            await page.Locator("button[data-testid='alert-modal-remind-button']").ClickAsync(new LocatorClickOptions { Timeout = 3_000 });
        }
        catch (Exception)
        {
        }
    }

    private void PersistCredentialsAsync()
    {
        var path = GetCredentialsFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var json = JsonSerializer.Serialize(Credentials);
        File.WriteAllText(path, json);
    }

    private async Task LoadCredentialsAsync()
    {
        var file = GetCredentialsFilePath();
        if (File.Exists(file))
        {
            var json = File.ReadAllText(file);
            var credentials = JsonSerializer.Deserialize<GitLabCredential>(json);
            if (credentials.AdminUserToken == null || credentials.UserToken == null)
                return;

            var client = new GitLabClient(GitLabUrl.ToString(), credentials.AdminUserToken);
            try
            {
                // Validate token
                var user = client.Users.Current;

                using var httpClient = new HttpClient
                {
                    BaseAddress = GitLabUrl,
                    DefaultRequestHeaders =
                    {
                        { "Cookie", "_gitlab_session=" + credentials.AdminCookies },
                    },
                };
                var response = await httpClient.GetAsync(new Uri("/", UriKind.RelativeOrAbsolute));
                if (response.RequestMessage.RequestUri.PathAndQuery == "/users/sign_in")
                    return;

                // Validate cookie
                Credentials = credentials;
            }
            catch (GitLabException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
            }
        }
    }

    private static string GetCredentialsFilePath()
    {
        return Path.Combine(Path.GetTempPath(), "ngitlab", "credentials.json");
    }

    private async Task WaitForCiGitLabInstance()
    {
        Console.WriteLine($"Executing tests on CI. Checking GitLab instance...");

        using var httpClient = new HttpClient();
        Console.WriteLine("Testing " + GitLabUrl);

        var now = Stopwatch.StartNew();
        while (now.Elapsed < TimeSpan.FromMinutes(10))
        {
            try
            {
                var result = await httpClient.GetStringAsync(GitLabUrl).ConfigureAwait(false);
                return;
            }
            catch
            {
            }

            await Task.Delay(1000);
        }

        s_creationErrorMessage = "GitLab is not well configured in CI";
        Assert.Fail(s_creationErrorMessage);
    }

    private async Task ResolveGitLabVersionAsync(IBrowserContext browserContext)
    {
        Console.WriteLine("Resolving GitLab version from help page...");
        var page = await browserContext.NewPageAsync();
        await page.GotoAsync(new Uri(GitLabUrl, "help").AbsoluteUri);
        var titleLink = await page.QuerySelectorAsync("h1 a");

        if (titleLink is null)
        {
            s_creationErrorMessage = "Cannot find title on the help page to get GitLab version";
            Assert.Fail(s_creationErrorMessage);
        }

        var version = await titleLink.TextContentAsync();

        if (string.IsNullOrEmpty(version))
        {
            s_creationErrorMessage = "Found title on the help page, but the version is empty";
            Assert.Fail(s_creationErrorMessage);
        }

        ResolvedGitLabVersion = version.Trim().TrimStart('v');
        Console.WriteLine($"GitLab resolved version is '{ResolvedGitLabVersion}'");
    }

    private async Task LoginAsync(IBrowserContext browserContext)
    {
        var page = await browserContext.NewPageAsync();
        await page.GotoAsync(GitLabUrl.AbsoluteUri);
        var url = await GetCurrentUrl(page);

        if (url != "/users/sign_in")
        {
            Console.WriteLine("Already logged in on GitLab instance");
            return;
        }

        Console.WriteLine("Logging in on GitLab instance...");

        var v15LoginInput = "form#new_user input[name='user[login]']";
        var v16LoginInput = "form[data-testid='sign-in-form'] input[name='user[login]']";

        if (await page.QuerySelectorAsync(v15LoginInput) is not null)
        {
            await page.Locator(v15LoginInput).FillAsync(AdminUserName);
            await page.Locator("form#new_user input[name='user[password]']").FillAsync(AdminPassword);
        }
        else if (await page.QuerySelectorAsync(v16LoginInput) is not null)
        {
            await page.Locator(v16LoginInput).FillAsync(AdminUserName);
            await page.Locator("form[data-testid='sign-in-form'] input[name='user[password]']").FillAsync(AdminPassword);
        }
        else
        {
            s_creationErrorMessage = $"Unable to find the correct login input. Please make sure that login form for the GitLab version you target is supported in '{nameof(LoginAsync)}'";
            Assert.Fail(s_creationErrorMessage);
        }

        var checkbox = page.Locator("form[data-testid='sign-in-form'] input[type=checkbox][name='user[remember_me]']");
        await checkbox.CheckAsync(new LocatorCheckOptions { Force = true });

        await page.RunAndWaitForResponseAsync(async () =>
        {
            await page.EvalOnSelectorAsync("form[data-testid='sign-in-form']", "form => form.submit()");
        }, response => response.Status == 200);
    }

    private static Task<string> GetCurrentUrl(IPage page) => page.EvaluateAsync<string>("window.location.pathname");
}
