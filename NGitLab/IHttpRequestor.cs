using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NGitLab.Models;

namespace NGitLab;

public interface IHttpRequestor
{
    IReadOnlyDictionary<string, IEnumerable<string>> GetHeader(string tailAPIUrl);

    IEnumerable<T> GetAll<T>(string tailUrl);

    GitLabCollectionResponse<T> GetAllAsync<T>(string tailUrl);

    PagedResponse<T> Page<T>(string tailAPIUrl);

    Task<PagedResponse<T>> PageAsync<T>(string tailAPIUrl, CancellationToken cancellationToken);

    void Stream(string tailAPIUrl, Action<Stream> parser);

    void StreamAndHeaders(string tailAPIUrl, Action<Stream, IReadOnlyDictionary<string, IEnumerable<string>>> parser);

    Task StreamAsync(string tailAPIUrl, Func<Stream, Task> parser, CancellationToken cancellationToken);

    Task StreamAndHeadersAsync(string tailAPIUrl, Func<Stream, IReadOnlyDictionary<string, IEnumerable<string>>, Task> parser, CancellationToken cancellationToken);

    T To<T>(string tailAPIUrl);

    Task<T> ToAsync<T>(string tailAPIUrl, CancellationToken cancellationToken);

    void Execute(string tailAPIUrl);

    Task ExecuteAsync(string tailAPIUrl, CancellationToken cancellationToken);

    IHttpRequestor With(object data);
}
