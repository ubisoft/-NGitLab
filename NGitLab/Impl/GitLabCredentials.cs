using System;

namespace NGitLab.Impl;

/// <summary>
/// To connect to GitLab, you need a token.
/// You can either set one in your profile or ask for one on the fly.
/// </summary>
/// <remarks>
/// The api token will be resolved only when required, to make sure no cost is
/// incurred until credentials are really needed.
/// </remarks>
public class GitLabCredentials
{
    private string _apiToken;

    internal AuthenticationMethod AuthenticationMethod { get; }

    public string HostUrl { get; }

    public string UserName { get; set; }

    public string Password { get; set; }

    internal GitLabCredentials(string hostUrl)
    {
        if (string.IsNullOrEmpty(hostUrl))
            throw new ArgumentException("HostUrl is mandatory", nameof(hostUrl));

        ValidateHostUrl(hostUrl);

        HostUrl = GetApiUrl(hostUrl);
        AuthenticationMethod = AuthenticationMethod.None;
    }

    public GitLabCredentials(string hostUrl, string apiToken)
    {
        if (string.IsNullOrEmpty(hostUrl))
            throw new ArgumentException("HostUrl is mandatory", nameof(hostUrl));
        if (string.IsNullOrEmpty(apiToken))
            throw new ArgumentException("Token is mandatory", nameof(apiToken));

        ValidateHostUrl(hostUrl);

        HostUrl = GetApiUrl(hostUrl);
        _apiToken = apiToken;
        AuthenticationMethod = AuthenticationMethod.ApiKey;
    }

    public GitLabCredentials(string hostUrl, string userName, string password)
    {
        if (string.IsNullOrEmpty(hostUrl))
            throw new ArgumentException("HostUrl is mandatory", nameof(hostUrl));
        if (string.IsNullOrEmpty(userName))
            throw new ArgumentException("UserName is mandatory", nameof(userName));
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password is mandatory", nameof(password));

        ValidateHostUrl(hostUrl);

        HostUrl = GetApiUrl(hostUrl);
        UserName = userName;
        Password = password;
        AuthenticationMethod = AuthenticationMethod.UsernamePassword;
    }

    public string ApiToken
    {
        get => _apiToken;
        set
        {
            _apiToken = value ?? throw new ArgumentNullException(nameof(value));

            // Passwords and user names and not useful anymore.
            UserName = null;
            Password = null;
        }
    }

    private static void ValidateHostUrl(string url)
    {
        if (url.EndsWith("/api/v3", StringComparison.OrdinalIgnoreCase) ||
            url.EndsWith("/api/v3/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("API v3 endpoint is not supported.");
        }
    }

    private static string GetApiUrl(string url)
    {
        url = url.TrimEnd('/');
        if (url.EndsWith("/api/v4", StringComparison.OrdinalIgnoreCase))
            return url[..^"/api/v4".Length];

        return url;
    }
}
