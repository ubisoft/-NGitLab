#pragma warning disable CS0672  // Member overrides obsolete member
#pragma warning disable SYSLIB0010 // Member overrides obsolete member
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NGitLab.Tests.Docker;

/// <summary>
/// Store and logs requests
/// </summary>
internal sealed class GitLabTestContextRequestOptions : RequestOptions
{
    private readonly List<WebRequest> _allRequests = [];
    private static readonly SemaphoreSlim s_semaphoreSlim = new(1, 1);

    private readonly ConcurrentDictionary<WebRequest, LoggableRequestStream> _pendingRequest = new();

    public IReadOnlyList<WebRequest> AllRequests => _allRequests;

    public GitLabTestContextRequestOptions()
        : base(retryCount: 0, retryInterval: TimeSpan.FromSeconds(1), isIncremental: true)
    {
        UserAgent = "NGitLab.Tests/1.0.0";
    }

    public override WebResponse GetResponse(HttpWebRequest request)
    {
        lock (_allRequests)
        {
            _allRequests.Add(request);
        }

        WebResponse response = null;

        // GitLab is unstable, so let's make sure we don't overload it with many concurrent requests
        s_semaphoreSlim.Wait();
        try
        {
            try
            {
                response = base.GetResponse(request);
            }
            catch (WebException exception)
            {
                response = exception.Response;
                if (response is HttpWebResponse webResponse)
                {
                    response = new LoggableHttpWebResponse(webResponse);
                    throw new WebException(exception.Message, exception, exception.Status, response);
                }

                throw;
            }
            finally
            {
                response = LogRequest(request, response);
            }
        }
        finally
        {
            s_semaphoreSlim.Release();
        }

        return response;
    }

    public override async Task<WebResponse> GetResponseAsync(HttpWebRequest request, CancellationToken cancellationToken)
    {
        lock (_allRequests)
        {
            _allRequests.Add(request);
        }

        WebResponse response = null;

        // GitLab is unstable, so let's make sure we don't overload it with many concurrent requests
        await s_semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                response = await base.GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (WebException exception)
            {
                response = exception.Response;
                if (response is HttpWebResponse webResponse)
                {
                    response = new LoggableHttpWebResponse(webResponse);
                    throw new WebException(exception.Message, exception, exception.Status, response);
                }

                throw;
            }
            finally
            {
                response = LogRequest(request, response);
            }
        }
        finally
        {
            s_semaphoreSlim.Release();
        }

