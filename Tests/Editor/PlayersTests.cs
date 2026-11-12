using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Toaba.Http;

namespace Toaba.Tests
{
    public class PlayersTests
    {
        TestServer server;
        MemoryStore store;

        sealed class MemoryStore : ISecureStore
        {
            readonly Dictionary<string, string> values = new Dictionary<string, string>();
            public string Read(string key) => values.GetValueOrDefault(key);
            public void Write(string key, string value) => values[key] = value;
            public void Delete(string key) => values.Remove(key);
        }

        [SetUp]
        public void SetUp()
        {
            server = new TestServer();
            store = new MemoryStore();
        }

        [TearDown]
        public void TearDown() => server.Dispose();

        PlayersService Launch() => new ToabaClient("app_1", new ApiClient("pk_test", server.Url), store).Players;

        static string SessionJson(
            string playerId = "pl_1", string accessToken = "at_1", string refreshToken = "rt_1", int ttlSeconds = 3600) =>
            $"{{\"data\":{{\"player_id\":\"{playerId}\",\"access_token\":\"{accessToken}\"," +
            $"\"refresh_token\":\"{refreshToken}\",\"access_token_ttl_seconds\":{ttlSeconds}}}}}";

        [Test]
        public async Task A_guest_stays_signed_in_across_launches()
        {
            server.Answer(201, SessionJson());
            var players = Launch();
            var signedIn = 0;
            players.SignedIn += () => signedIn++;

            await players.SignInAnonymouslyAsync();

            Assert.That(server.Requests[0].Body, Is.EqualTo("{\"anonymous\":true}"));
            Assert.That(signedIn, Is.EqualTo(1));
            var relaunched = Launch();
            Assert.That(relaunched.IsSignedIn, Is.True);
            Assert.That(relaunched.PlayerId, Is.EqualTo("pl_1"));
        }

        [Test]
        public async Task Signing_in_again_while_signed_in_is_refused()
        {
            server.Answer(201, SessionJson());
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            await Expect.Throws<InvalidOperationException>(async () => await players.SignInAnonymouslyAsync());
            Assert.That(server.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Each_profile_keeps_its_own_player()
        {
            server.Answer(201, SessionJson());
            server.Answer(201, SessionJson(playerId: "pl_2"));
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            players.SwitchProfile("p2");
            Assert.That(players.IsSignedIn, Is.False);
            await players.SignInAnonymouslyAsync();
            players.SwitchProfile("default");

            Assert.That(players.PlayerId, Is.EqualTo("pl_1"));
            players.SwitchProfile("p2");
            Assert.That(players.PlayerId, Is.EqualTo("pl_2"));
        }

        [Test]
        public async Task An_expiring_token_is_refreshed_once_for_everyone_and_kept()
        {
            server.Answer(201, SessionJson(ttlSeconds: 60));
            server.Answer(200, SessionJson(accessToken: "at_2", refreshToken: "rt_2"));
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            var first = players.GetAccessTokenAsync();
            var second = players.GetAccessTokenAsync();

            Assert.That(await first, Is.EqualTo("at_2"));
            Assert.That(await second, Is.EqualTo("at_2"));
            Assert.That(server.Requests, Has.Count.EqualTo(2));
            Assert.That(server.Requests[1].Body, Is.EqualTo("{\"refresh_token\":\"rt_1\"}"));
            Assert.That(await Launch().GetAccessTokenAsync(), Is.EqualTo("at_2"));
        }

        [Test]
        public async Task A_refused_refresh_ends_the_session()
        {
            server.Answer(201, SessionJson(ttlSeconds: 60));
            server.Answer(401, "{\"error\":\"refresh_token_reused\"}");
            var players = Launch();
            var reasons = new List<SignOutReason>();
            players.SignedOut += reasons.Add;
            await players.SignInAnonymouslyAsync();

            var error = await Expect.Throws<ToabaException>(async () => await players.GetAccessTokenAsync());

            Assert.That(error.Code, Is.EqualTo("refresh_token_reused"));
            Assert.That(players.IsSignedIn, Is.False);
            Assert.That(reasons, Is.EqualTo(new[] { SignOutReason.SessionEnded }));
            Assert.That(Launch().IsSignedIn, Is.False);
        }

        [Test]
        public async Task Signing_out_forgets_the_session_even_when_Toaba_refuses()
        {
            server.Answer(201, SessionJson());
            server.Answer(401, "{\"error\":\"invalid_access_token\"}");
            var players = Launch();
            var reasons = new List<SignOutReason>();
            players.SignedOut += reasons.Add;
            await players.SignInAnonymouslyAsync();

            await players.SignOutAsync();

            var request = server.Requests[1];
            Assert.That(request.Method, Is.EqualTo("DELETE"));
            Assert.That(request.Headers["Authorization"], Is.EqualTo("Bearer at_1"));
            Assert.That(players.IsSignedIn, Is.False);
            Assert.That(reasons, Is.EqualTo(new[] { SignOutReason.Requested }));
            Assert.That(Launch().IsSignedIn, Is.False);
        }
    }
}
