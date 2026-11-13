using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace Toaba.Http
{
    internal sealed class ApiClient
    {
        const string ProductionUrl = "https://api.toaba.io";
        const int TimeoutSeconds = 30;
        const int RateLimitWaits = 2;
        static readonly TimeSpan RateLimitWaitMax = TimeSpan.FromSeconds(10);

        readonly string baseUrl;
        readonly string apiKey;
        readonly Func<TimeSpan, CancellationToken, Awaitable> wait;
        readonly Func<double> realtime;
        readonly IReadOnlyDictionary<string, string> headers;

        /// <param name="realtime">Seconds on a clock that only moves forward.</param>
        public ApiClient(
            string apiKey,
            string baseUrl = null,
            Func<TimeSpan, CancellationToken, Awaitable> wait = null,
            Func<double> realtime = null)
            : this(apiKey, (baseUrl ?? DefaultUrl()).TrimEnd('/'), wait ?? WaitRealtimeAsync,
                realtime ?? (() => Time.realtimeSinceStartupAsDouble), new Dictionary<string, string>())
        {
        }

        ApiClient(
            string apiKey,
            string baseUrl,
            Func<TimeSpan, CancellationToken, Awaitable> wait,
            Func<double> realtime,
            IReadOnlyDictionary<string, string> headers)
        {
            this.apiKey = apiKey;
            this.baseUrl = baseUrl;
            this.wait = wait;
            this.realtime = realtime;
            this.headers = headers;
        }

        public double Realtime => realtime();

        static string DefaultUrl()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var url = Environment.GetEnvironmentVariable("TOABA_API_URL");
            if (!string.IsNullOrWhiteSpace(url))
                return url;
#endif
            return ProductionUrl;
        }

        /// <summary>A copy that sends <paramref name="headers"/> with every request.</summary>
        public ApiClient WithHeaders(IReadOnlyDictionary<string, string> headers) =>
            new ApiClient(apiKey, baseUrl, wait, realtime, headers);

        public async Awaitable<ApiResponse> SendAsync(
            string method,
            string path,
            object body = null,
            string accessToken = null,
            CancellationToken cancellation = default)
        {
            var json = body == null ? null : Encoding.UTF8.GetBytes(Json.Serialize(body));
            for (var waits = 0; ; waits++)
            {
                cancellation.ThrowIfCancellationRequested();
                using var web = Request(method, path, json, accessToken);
                var sent = realtime();
                using (cancellation.Register(web.Abort))
                    await web.SendWebRequest();
                cancellation.ThrowIfCancellationRequested();

                if (web.result == UnityWebRequest.Result.ConnectionError
                    || web.result == UnityWebRequest.Result.DataProcessingError)
                    throw new ToabaException(ErrorCodes.NetworkError, $"Couldn't reach Toaba: {web.error}");
                var status = (int)web.responseCode;
                if (status >= 200 && status < 300)
                    return new ApiResponse(status, web.downloadHandler.text, sent, realtime());

                var error = ReadError(web);
                // A rate-limited request wasn't processed, so it's safe to send again.
                if (status == 429 && error.RetryAfter <= RateLimitWaitMax && waits < RateLimitWaits)
                    await wait(error.RetryAfter.Value, cancellation);
                else
                    throw error;
            }
        }

        public async Awaitable<T> SendAsync<T>(
            string method,
            string path,
            object body = null,
            string accessToken = null,
            CancellationToken cancellation = default) =>
            (await SendAsync(method, path, body, accessToken, cancellation)).Data<T>();

        public Awaitable WaitAsync(TimeSpan duration, CancellationToken cancellation) => wait(duration, cancellation);

        // A page the browser opens can't send headers, so the key goes in the query.
        public string BrowserUrl(string path, IEnumerable<(string Name, string Value)> query) =>
            baseUrl + path + "?" + QueryString(new[] { ("apikey", apiKey) }.Concat(query));

        public static string QueryString(IEnumerable<(string Name, string Value)> query) =>
            string.Join("&", query.Select(pair => Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(pair.Value)));

        /// <summary>The query parameters of a URL or request target; a missing one reads as null.</summary>
        public static NameValueCollection ParseQuery(string url)
        {
            var start = url.IndexOf('?');
            return HttpUtility.ParseQueryString(start < 0 ? "" : url.Substring(start + 1));
        }

        // UnityWebRequest because HttpClient doesn't work on WebGL.
        UnityWebRequest Request(string method, string path, byte[] json, string accessToken)
        {
            var upload = json == null ? null : new UploadHandlerRaw(json);
            var web = new UnityWebRequest(baseUrl + path, method, new DownloadHandlerBuffer(), upload);
            web.timeout = TimeoutSeconds;
            foreach (var header in headers)
                web.SetRequestHeader(header.Key, header.Value);
            web.SetRequestHeader("apikey", apiKey);
            web.SetRequestHeader("Accept", "application/json");
            if (json != null)
                web.SetRequestHeader("Content-Type", "application/json");
            if (accessToken != null)
                web.SetRequestHeader("Authorization", "Bearer " + accessToken);
            return web;
        }

        // {"error": code, "message": text, "errors": {field: [text]}}; content errors have only the code,
        // and framework errors are {"errors": {"detail": text}}.
        static ToabaException ReadError(UnityWebRequest web)
        {
            var status = (int)web.responseCode;
            TimeSpan? retryAfter = null;
            if (int.TryParse(web.GetResponseHeader("retry-after"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
                retryAfter = TimeSpan.FromSeconds(seconds);

            var body = Json.TryParseObject(web.downloadHandler.text);
            var errors = body?["errors"] as JObject;
            var code = Text(body?["error"]) ?? (status == 429 ? ErrorCodes.RateLimited : ErrorCodes.UnexpectedResponse);
            var message = Text(body?["message"]) ?? Text(errors?["detail"]) ?? $"Toaba answered {status}: {code}";
            return new ToabaException(
                code, message, status, FieldErrors(errors), retryAfter, web.GetResponseHeader("x-request-id"));
        }

        static Dictionary<string, IReadOnlyList<string>> FieldErrors(JObject errors)
        {
            var fields = new Dictionary<string, IReadOnlyList<string>>();
            if (errors == null)
                return fields;
            foreach (var field in errors.Properties())
                if (field.Value is JArray messages)
                    fields[field.Name] = messages.Select(Text).Where(message => message != null).ToArray();
            return fields;
        }

        static string Text(JToken token) => token?.Type == JTokenType.String ? (string)token : null;

        // Real time, so Time.timeScale = 0 doesn't stall it.
        static async Awaitable WaitRealtimeAsync(TimeSpan duration, CancellationToken cancellation)
        {
            var end = Time.realtimeSinceStartupAsDouble + duration.TotalSeconds;
            while (Time.realtimeSinceStartupAsDouble < end)
                await Awaitable.NextFrameAsync(cancellation);
        }
    }

    internal sealed class ApiResponse
    {
        readonly string body;

        public int Status { get; }

        /// <summary>When the answered attempt was sent and its answer arrived, in <see cref="ApiClient.Realtime"/> seconds.</summary>
        public double Sent { get; }

        public double Received { get; }

        public ApiResponse(int status, string body, double sent, double received)
        {
            Status = status;
            this.body = body;
            Sent = sent;
            Received = received;
        }

        public T Data<T>()
        {
            try
            {
                var envelope = Json.Deserialize<Envelope<T>>(body);
                if (envelope != null)
                    return envelope.Data;
            }
            catch (JsonException)
            {
            }
            throw new ToabaException(ErrorCodes.UnexpectedResponse, $"Couldn't read Toaba's answer ({Status})", Status);
        }

        [Preserve]
        sealed class Envelope<T>
        {
            [JsonProperty(Required = Required.AllowNull)]
            public T Data;
        }
    }
}
