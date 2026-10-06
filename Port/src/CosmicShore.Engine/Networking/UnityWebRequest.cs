using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// UnityEngine.Networking.UnityWebRequest over <see cref="HttpClient"/>. The request
    /// runs off-thread; completion (result, response code, downloaded bytes, the
    /// operation's <c>completed</c> callback) is marshalled back onto the synchronization
    /// context that called <see cref="SendWebRequest"/> — the game loop's context during a
    /// frame — so, as in the original, everything a caller observes changes on the main thread.
    /// </summary>
    public class UnityWebRequest : IDisposable
    {
        public const string kHttpVerbGET = "GET";
        public const string kHttpVerbHEAD = "HEAD";
        public const string kHttpVerbPOST = "POST";
        public const string kHttpVerbPUT = "PUT";
        public const string kHttpVerbCREATE = "CREATE";
        public const string kHttpVerbDELETE = "DELETE";

        public enum Result { InProgress = 0, Success = 1, ConnectionError = 2, ProtocolError = 3, DataProcessingError = 4 }

        static readonly HttpClient s_client = new() { Timeout = Timeout.InfiniteTimeSpan };

        readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> _responseHeaders;
        UnityWebRequestAsyncOperation _operation;
        CancellationTokenSource _abort;

        public string url { get; set; }
        public Uri uri { get => url == null ? null : new Uri(url); set => url = value?.ToString(); }
        public string method { get; set; } = kHttpVerbGET;
        /// <summary>Seconds before the request is aborted (0 = no timeout).</summary>
        public int timeout { get; set; }
        public UploadHandler uploadHandler { get; set; }
        public DownloadHandler downloadHandler { get; set; }
        public bool disposeUploadHandlerOnDispose { get; set; } = true;
        public bool disposeDownloadHandlerOnDispose { get; set; } = true;
        public bool useHttpContinue { get; set; } = true;
        public int redirectLimit { get; set; } = 32;

        public Result result { get; private set; } = Result.InProgress;
        public long responseCode { get; private set; }
        public string error { get; private set; }
        public bool isDone { get; private set; }
        public float downloadProgress => isDone ? 1f : 0f;
        public float uploadProgress => isDone ? 1f : 0f;
        public ulong downloadedBytes => (ulong)(downloadHandler?.data?.Length ?? 0);
        public ulong uploadedBytes => (ulong)(uploadHandler?.data?.Length ?? 0);
        [Obsolete("Use result instead.")] public bool isNetworkError => result == Result.ConnectionError;
        [Obsolete("Use result instead.")] public bool isHttpError => result == Result.ProtocolError;

        public UnityWebRequest() { }
        public UnityWebRequest(string url) { this.url = url; }
        public UnityWebRequest(Uri uri) { this.uri = uri; }
        public UnityWebRequest(string url, string method) { this.url = url; this.method = method; }
        public UnityWebRequest(string url, string method, DownloadHandler downloadHandler, UploadHandler uploadHandler)
        { this.url = url; this.method = method; this.downloadHandler = downloadHandler; this.uploadHandler = uploadHandler; }

        public static UnityWebRequest Get(string uri) => new(uri, kHttpVerbGET, new DownloadHandlerBuffer(), null);
        public static UnityWebRequest Delete(string uri) => new(uri, kHttpVerbDELETE);
        public static UnityWebRequest Head(string uri) => new(uri, kHttpVerbHEAD);
        public static UnityWebRequest Put(string uri, byte[] bodyData)
            => new(uri, kHttpVerbPUT, new DownloadHandlerBuffer(), new UploadHandlerRaw(bodyData));
        public static UnityWebRequest Put(string uri, string bodyData) => Put(uri, Encoding.UTF8.GetBytes(bodyData ?? string.Empty));
        public static UnityWebRequest Post(string uri, string postData, string contentType)
        {
            var r = new UnityWebRequest(uri, kHttpVerbPOST, new DownloadHandlerBuffer(),
                new UploadHandlerRaw(Encoding.UTF8.GetBytes(postData ?? string.Empty)) { contentType = contentType });
            return r;
        }
        public static string EscapeURL(string s) => Uri.EscapeDataString(s ?? string.Empty);
        public static string UnEscapeURL(string s) => Uri.UnescapeDataString(s ?? string.Empty);

        public void SetRequestHeader(string name, string value) => _headers[name] = value;
        public string GetRequestHeader(string name) => _headers.TryGetValue(name, out var v) ? v : null;
        public string GetResponseHeader(string name) => _responseHeaders != null && _responseHeaders.TryGetValue(name, out var v) ? v : null;
        public Dictionary<string, string> GetResponseHeaders() => _responseHeaders;

        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            if (_operation != null) throw new InvalidOperationException("UnityWebRequest has already been sent.");
            _operation = new UnityWebRequestAsyncOperation(this);
            _abort = timeout > 0 ? new CancellationTokenSource(TimeSpan.FromSeconds(timeout)) : new CancellationTokenSource();
            var context = SynchronizationContext.Current;
            _ = RunAsync(context, _abort.Token);
            return _operation;
        }

        public void Abort() => _abort?.Cancel();

        async Task RunAsync(SynchronizationContext context, CancellationToken ct)
        {
            Result outcome;
            long code = 0;
            string err = null;
            byte[] body = null;
            Dictionary<string, string> headers = null;
            try
            {
                using var msg = new HttpRequestMessage(new HttpMethod(method ?? kHttpVerbGET), url);
                if (uploadHandler?.data != null)
                {
                    msg.Content = new ByteArrayContent(uploadHandler.data);
                    if (!string.IsNullOrEmpty(uploadHandler.contentType))
                        msg.Content.Headers.TryAddWithoutValidation("Content-Type", uploadHandler.contentType);
                }
                foreach (var (k, v) in _headers)
                {
                    if (msg.Headers.TryAddWithoutValidation(k, v)) continue;
                    msg.Content ??= new ByteArrayContent(Array.Empty<byte>());
                    msg.Content.Headers.Remove(k);
                    msg.Content.Headers.TryAddWithoutValidation(k, v);
                }
                using var response = await s_client.SendAsync(msg, ct).ConfigureAwait(false);
                code = (long)response.StatusCode;
                headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in response.Headers) headers[h.Key] = string.Join(", ", h.Value);
                foreach (var h in response.Content.Headers) headers[h.Key] = string.Join(", ", h.Value);
                body = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) outcome = Result.Success;
                else { outcome = Result.ProtocolError; err = $"HTTP/1.1 {code} {response.ReasonPhrase}"; }
            }
            catch (OperationCanceledException)
            {
                outcome = Result.ConnectionError;
                err = timeout > 0 ? "Request timeout" : "Request aborted";
            }
            catch (Exception e)
            {
                outcome = Result.ConnectionError;
                err = e is HttpRequestException ? "Cannot connect to destination host" : e.Message;
            }

            void Finish(object _)
            {
                responseCode = code;
                _responseHeaders = headers;
                error = err;
                downloadHandler?.ReceiveData(body ?? Array.Empty<byte>());
                result = outcome;
                isDone = true;
                _operation.Finish();
            }

            if (context != null) context.Post(Finish, null);
            else Finish(null);
        }

        public void Dispose()
        {
            _abort?.Dispose();
            if (disposeUploadHandlerOnDispose) uploadHandler?.Dispose();
            if (disposeDownloadHandlerOnDispose) downloadHandler?.Dispose();
        }
    }

    /// <summary>The operation <see cref="UnityWebRequest.SendWebRequest"/> returns.</summary>
    public class UnityWebRequestAsyncOperation : AsyncOperation
    {
        public UnityWebRequest webRequest { get; }
        internal UnityWebRequestAsyncOperation(UnityWebRequest request) { webRequest = request; }
        internal void Finish() => Complete();
    }

    /// <summary>Thrown by the UniTask awaiter when a request finishes without success (original contract).</summary>
    public class UnityWebRequestException : Exception
    {
        public UnityWebRequest UnityWebRequest { get; }
        public UnityWebRequest.Result Result => UnityWebRequest.result;
        public string Error => UnityWebRequest.error;
        public long ResponseCode => UnityWebRequest.responseCode;
        public string Text => (UnityWebRequest.downloadHandler as DownloadHandlerBuffer)?.text;

        public UnityWebRequestException(UnityWebRequest request)
            : base($"{request.error}{Environment.NewLine}{(request.downloadHandler as DownloadHandlerBuffer)?.text}")
        { UnityWebRequest = request; }
    }

    public abstract class UploadHandler : IDisposable
    {
        public byte[] data { get; protected set; }
        public string contentType { get; set; }
        public float progress => 1f;
        public virtual void Dispose() { }
    }

    public sealed class UploadHandlerRaw : UploadHandler
    {
        public UploadHandlerRaw(byte[] data) { this.data = data; contentType = "application/octet-stream"; }
    }

    public abstract class DownloadHandler : IDisposable
    {
        public byte[] data { get; protected set; }
        public string text => data == null ? null : Encoding.UTF8.GetString(data);
        public bool isDone { get; private set; }
        public string error { get; protected set; }
        internal virtual void ReceiveData(byte[] bytes) { data = bytes; isDone = true; }
        public virtual void Dispose() { }
    }

    public sealed class DownloadHandlerBuffer : DownloadHandler
    {
        public static string GetContent(UnityWebRequest www) => www.downloadHandler?.text;
    }
}
