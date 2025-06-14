using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;


namespace UnityHelperSDK.HelperUtilities
{
    /// <summary>
    /// Provides a centralized HTTP client for Unity using UnityWebRequest.
    /// Supports JSON requests, retries, timeouts, custom headers, and more.
    /// </summary>

    /// <summary>
    /// A centralized, static HTTP client for Unity using UnityWebRequest.
    /// Supports JSON GET/POST/PUT/DELETE, retries, timeouts, custom headers,
    /// auth tokens, file download/upload, and request queuing with concurrency control.
    /// </summary>
    public static class NetworkRequestManager
    {
        // Default configuration
        private static int _maxRetries = 3;
        private static TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
        private static int _maxConcurrentRequests = 4;

        // Authentication token (e.g. JWT) to include in Authorization header
        public static string AuthToken { get; set; }

        // Global default headers
        private static readonly Dictionary<string, string> _defaultHeaders = new Dictionary<string, string>();

        // Semaphore to limit concurrent requests
        private static readonly SemaphoreSlim _throttle =
            new SemaphoreSlim(_maxConcurrentRequests, _maxConcurrentRequests);

        /// <summary>
        /// Add or update a default header included in every request.
        /// </summary>
        public static void SetDefaultHeader(string key, string value)
        {
            _defaultHeaders[key] = value;
        }

        /// <summary>
        /// Remove a default header.
        /// </summary>
        public static void RemoveDefaultHeader(string key)
        {
            _defaultHeaders.Remove(key);
        }

        /// <summary>
        /// Send a GET request and deserialize JSON response to T.
        /// </summary>
        public static async Task<T> GetJsonAsync<T>(
            string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            return await SendJsonRequestAsync<T>(UnityWebRequest.kHttpVerbGET, url, null, headers, cancellationToken);
        }

        /// <summary>
        /// Send a POST request with JSON payload and deserialize response to T.
        /// </summary>
        public static async Task<T> PostJsonAsync<T>(
            string url,
            object payload,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            string body = JsonUtility.ToJson(payload);
            return await SendJsonRequestAsync<T>(UnityWebRequest.kHttpVerbPOST, url, body, headers, cancellationToken);
        }

        /// <summary>
        /// Send a PUT request with JSON payload and deserialize response to T.
        /// </summary>
        public static async Task<T> PutJsonAsync<T>(
            string url,
            object payload,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            string body = JsonUtility.ToJson(payload);
            return await SendJsonRequestAsync<T>(UnityWebRequest.kHttpVerbPUT, url, body, headers, cancellationToken);
        }

        /// <summary>
        /// Send a DELETE request and deserialize JSON response to T.
        /// </summary>
        public static async Task<T> DeleteJsonAsync<T>(
            string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            return await SendJsonRequestAsync<T>(UnityWebRequest.kHttpVerbDELETE, url, null, headers, cancellationToken);
        }

        /// <summary>
        /// Core JSON request handler with retry, timeout, and error handling.
        /// </summary>
        private static async Task<T> SendJsonRequestAsync<T>(
            string method,
            string url,
            string jsonBody,
            Dictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            int attempt = 0;
            Exception lastError = null;

            while (attempt < _maxRetries)
            {
                attempt++;
                await _throttle.WaitAsync(cancellationToken);
                using (var req = new UnityWebRequest(url, method))
                {
                    try
                    {
                        // Body & headers
                        if (!string.IsNullOrEmpty(jsonBody))
                        {
                            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        }
                        req.downloadHandler = new DownloadHandlerBuffer();
                        req.SetRequestHeader("Content-Type", "application/json");
                        // Auth
                        if (!string.IsNullOrEmpty(AuthToken))
                            req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                        // Default headers
                        foreach (var kv in _defaultHeaders)
                            req.SetRequestHeader(kv.Key, kv.Value);
                        // Custom headers
                        if (headers != null)
                            foreach (var kv in headers)
                                req.SetRequestHeader(kv.Key, kv.Value);

                        // Send and await with timeout
                        var op = req.SendWebRequest();
                        using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            timeoutCts.CancelAfter(_defaultTimeout);
                            while (!op.isDone && !timeoutCts.IsCancellationRequested)
                                await Task.Yield();

                            if (timeoutCts.IsCancellationRequested)
                                throw new TimeoutException($"Request to {url} timed out after {_defaultTimeout.TotalSeconds}s");
                        }

                        // Error check
                        if (req.result != UnityWebRequest.Result.Success)
                            throw new Exception($"HTTP {req.responseCode}: {req.error}\n{req.downloadHandler.text}");

                        // Deserialize JSON
                        string text = req.downloadHandler.text;
                        return JsonUtility.FromJson<T>(text);
                    }
                    catch (Exception ex) when (attempt < _maxRetries)
                    {
                        lastError = ex;
                        await Task.Delay(ComputeBackoff(attempt), cancellationToken);
                    }
                    finally
                    {
                        _throttle.Release();
                    }
                }
            }

