using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Toaba.Http;
using UnityEngine;
using UnityEngine.Scripting;

namespace Toaba
{
    public sealed partial class PlayersService
    {
        static readonly string[] ReturnParameters = { "code", "error", "error_code", "error_description", "flow_id" };

        // Read at startup, before a profile is picked, so it isn't per profile. Profiles can't contain dots.
        const string BrowserKey = "browser.pkce";

        /// <summary>
        /// Signs in on the provider's page: the system browser on desktop, or this page on WebGL,
        /// which finishes when it loads again (see <see cref="ReadyAsync"/>).
        /// </summary>
        public Awaitable<EmailVerification> SignInWithBrowserAsync(string provider, CancellationToken cancellation = default) =>
            BrowserAsync(provider, linking: false, cancellation);

        /// <summary>Like <see cref="SignInWithBrowserAsync"/>, but adds the account to the signed-in player.</summary>
        public Awaitable<EmailVerification> LinkWithBrowserAsync(string provider, CancellationToken cancellation = default) =>
            BrowserAsync(provider, linking: true, cancellation);

        async Awaitable<EmailVerification> BrowserAsync(string provider, bool linking, CancellationToken cancellation)
        {
            await ready;
            if (!linking)
                EnsureSignedOut();
            var verifier = Crypto.RandomUrlSafe();
            if (browser.PageUrl is { } page)
            {
                Save(BrowserKey, new BrowserAttempt { Profile = Profile, Verifier = verifier, Linking = linking });
                browser.Open(await AuthorizeUrlAsync(provider, WithoutReturn(page), verifier, linking, cancellation));
                var leaving = new AwaitableCompletionSource<EmailVerification>();
                using (cancellation.Register(() => leaving.TrySetCanceled()))
                    return await leaving.Awaitable;
            }
            using var callback = new LoopbackCallback();
            browser.Open(await AuthorizeUrlAsync(provider, callback.Url, verifier, linking, cancellation));
            return await FinishInBrowserAsync(await callback.WaitAsync(cancellation), verifier, linking, cancellation);
        }

        // ReadyAsync throws the failure.
        async Task<Exception> StartUpAsync()
        {
            var page = browser.PageUrl;
            if (page == null)
                return null;
            var result = ApiClient.ParseQuery(page.Split('#')[0]);
            if (result["code"] == null && result["error"] == null)
                return null;
            browser.ReplacePageUrl(WithoutReturn(page));
            var attempt = Load<BrowserAttempt>(BrowserKey);
            if (attempt == null)
                return null;
            Forget(BrowserKey);
            if (attempt.Profile != Profile)
            {
                Profile = attempt.Profile;
                Restore();
            }
            try
            {
                await FinishInBrowserAsync(result, attempt.Verifier, attempt.Linking, CancellationToken.None);
                return null;
            }
            catch (Exception error)
            {
                return error;
            }
        }

        static string WithoutReturn(string url)
        {
            url = url.Split('#')[0];
            var start = url.IndexOf('?');
            if (start < 0)
                return url;
            var query = ApiClient.ParseQuery(url);
            foreach (var name in ReturnParameters)
                query.Remove(name);
            return query.Count == 0 ? url.Substring(0, start) : url.Substring(0, start) + "?" + query;
        }

        // Linking needs the player's token, so Toaba builds that URL.
        async Awaitable<string> AuthorizeUrlAsync(
            string provider, string redirect, string verifier, bool linking, CancellationToken cancellation)
        {
            var query = new[]
            {
                ("provider", provider),
                ("redirect_to", redirect),
                ("code_challenge", Crypto.Base64Url(Crypto.Sha256(verifier))),
                ("code_challenge_method", "S256"),
            };
            if (!linking)
                return api.BrowserUrl("/v1/auth/authorize", query);
            var path = "/v1/auth/players/me/identities/authorize?" +
                       ApiClient.QueryString(query.Concat(new[] { ("skip_http_redirect", "true") }));
            return (await SendAuthorizedAsync("GET", path, null, cancellation)).Data<AuthorizeUrl>().Url;
        }

        // PKCE: only this verifier can trade the code for tokens.
        async Awaitable<EmailVerification> FinishInBrowserAsync(
            NameValueCollection result, string verifier, bool linking, CancellationToken cancellation)
        {
            var code = result["code"];
            if (code != null)
            {
                var response = await ExchangeAsync(
                    "/v1/auth/sessions", new { AuthCode = code, CodeVerifier = verifier }, linking, cancellation);
                return Verification(null, CodeIn(response), linking);
            }
            var error = result["error_code"] ?? result["error"] ?? ErrorCodes.UnexpectedResponse;
            var flowId = result["flow_id"];
            if (error == ErrorCodes.EmailConfirmationRequired && flowId != null)
                return Verification(null, new PendingCode { Id = flowId }, linking);
            throw new ToabaException(error, result["error_description"] ?? $"Browser sign-in failed: {error}");
        }

        [Preserve]
        sealed class AuthorizeUrl
        {
            public string Url;
        }

        [Preserve]
        sealed class BrowserAttempt
        {
            public string Profile;
            public string Verifier;
            public bool Linking;
        }
    }
}
