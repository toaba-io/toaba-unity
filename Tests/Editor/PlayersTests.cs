using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Toaba.Http;

namespace Toaba.Tests
{
    public class PlayersTests
    {
        const string SessionJson =
            "{\"data\":{\"id\":\"se_1\",\"player_id\":\"pl_1\",\"access_token\":\"at_1\",\"refresh_token\":\"rt_1\",\"access_token_ttl_seconds\":3600}}";

        TestServer server;
        MemoryStore store;

        sealed class MemoryStore : ISessionStore
        {
            public string Saved;
            public string Load() => Saved;
            public void Save(string session) => Saved = session;
            public void Clear() => Saved = null;
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

        [Test]
        public async Task A_guest_stays_signed_in_across_launches()
        {
            server.Answer(201, SessionJson);
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
            server.Answer(201, SessionJson);
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            await Expect.Throws<InvalidOperationException>(async () => await players.SignInAnonymouslyAsync());
            Assert.That(server.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Signing_out_forgets_the_session_even_when_Toaba_refuses()
        {
            server.Answer(201, SessionJson);
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
