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

        const string FlowJson = "{\"data\":{\"id\":\"fl_1\",\"expire_time\":\"2099-01-01T00:00:00Z\"}}";

        [Test]
        public async Task An_emailed_code_can_be_entered_after_a_restart_and_retried()
        {
            server.Answer(202, FlowJson);
            server.Answer(401, "{\"error\":\"invalid_code\",\"message\":\"The verification code is invalid\"}");
            server.Answer(201, SessionJson());
            await Launch().SignInWithEmailAsync("momo@example.com");

            var relaunched = Launch();
            var signedIn = 0;
            relaunched.SignedIn += () => signedIn++;
            var pending = relaunched.PendingEmail;
            Assert.That(pending.Email, Is.EqualTo("momo@example.com"));
            var error = await Expect.Throws<ToabaException>(async () => await pending.VerifyAsync("000000"));
            Assert.That(error.Code, Is.EqualTo(ErrorCodes.InvalidCode));
            await pending.VerifyAsync("123456");

            Assert.That(server.Requests[0].Body, Is.EqualTo("{\"email\":\"momo@example.com\"}"));
            Assert.That(server.Requests[2].Path, Is.EqualTo("/v1/auth/flows/fl_1/verify"));
            Assert.That(server.Requests[2].Body, Is.EqualTo("{\"code\":\"123456\"}"));
            Assert.That(relaunched.PlayerId, Is.EqualTo("pl_1"));
            Assert.That(signedIn, Is.EqualTo(1));
            Assert.That(Launch().PendingEmail, Is.Null);
        }

        [Test]
        public async Task Without_email_confirmation_no_code_is_needed()
        {
            server.Answer(201, SessionJson());
            var players = Launch();

            var email = await players.SignInWithEmailAsync("momo@example.com");

            Assert.That(email.NeedsCode, Is.False);
            Assert.That(players.IsSignedIn, Is.True);
        }

        [Test]
        public async Task A_guest_who_adds_an_email_keeps_their_player()
        {
            server.Answer(201, SessionJson());
            server.Answer(202, FlowJson);
            server.Answer(201, SessionJson(accessToken: "at_2", refreshToken: "rt_2"));
            var players = Launch();
            var signedIn = 0;
            players.SignedIn += () => signedIn++;
            await players.SignInAnonymouslyAsync();

            var added = await players.AddEmailAsync("momo@example.com");
            await added.VerifyAsync("123456");

            var request = server.Requests[1];
            Assert.That(request.Path, Is.EqualTo("/v1/auth/players/me/identities"));
            Assert.That(request.Headers["Authorization"], Is.EqualTo("Bearer at_1"));
            Assert.That(request.Body, Is.EqualTo("{\"provider\":\"email\",\"email\":\"momo@example.com\"}"));
            Assert.That(players.PlayerId, Is.EqualTo("pl_1"));
            Assert.That(signedIn, Is.EqualTo(1));
            Assert.That(await players.GetAccessTokenAsync(), Is.EqualTo("at_2"));
        }

        [Test]
        public async Task A_rejected_token_is_refreshed_and_the_call_retried()
        {
            server.Answer(201, SessionJson());
            server.Answer(401, "{\"error\":\"invalid_access_token\"}");
            server.Answer(200, SessionJson(accessToken: "at_2", refreshToken: "rt_2"));
            server.Answer(202, FlowJson);
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            await players.AddEmailAsync("momo@example.com");

            Assert.That(server.Requests[2].Path, Is.EqualTo("/v1/auth/sessions/refresh"));
            Assert.That(server.Requests[3].Headers["Authorization"], Is.EqualTo("Bearer at_2"));
        }

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
