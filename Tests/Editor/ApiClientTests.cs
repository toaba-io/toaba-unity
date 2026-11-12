using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Toaba.Http;
using UnityEngine;

namespace Toaba.Tests
{
    public class ApiClientTests
    {
        TestServer server;
        List<TimeSpan> waits;
        ApiClient api;

        class Session
        {
            public string PlayerId;
            public string CreateTime;
        }

        [SetUp]
        public void SetUp()
        {
            server = new TestServer();
            waits = new List<TimeSpan>();
            api = new ApiClient("pk_test", server.Url, (wait, _) =>
            {
                waits.Add(wait);
                var done = new AwaitableCompletionSource();
                done.SetResult();
                return done.Awaitable;
            });
        }

        [TearDown]
        public void TearDown() => server.Dispose();

        [Test]
        public async Task A_call_sends_the_key_token_and_body_and_returns_the_data()
        {
            server.Answer(201, "{\"data\":{\"player_id\":\"pl_1\",\"create_time\":\"2026-11-12T08:30:49.123Z\"}}");

            var session = await api.SendAsync<Session>(
                "POST", "/v1/auth/sessions", new { Anonymous = true, DisplayName = "Momo" }, "at_1");

            Assert.That(session.PlayerId, Is.EqualTo("pl_1"));
            Assert.That(session.CreateTime, Is.EqualTo("2026-11-12T08:30:49.123Z"));
            var request = server.Requests[0];
            Assert.That(request.Method, Is.EqualTo("POST"));
            Assert.That(request.Path, Is.EqualTo("/v1/auth/sessions"));
            Assert.That(request.Headers["apikey"], Is.EqualTo("pk_test"));
            Assert.That(request.Headers["Authorization"], Is.EqualTo("Bearer at_1"));
            Assert.That(request.Headers["Content-Type"], Is.EqualTo("application/json"));
            Assert.That(request.Body, Is.EqualTo("{\"anonymous\":true,\"display_name\":\"Momo\"}"));
        }

        [Test]
        public async Task An_error_response_becomes_a_ToabaException()
        {
            server.Answer(
                422,
                "{\"error\":\"validation_failed\",\"message\":\"The request has invalid fields\",\"errors\":{\"email\":[\"is invalid\"]}}",
                ("x-request-id", "req_1"));

            var error = await Throws(() => api.SendAsync("POST", "/x", new { }));

            Assert.That(error.Code, Is.EqualTo(ErrorCodes.ValidationFailed));
            Assert.That(error.Status, Is.EqualTo(422));
            Assert.That(error.Message, Is.EqualTo("The request has invalid fields"));
            Assert.That(error.FieldErrors["email"], Is.EqualTo(new[] { "is invalid" }));
            Assert.That(error.RequestId, Is.EqualTo("req_1"));
        }

        [Test]
        public async Task A_response_that_isnt_toabas_is_unexpected()
        {
            server.Answer(502, "<html>Bad Gateway</html>");

            var error = await Throws(() => api.SendAsync("GET", "/x"));

            Assert.That(error.Code, Is.EqualTo(ErrorCodes.UnexpectedResponse));
            Assert.That(error.Status, Is.EqualTo(502));
        }

        [Test]
        public async Task No_response_is_a_network_error()
        {
            var url = server.Url;
            server.Dispose();

            var error = await Throws(() => new ApiClient("pk_test", url).SendAsync("GET", "/x"));

            Assert.That(error.Code, Is.EqualTo(ErrorCodes.NetworkError));
            Assert.That(error.Status, Is.EqualTo(0));
        }

        [Test]
        public async Task A_short_rate_limit_is_waited_out()
        {
            server.Answer(429, "{\"error\":\"rate_limited\"}", ("Retry-After", "2"));
            server.Answer(200, "{\"data\":{\"player_id\":\"pl_1\"}}");

            var session = await api.SendAsync<Session>("POST", "/x", new { });

            Assert.That(session.PlayerId, Is.EqualTo("pl_1"));
            Assert.That(waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
        }

        [Test]
        public async Task A_long_rate_limit_fails_at_once()
        {
            server.Answer(429, "{\"error\":\"email_cooldown\"}", ("Retry-After", "60"));

            var error = await Throws(() => api.SendAsync("POST", "/x", new { }));

            Assert.That(error.Code, Is.EqualTo("email_cooldown"));
            Assert.That(error.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(waits, Is.Empty);
        }

        static async Task<ToabaException> Throws<T>(Func<Awaitable<T>> call)
        {
            try
            {
                await call();
            }
            catch (ToabaException error)
            {
                return error;
            }
            Assert.Fail("Expected a ToabaException");
            return null;
        }
    }
}