        return response;
    }

    private WebResponse LogRequest(HttpWebRequest request, WebResponse response)
    {
        byte[] requestContent = null;
        if (_pendingRequest.TryRemove(request, out var requestStream))
        {
            requestContent = requestStream.GetRequestContent();
        }

        var sb = new StringBuilder();
        sb.Append(request.Method);
        sb.Append(' ');
        sb.Append(request.RequestUri);
        sb.AppendLine();
        LogHeaders(sb, request.Headers);
        if (requestContent != null)
        {
            sb.AppendLine();

            if (string.Equals(request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(Encoding.UTF8.GetString(requestContent));
            }
            else
            {
                sb.Append("Binary data: ").Append(requestContent.Length).AppendLine(" bytes");
            }

            sb.AppendLine();
        }

        if (response != null)
        {
            sb.AppendLine("----------");

            if (response.ResponseUri != request.RequestUri)
            {
                sb.Append(request.RequestUri).AppendLine();
            }

            if (response is HttpWebResponse webResponse)
            {
                sb.Append((int)webResponse.StatusCode).Append(' ').AppendLine(webResponse.StatusCode.ToString());
                LogHeaders(sb, response.Headers);
                if (string.Equals(webResponse.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    // This response allows multiple reads, so NGitLab can also read the response
                    // AllowResponseBuffering does not seem to work for WebException.Response
                    response = new LoggableHttpWebResponse(webResponse);
                    sb.AppendLine();
                    using var responseStream = response.GetResponseStream();
                    using var sr = new StreamReader(responseStream);
                    var responseText = sr.ReadToEnd();
                    sb.AppendLine(responseText);
                }
                else
                {
                    sb.Append("Binary data: ").Append(response.ContentLength).AppendLine(" bytes");
                }
            }
        }

        var logs = sb.ToString();
        TestContext.Out.WriteLine(new string('-', 100) + "\nGitLab request: " + logs);
        return response;
    }

    internal override Stream GetRequestStream(HttpWebRequest request)
    {
        var stream = new LoggableRequestStream(request.GetRequestStream());
        _pendingRequest.AddOrUpdate(request, stream, (_, _) => stream);
        return stream;
    }

    private static void LogHeaders(StringBuilder sb, WebHeaderCollection headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var headerName = headers.GetKey(i);
            if (headerName == null)
                continue;

            var headerValues = headers.GetValues(i);
            if (headerValues == null)
                continue;

            foreach (var headerValue in headerValues)
            {
                sb.Append(headerName).Append(": ");
                if (string.Equals(headerName, "Private-Token", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("******");
                }
                else if (string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    const string BearerTokenPrefix = "Bearer ";
                    if (headerValue.StartsWith(BearerTokenPrefix, StringComparison.Ordinal))
                        sb.Append(BearerTokenPrefix);
                    sb.AppendLine("******");
                }
                else
                {
                    sb.AppendLine(headerValue);
                }
            }
        }
    }

    private sealed class LoggableHttpWebResponse : HttpWebResponse
    {
        private readonly HttpWebResponse _innerWebResponse;
        private byte[] _stream;

        [Obsolete("We have to use it")]
        public LoggableHttpWebResponse(HttpWebResponse innerWebResponse)
        {
            _innerWebResponse = innerWebResponse;
        }

        public override long ContentLength => _innerWebResponse.ContentLength;

        public override string ContentType => _innerWebResponse.ContentType;

        public override CookieCollection Cookies
        {
            get => _innerWebResponse.Cookies;
            set => _innerWebResponse.Cookies = value;
        }

        public override WebHeaderCollection Headers => _innerWebResponse.Headers;

        public override bool IsFromCache => _innerWebResponse.IsFromCache;

        public override bool IsMutuallyAuthenticated => _innerWebResponse.IsMutuallyAuthenticated;

        public override string Method => _innerWebResponse.Method;

        public override Uri ResponseUri => _innerWebResponse.ResponseUri;

        public override HttpStatusCode StatusCode => _innerWebResponse.StatusCode;

        public override string StatusDescription => _innerWebResponse.StatusDescription;

        public override bool SupportsHeaders => _innerWebResponse.SupportsHeaders;

        public override bool Equals(object obj)
        {
            return _innerWebResponse.Equals(obj);
        }

        public override int GetHashCode()
        {
            return _innerWebResponse.GetHashCode();
        }

        public override void Close()
        {
            _innerWebResponse.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerWebResponse.Dispose();
            }

            base.Dispose(disposing);
        }

        public override string ToString()
        {
            return _innerWebResponse.ToString();
        }

        public override object InitializeLifetimeService()
        {
            return _innerWebResponse.InitializeLifetimeService();
        }

        public override Stream GetResponseStream()
        {
            if (_stream == null)
            {
                using var ms = new MemoryStream();
                using var responseStream = _innerWebResponse.GetResponseStream();
                responseStream.CopyTo(ms);

                _stream = ms.ToArray();
            }

            var result = new MemoryStream(_stream);
            return result;
        }
    }

    private sealed class LoggableRequestStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly MemoryStream _memoryStream = new();

        public override bool CanRead => _innerStream.CanRead;

        public override bool CanSeek => _innerStream.CanSeek;

        public override bool CanWrite => _innerStream.CanWrite;

        public override long Length => _innerStream.Length;

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public LoggableRequestStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        public byte[] GetRequestContent()
        {
            return _memoryStream.ToArray();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
            _memoryStream.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _innerStream.Write(buffer);
            _memoryStream.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            _innerStream.WriteByte(value);
            _memoryStream.WriteByte(value);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _innerStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await _memoryStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            _innerStream.Dispose();
            _memoryStream.Dispose();
            base.Dispose(disposing);
        }

        public override void Flush()
        {
            _innerStream.Flush();
            _memoryStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            _innerStream.SetLength(value);
            _memoryStream.SetLength(value);
        }
    }
}
