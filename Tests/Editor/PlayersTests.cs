using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using Toaba.Http;

namespace Toaba.Tests
{
    // Also plays the browser: see Open.
    public class PlayersTests : IBrowser
    {
        TestServer server;
        MemoryStore store;
        string page;
        string opened;
        string replaced;
        string browserReturn;

        [SetUp]
        public void SetUp()
        {
            server = new TestServer();
            store = new MemoryStore();
            page = opened = replaced = null;
            browserReturn = "code=ac_1";
        }

        [TearDown]
        public void TearDown() => server.Dispose();

        // page: the address of the WebGL page the game runs in.
        PlayersService Launch(string page = null)
        {
            this.page = page;
            return new ToabaClient("app_1", new ApiClient("pk_test", server.Url), store, this).Players;
        }

        string IBrowser.PageUrl => page;

        void IBrowser.ReplacePageUrl(string url) => replaced = url;

        // On desktop, returns to the game's loopback address, named in the opened URL or, when
        // linking, in the authorize request Toaba received.
        void IBrowser.Open(string url)
        {
            opened = url;
            if (page != null)
                return;
            var redirect = ApiClient.ParseQuery(url)["redirect_to"]
                           ?? ApiClient.ParseQuery(server.Requests.Last().Query)["redirect_to"];
            var back = $"{redirect}?{browserReturn}";
            Task.Run(() => new HttpClient().GetAsync(back));
        }

        static string SessionJson(
            string playerId = "pl_1", string accessToken = "at_1", string refreshToken = "rt_1", int ttlSeconds = 3600) =>
            $"{{\"data\":{{\"player_id\":\"{playerId}\",\"access_token\":\"{accessToken}\"," +
            $"\"refresh_token\":\"{refreshToken}\",\"access_token_ttl_seconds\":{ttlSeconds}}}}}";

        const string FlowJson = "{\"data\":{\"id\":\"fl_1\",\"expire_time\":\"2099-01-01T00:00:00Z\"}}";

        const string PlayerJson =
            "{\"data\":{\"id\":\"pl_1\",\"username\":null,\"email\":\"momo@example.com\",\"email_verified\":true," +
            "\"anonymous\":false,\"create_time\":\"2026-11-12T08:30:49Z\"}}";

        [Test]
        public async Task An_ID_token_signs_in_with_the_raw_nonce()
        {
            server.Answer(201, SessionJson());
            var players = Launch();
            var nonce = Nonce.Create();

            var result = await players.SignInWithIdTokenAsync(Provider.Apple, "id_apple", nonce.Raw);

            Assert.That(result.NeedsCode, Is.False);
            Assert.That(players.IsSignedIn, Is.True);
            Assert.That(server.Requests[0].Body, Is.EqualTo(
                $"{{\"provider\":\"apple\",\"id_token\":\"id_apple\",\"nonce\":\"{nonce.Raw}\"}}"));
        }

