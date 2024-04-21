using System;
using System.IO;
using System.Net;
using NGitLab.Impl;

namespace NGitLab;

/// <summary>
/// Allows to override how NGitLab executes queries to the GitLab server,
/// each http call goes in that handler.
/// </summary>
public class RequestOptions
{
    public int RetryCount { get; set; }

    public TimeSpan RetryInterval { get; set; }

    public bool IsIncremental { get; set; }

    /// <summary>
    /// ID or case-insensitive username of the user to impersonate, if any
    /// </summary>
    public string Sudo { get; set; }

    /// <summary>
    /// Configure the default client side timeout when calling GitLab.
    /// GitLab exposes some end points which are really slow so
    /// the default we use is larger than the default 100 seconds of .net
    /// </summary>
    public TimeSpan HttpClientTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public string UserAgent { get; set; }

    public RequestOptions(int retryCount, TimeSpan retryInterval, bool isIncremental = true)
    {
        RetryCount = retryCount;
        RetryInterval = retryInterval;
        IsIncremental = isIncremental;
    }

    /// <summary>
    /// Predicate indicating, after a request has failed, if a new attempt should occur
    /// </summary>
    /// <param name="ex">Exception thrown by the failed request</param>
    /// <param name="retryNumber">Number of retries left</param>
    /// <returns>Whether the request should be retried</returns>
    public virtual bool ShouldRetry(Exception ex, int retryNumber)
    {
        if (ex is not GitLabException gitLabException)
            return false;

        // For requests that are potentially NOT Safe/Idempotent, do not retry
        // See https://developer.mozilla.org/en-US/docs/Glossary/Safe/HTTP
        // If there is no HTTP request method specified, carry on the predicate assessment.
        if (gitLabException.MethodType.HasValue &&
            gitLabException.MethodType != MethodType.Get &&
            gitLabException.MethodType != MethodType.Head &&
            gitLabException.MethodType != MethodType.Options)
        {
            return false;
        }

        // Use the same Transient HTTP StatusCodes as Polly's HttpPolicyExtensions
        // https://github.com/App-vNext/Polly.Extensions.Http/blob/69fd292bc603cb3032e57b028522737255f03a49/src/Polly.Extensions.Http/HttpPolicyExtensions.cs#L14
        return gitLabException.StatusCode >= HttpStatusCode.InternalServerError ||
               gitLabException.StatusCode == HttpStatusCode.RequestTimeout;
    }

    public static RequestOptions Default => new(retryCount: 0, retryInterval: TimeSpan.Zero);

    /// <summary>
    /// Allows to monitor GitLab requests from the caller library
    /// </summary>
    public virtual void ProcessGitLabRequestResult(GitLabRequestResult e)
    {
    }

    internal virtual Stream GetRequestStream(HttpWebRequest request)
    {
        return request.GetRequestStream();
    }
}