            throw new Exception($"Failed after {_maxRetries} attempts", lastError);
        }

        /// <summary>
        /// Compute exponential backoff delay.
        /// </summary>
        private static TimeSpan ComputeBackoff(int attempt)
        {
            // e.g., 500ms * 2^(attempt-1)
            return TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt - 1));
        }

        /// <summary>
        /// Download a file from URL and save to local path.
        /// </summary>
        public static async Task DownloadFileAsync(
            string url,
            string localPath,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            await _throttle.WaitAsync(cancellationToken);
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerFile(localPath);
                var op = req.SendWebRequest();
                while (!op.isDone && !cancellationToken.IsCancellationRequested)
                {
                    progress?.Report(op.progress);
                    await Task.Yield();
                }
                _throttle.Release();

                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Download error: {req.error}");
            }
        }

        /// <summary>
        /// Upload a file via multipart/form-data POST.
        /// </summary>
        public static async Task<string> UploadFileAsync(
            string url,
            byte[] fileData,
            string fileName,
            string formField = "file",
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            await _throttle.WaitAsync(cancellationToken);
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection(formField, fileData, fileName, "application/octet-stream")
            };
            using (var req = UnityWebRequest.Post(url, form))
            {
                // Apply headers
                if (!string.IsNullOrEmpty(AuthToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                if (headers != null)
                    foreach (var kv in headers)
                        req.SetRequestHeader(kv.Key, kv.Value);

                var op = req.SendWebRequest();
                while (!op.isDone && !cancellationToken.IsCancellationRequested)
                    await Task.Yield();
                _throttle.Release();

                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Upload error: {req.error}");
                return req.downloadHandler.text;
            }
        }

        /// <summary>
        /// Send a HEAD request to check if a resource exists or to get headers only.
        /// </summary>
        public static async Task<UnityWebRequest> HeadAsync(
            string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            await _throttle.WaitAsync(cancellationToken);
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbHEAD))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                if (!string.IsNullOrEmpty(AuthToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                foreach (var kv in _defaultHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);
                if (headers != null)
                    foreach (var kv in headers)
                        req.SetRequestHeader(kv.Key, kv.Value);
                var op = req.SendWebRequest();
                while (!op.isDone && !cancellationToken.IsCancellationRequested)
                    await Task.Yield();
                _throttle.Release();
                return req;
            }
        }

        /// <summary>
        /// Send a PATCH request with JSON payload and deserialize response to T.
        /// </summary>
        public static async Task<T> PatchJsonAsync<T>(
            string url,
            object payload,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            string body = JsonUtility.ToJson(payload);
            return await SendJsonRequestAsync<T>("PATCH", url, body, headers, cancellationToken);
        }

        /// <summary>
        /// Send a GET request and return the raw string response (no JSON deserialization).
        /// </summary>
        public static async Task<string> GetStringAsync(
            string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            await _throttle.WaitAsync(cancellationToken);
            using (var req = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(AuthToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                foreach (var kv in _defaultHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);
                if (headers != null)
                    foreach (var kv in headers)
                        req.SetRequestHeader(kv.Key, kv.Value);
                var op = req.SendWebRequest();
                while (!op.isDone && !cancellationToken.IsCancellationRequested)
                    await Task.Yield();
                _throttle.Release();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"HTTP {req.responseCode}: {req.error}");
                return req.downloadHandler.text;
            }
        }

        /// <summary>
        /// Send a GET request and return the raw byte[] response (for binary data).
        /// </summary>
        public static async Task<byte[]> GetBytesAsync(
            string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            await _throttle.WaitAsync(cancellationToken);
            using (var req = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(AuthToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                foreach (var kv in _defaultHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);
                if (headers != null)
                    foreach (var kv in headers)
                        req.SetRequestHeader(kv.Key, kv.Value);
                var op = req.SendWebRequest();
                while (!op.isDone && !cancellationToken.IsCancellationRequested)
                    await Task.Yield();
                _throttle.Release();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"HTTP {req.responseCode}: {req.error}");
                return req.downloadHandler.data;
            }
        }

        /// <summary>
        /// Ping a server URL to check if it is reachable (returns true if HTTP 200-399).
        /// </summary>
        public static async Task<bool> PingAsync(
            string url,
            int timeoutMs = 2000,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _throttle.WaitAsync(cancellationToken);
                using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbHEAD))
                {
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.timeout = timeoutMs / 1000;
                    var op = req.SendWebRequest();
                    while (!op.isDone && !cancellationToken.IsCancellationRequested)
                        await Task.Yield();
                    _throttle.Release();
                    return req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 400;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Set the default timeout for all requests.
        /// </summary>
        public static void SetDefaultTimeout(TimeSpan timeout)
        {
            _defaultTimeout = timeout;
        }

        /// <summary>
        /// Set the maximum number of retries for failed requests.
        /// </summary>
        public static void SetMaxRetries(int retries)
        {
            _maxRetries = retries;
        }

        /// <summary>
        /// Clear all default headers.
        /// </summary>
        public static void ClearDefaultHeaders()
        {
            _defaultHeaders.Clear();
        }

        /// <summary>
        /// Check if the device has internet connectivity (Unity's Application.internetReachability).
        /// </summary>
        public static bool IsInternetAvailable()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        /// <summary>
        /// Send a custom HTTP request with any verb, body, and headers. Returns raw string response.
        /// </summary>
        public static async Task<string> SendRequestAsync(
            string method,
            string url,
            string body = null,
            Dictionary<string, string> headers = null,
            string contentType = "application/json",
            CancellationToken cancellationToken = default)
        {
            await _throttle.WaitAsync(cancellationToken);
            using (var req = new UnityWebRequest(url, method))
            {
                if (!string.IsNullOrEmpty(body))
                {
                    byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", contentType);
                if (!string.IsNullOrEmpty(AuthToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                foreach (var kv in _defaultHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);
                if (headers != null)
                    foreach (var kv in headers)
                        req.SetRequestHeader(kv.Key, kv.Value);
                var op = req.SendWebRequest();
                while (!op.isDone && !cancellationToken.IsCancellationRequested)
                    await Task.Yield();
                _throttle.Release();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"HTTP {req.responseCode}: {req.error}");
                return req.downloadHandler.text;
            }
        }

        /// <summary>
        /// Try to send a GET request and deserialize JSON response to T. Returns (success, result).
        /// </summary>
        public static async Task<(bool Success, T Result)> TryGetJsonAsync<T>(
            string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await GetJsonAsync<T>(url, headers, cancellationToken);
                return (true, result);
            }
            catch
            {
                return (false, default);
            }
        }

        /// <summary>
        /// Download a Texture2D from a URL (useful for images/avatars).
        /// </summary>
        public static async Task<Texture2D> DownloadTextureAsync(
            string url,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            await _throttle.WaitAsync(cancellationToken);
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                if (!string.IsNullOrEmpty(AuthToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                foreach (var kv in _defaultHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);
                if (headers != null)
                    foreach (var kv in headers)
                        req.SetRequestHeader(kv.Key, kv.Value);
                var op = req.SendWebRequest();
                while (!op.isDone && !cancellationToken.IsCancellationRequested)
                    await Task.Yield();
                _throttle.Release();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Texture download error: {req.error}");
                return DownloadHandlerTexture.GetContent(req);
            }
        }

        /// <summary>
        /// Download an AudioClip from a URL (useful for audio streaming).
        /// </summary>
        public static async Task<AudioClip> DownloadAudioClipAsync(
            string url,
            AudioType audioType,
            Dictionary<string, string> headers = null,
            CancellationToken cancellationToken = default)
        {
            await _throttle.WaitAsync(cancellationToken);
            using (var req = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                if (!string.IsNullOrEmpty(AuthToken))
                    req.SetRequestHeader("Authorization", $"Bearer {AuthToken}");
                foreach (var kv in _defaultHeaders)
                    req.SetRequestHeader(kv.Key, kv.Value);
                if (headers != null)
                    foreach (var kv in headers)
                        req.SetRequestHeader(kv.Key, kv.Value);
                var op = req.SendWebRequest();
                while (!op.isDone && !cancellationToken.IsCancellationRequested)
                    await Task.Yield();
                _throttle.Release();
                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"Audio download error: {req.error}");
                return DownloadHandlerAudioClip.GetContent(req);
            }
        }
    }
}