using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
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

        public ApiClient(string apiKey, string baseUrl = null, Func<TimeSpan, CancellationToken, Awaitable> wait = null)
        {
            this.apiKey = apiKey;
            this.baseUrl = (baseUrl ?? DefaultUrl()).TrimEnd('/');
            this.wait = wait ?? WaitRealtimeAsync;
        }

        static string DefaultUrl()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var url = Environment.GetEnvironmentVariable("TOABA_API_URL");
            if (!string.IsNullOrWhiteSpace(url))
                return url;
#endif
            return ProductionUrl;
        }

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
                using (cancellation.Register(web.Abort))
                    await web.SendWebRequest();
                cancellation.ThrowIfCancellationRequested();

                if (web.result == UnityWebRequest.Result.ConnectionError
                    || web.result == UnityWebRequest.Result.DataProcessingError)
                    throw new ToabaException(ErrorCodes.NetworkError, $"Couldn't reach Toaba: {web.error}");
                var status = (int)web.responseCode;
                if (status >= 200 && status < 300)
                    return new ApiResponse(status, web.downloadHandler.text);

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

        // UnityWebRequest because HttpClient doesn't work on WebGL.
        UnityWebRequest Request(string method, string path, byte[] json, string accessToken)
        {
            var upload = json == null ? null : new UploadHandlerRaw(json);
            var web = new UnityWebRequest(baseUrl + path, method, new DownloadHandlerBuffer(), upload);
            web.timeout = TimeoutSeconds;
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

        public ApiResponse(int status, string body)
        {
            Status = status;
            this.body = body;
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
