﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
#if NET45
using System.Reflection;
#endif

namespace NGitLab.Impl
{
    /// <summary>
    /// The requestor is typically used for a single call to gitlab.
    /// </summary>
    public partial class HttpRequestor : IHttpRequestor
    {
        private readonly RequestOptions _options;
        private readonly MethodType _methodType;
        private object _data;

        private readonly string _apiToken;
        private readonly string _hostUrl;

        static HttpRequestor()
        {
            // By default only Sssl and Tls 1.0 is enabled with .NET 4.5
            // We add Tls 1.2 and Tls 1.2 without affecting the other values in case new protocols are added in the future
            // (see https://stackoverflow.com/questions/28286086/default-securityprotocol-in-net-4-5)
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

        public HttpRequestor(string hostUrl, string apiToken, MethodType methodType)
            : this(hostUrl, apiToken, methodType, RequestOptions.Default)
        {
        }

        public HttpRequestor(string hostUrl, string apiToken, MethodType methodType, RequestOptions options)
        {
            _hostUrl = hostUrl.EndsWith("/", StringComparison.Ordinal) ? hostUrl.Replace("/$", string.Empty) : hostUrl;
            _apiToken = apiToken;
            _methodType = methodType;
            _options = options;
        }

        public IHttpRequestor With(object data)
        {
            _data = data;
            return this;
        }

        public virtual void Execute(string tailAPIUrl)
        {
            Stream(tailAPIUrl, parser: null);
        }

        public virtual T To<T>(string tailAPIUrl)
        {
            var result = default(T);
            Stream(tailAPIUrl, s =>
            {
                var json = new StreamReader(s).ReadToEnd();
                result = SimpleJson.DeserializeObject<T>(json);
            });
            return result;
        }

        public Uri GetAPIUrl(string tailAPIUrl)
        {
            if (!tailAPIUrl.StartsWith("/", StringComparison.Ordinal))
            {
                tailAPIUrl = "/" + tailAPIUrl;
            }

            return UriFix.Build(_hostUrl + tailAPIUrl);
        }

        public Uri GetUrl(string tailAPIUrl)
        {
            if (!tailAPIUrl.StartsWith("/", StringComparison.Ordinal))
            {
                tailAPIUrl = "/" + tailAPIUrl;
            }

            return UriFix.Build(_hostUrl + tailAPIUrl);
        }

        public virtual void Stream(string tailAPIUrl, Action<Stream> parser)
        {
            var request = new GitLabRequest(GetAPIUrl(tailAPIUrl), _methodType, _data, _apiToken, _options.Sudo);

            using var response = request.GetResponse(_options);
            if (parser != null)
            {
                using var stream = response.GetResponseStream();
                parser(stream);
            }
        }

        public virtual IEnumerable<T> GetAll<T>(string tailUrl)
        {
            return new Enumerable<T>(_apiToken, GetAPIUrl(tailUrl), _options);
        }

        private sealed class Enumerable<T> : IEnumerable<T>
        {
            private readonly string _apiToken;
            private readonly RequestOptions _options;
            private readonly Uri _startUrl;

            public Enumerable(string apiToken, Uri startUrl, RequestOptions options)
            {
                _apiToken = apiToken;
                _startUrl = startUrl;
                _options = options;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return new Enumerator(_apiToken, _startUrl, _options);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private sealed class Enumerator : IEnumerator<T>
            {
                private readonly string _apiToken;
                private readonly RequestOptions _options;
                private readonly List<T> _buffer = new();

                private Uri _nextUrlToLoad;
                private int _index;

                public Enumerator(string apiToken, Uri startUrl, RequestOptions options)
                {
                    _apiToken = apiToken;
                    _nextUrlToLoad = startUrl;
                    _options = options;
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (++_index < _buffer.Count)
                        return true;

                    if (_nextUrlToLoad == null)
                        return false;

                    // Empty the buffer and get next batch from GitLab, if any
                    _index = 0;
                    _buffer.Clear();

                    var request = new GitLabRequest(_nextUrlToLoad, MethodType.Get, data: null, _apiToken, _options.Sudo);
                    using (var response = request.GetResponse(_options))
                    {
                        // <http://localhost:1080/api/v3/projects?page=2&per_page=0>; rel="next", <http://localhost:1080/api/v3/projects?page=1&per_page=0>; rel="first", <http://localhost:1080/api/v3/projects?page=2&per_page=0>; rel="last"
                        var link = response.Headers["Link"] ?? response.Headers["Links"];

                        string[] nextLink = null;
                        if (!string.IsNullOrEmpty(link))
                        {
                            nextLink = link.Split(',')
                               .Select(l => l.Split(';'))
                               .FirstOrDefault(pair => pair[1].Contains("next"));
                        }

                        _nextUrlToLoad = (nextLink != null) ? new Uri(nextLink[0].Trim('<', '>', ' ')) : null;

                        var stream = response.GetResponseStream();
                        var responseText = new StreamReader(stream).ReadToEnd();
                        var deserialized = SimpleJson.DeserializeObject<T[]>(responseText);

                        _buffer.AddRange(deserialized);
                    }

                    return _buffer.Count > 0;
                }

                public void Reset()
                {
                    throw new NotSupportedException();
                }

                public T Current => _buffer[_index];

                object IEnumerator.Current => Current;
            }
        }
    }

    /// <summary>
    /// .Net framework has a bug which converts the escaped / into normal slashes
    /// This is not equivalent and fails when retrieving a project with its full name for example.
    /// There is an ugly workaround which involves reflection but it better than nothing.
    /// </summary>
    /// <remarks>
    /// http://stackoverflow.com/questions/2320533/system-net-uri-with-urlencoded-characters/
    /// </remarks>
    internal static class UriFix
    {
        static UriFix()
        {
            LeaveDotsAndSlashesEscaped();
        }

        public static Uri Build(string asString)
        {
            return new Uri(asString);
        }

        public static void LeaveDotsAndSlashesEscaped()
        {
#if NET45
            var getSyntaxMethod =
                typeof(UriParser).GetMethod("GetSyntax", BindingFlags.Static | BindingFlags.NonPublic);
            if (getSyntaxMethod == null)
            {
                throw new MissingMethodException("UriParser", "GetSyntax");
            }

            var uriParser = getSyntaxMethod.Invoke(null, new object[] { "https" });

            var setUpdatableFlagsMethod =
                uriParser.GetType().GetMethod("SetUpdatableFlags", BindingFlags.Instance | BindingFlags.NonPublic);
            if (setUpdatableFlagsMethod == null)
            {
                throw new MissingMethodException("UriParser", "SetUpdatableFlags");
            }

            setUpdatableFlagsMethod.Invoke(uriParser, new object[] { 0 });
#endif
        }
    }
}