        [Test]
        public void A_nonce_is_hashed_as_lowercase_SHA256_hex()
        {
            Assert.That(new Nonce("abc").Hashed,
                Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        }

        [Test]
        public async Task Linking_an_ID_token_keeps_the_player()
        {
            server.Answer(201, SessionJson());
            server.Answer(201, SessionJson(accessToken: "at_2", refreshToken: "rt_2"));
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            await players.LinkIdTokenAsync(Provider.Google, "id_google");

            Assert.That(server.Requests[1].Path, Is.EqualTo("/v1/auth/players/me/identities"));
            Assert.That(server.Requests[1].Body, Is.EqualTo("{\"provider\":\"google\",\"id_token\":\"id_google\"}"));
            Assert.That(players.PlayerId, Is.EqualTo("pl_1"));
        }

        [Test]
        public async Task Identities_can_be_listed_and_removed()
        {
            server.Answer(201, SessionJson());
            server.Answer(200,
                "{\"data\":[{\"id\":\"id_1\",\"provider\":\"email\",\"create_time\":\"2026-11-12T08:00:00Z\"}," +
                "{\"id\":\"id_2\",\"provider\":\"google\",\"create_time\":\"2026-11-12T09:00:00Z\"}]}");
            server.Answer(204, "");
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            var identities = await players.GetIdentitiesAsync();
            await players.UnlinkAsync(identities[1]);

            Assert.That(identities[0].Provider, Is.EqualTo("email"));
            Assert.That(identities[1].Provider, Is.EqualTo(Provider.Google));
            Assert.That(server.Requests[2].Method, Is.EqualTo("DELETE"));
            Assert.That(server.Requests[2].Path, Is.EqualTo("/v1/auth/players/me/identities/id_2"));
        }

        [Test]
        public async Task The_browser_signs_in_with_a_PKCE_code()
        {
            server.Answer(201, SessionJson());
            var players = Launch();

            var result = await players.SignInWithBrowserAsync(Provider.Google);

            Assert.That(opened, Does.StartWith(server.Url + "/v1/auth/authorize?"));
            var authorize = ApiClient.ParseQuery(opened);
            Assert.That(authorize["provider"], Is.EqualTo("google"));
            Assert.That(authorize["apikey"], Is.EqualTo("pk_test"));
            Assert.That(authorize["redirect_to"], Does.Match(@"^http://127\.0\.0\.1:\d+/callback$"));
            Assert.That(authorize["code_challenge_method"], Is.EqualTo("S256"));
            var exchange = server.Requests[0];
            Assert.That(exchange.Path, Is.EqualTo("/v1/auth/sessions"));
            Assert.That(exchange.Body, Does.StartWith("{\"auth_code\":\"ac_1\",\"code_verifier\":\""));
            var verifier = Regex.Match(exchange.Body, "\"code_verifier\":\"([^\"]+)\"").Groups[1].Value;
            using var sha256 = SHA256.Create();
            var challenge = Convert.ToBase64String(sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Assert.That(authorize["code_challenge"], Is.EqualTo(challenge));
            Assert.That(result.NeedsCode, Is.False);
            Assert.That(players.IsSignedIn, Is.True);
        }

        [Test]
        public async Task An_unverified_provider_email_from_the_browser_waits_for_a_code()
        {
            browserReturn = "error=access_denied&error_code=email_confirmation_required&flow_id=fl_9";
            var players = Launch();

            var result = await players.SignInWithBrowserAsync(Provider.Apple);

            Assert.That(result.NeedsCode, Is.True);
            Assert.That(Launch().PendingEmail, Is.Not.Null);
        }

        [Test]
        public async Task A_browser_error_becomes_a_ToabaException()
        {
            browserReturn = "error=access_denied&error_code=bad_oauth_state&error_description=OAuth+state+is+invalid";
            var players = Launch();

            var error = await Expect.Throws<ToabaException>(async () => await players.SignInWithBrowserAsync(Provider.Google));

            Assert.That(error.Code, Is.EqualTo("bad_oauth_state"));
            Assert.That(error.Message, Is.EqualTo("OAuth state is invalid"));
        }

        [Test]
        public async Task Linking_in_the_browser_keeps_the_player()
        {
            server.Answer(201, SessionJson());
            server.Answer(200, "{\"data\":{\"url\":\"https://accounts.example/o\"}}");
            server.Answer(201, SessionJson(accessToken: "at_2", refreshToken: "rt_2"));
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            await players.LinkWithBrowserAsync(Provider.Apple);

            Assert.That(opened, Is.EqualTo("https://accounts.example/o"));
            var authorize = server.Requests[1];
            Assert.That(authorize.Path, Is.EqualTo("/v1/auth/players/me/identities/authorize"));
            Assert.That(authorize.Headers["Authorization"], Is.EqualTo("Bearer at_1"));
            Assert.That(ApiClient.ParseQuery(authorize.Query)["skip_http_redirect"], Is.EqualTo("true"));
            Assert.That(server.Requests[2].Body, Does.StartWith("{\"auth_code\":\"ac_1\""));
            Assert.That(players.PlayerId, Is.EqualTo("pl_1"));
            Assert.That(await players.GetAccessTokenAsync(), Is.EqualTo("at_2"));
        }

        const string Page = "https://game.test/play?level=2";

        [Test]
        public async Task On_WebGL_the_page_finishes_the_sign_in_when_it_loads_again()
        {
            _ = Launch(Page).SignInWithBrowserAsync(Provider.Google);
            var challenge = ApiClient.ParseQuery(opened)["code_challenge"];
            Assert.That(ApiClient.ParseQuery(opened)["redirect_to"], Is.EqualTo(Page));

            server.Answer(201, SessionJson());
            var players = Launch(Page + "&code=ac_1");
            await players.ReadyAsync();

            Assert.That(players.IsSignedIn, Is.True);
            Assert.That(replaced, Is.EqualTo(Page));
            var verifier = Regex.Match(server.Requests[0].Body, "\"code_verifier\":\"([^\"]+)\"").Groups[1].Value;
            Assert.That(Crypto.Base64Url(Crypto.Sha256(verifier)), Is.EqualTo(challenge));
            Assert.That(store.Values.ContainsKey("browser.pkce"), Is.False);
        }

        [Test]
        public async Task A_guest_sign_in_waits_for_the_page_to_finish_its_browser_sign_in()
        {
            _ = Launch(Page).SignInWithBrowserAsync(Provider.Google);
            server.Answer(201, SessionJson());
            var players = Launch(Page + "&code=ac_1");

            await Expect.Throws<InvalidOperationException>(async () => await players.SignInAnonymouslyAsync());

            Assert.That(players.IsSignedIn, Is.True);
            Assert.That(server.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task A_failed_browser_sign_in_is_thrown_by_ReadyAsync()
        {
            _ = Launch(Page).SignInWithBrowserAsync(Provider.Google);
            var players = Launch(Page + "&error=access_denied&error_code=bad_oauth_state&error_description=Expired");

            var error = await Expect.Throws<ToabaException>(async () => await players.ReadyAsync());

            Assert.That(error.Code, Is.EqualTo("bad_oauth_state"));
        }

        [Test]
        public async Task A_username_can_be_set_and_cleared()
        {
            server.Answer(201, SessionJson());
            server.Answer(200, PlayerJson.Replace("\"username\":null", "\"username\":\"moss\""));
            server.Answer(200, PlayerJson);
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            var named = await players.SetUsernameAsync("moss");
            var cleared = await players.SetUsernameAsync(null);

            Assert.That(server.Requests[1].Body, Is.EqualTo("{\"username\":\"moss\"}"));
            Assert.That(server.Requests[2].Body, Is.EqualTo("{\"username\":null}"));
            Assert.That(named.Username, Is.EqualTo("moss"));
            Assert.That(cleared.Username, Is.Null);
            Assert.That(cleared.Email, Is.EqualTo("momo@example.com"));
            Assert.That(cleared.EmailVerified, Is.True);
            Assert.That(cleared.IsAnonymous, Is.False);
            Assert.That(cleared.CreateTime, Is.EqualTo(new DateTimeOffset(2026, 11, 12, 8, 30, 49, TimeSpan.Zero)));
        }

        [Test]
        public async Task Deleting_the_account_signs_the_player_out()
        {
            server.Answer(201, SessionJson());
            server.Answer(204, "");
            var players = Launch();
            var reasons = new List<SignOutReason>();
            players.SignedOut += reasons.Add;
            await players.SignInAnonymouslyAsync();

            await players.DeleteAccountAsync();

            Assert.That(server.Requests[1].Method, Is.EqualTo("DELETE"));
            Assert.That(server.Requests[1].Path, Is.EqualTo("/v1/auth/players/me"));
            Assert.That(reasons, Is.EqualTo(new[] { SignOutReason.Requested }));
        }

        [Test]
        public async Task Signing_out_other_devices_keeps_this_one_and_everywhere_ends_it()
        {
            server.Answer(201, SessionJson());
            server.Answer(204, "");
            server.Answer(204, "");
            var players = Launch();
            await players.SignInAnonymouslyAsync();

            await players.SignOutAsync(SignOutScope.OtherDevices);
            Assert.That(players.IsSignedIn, Is.True);
            await players.SignOutAsync(SignOutScope.Everywhere);

            Assert.That(server.Requests[1].Body, Is.EqualTo("{\"keep_current\":true}"));
            Assert.That(server.Requests[2].Body, Is.EqualTo("{\"keep_current\":false}"));
            Assert.That(players.IsSignedIn, Is.False);
        }

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
