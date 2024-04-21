using System;
using System.Net;

namespace NGitLab;

public sealed class GitLabRequestResult
{
    public HttpWebRequest Request { get; set; }

    public WebResponse Response { get; set; }

    public Exception Exception { get; set; }
}
