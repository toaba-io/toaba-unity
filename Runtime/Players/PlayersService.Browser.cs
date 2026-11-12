using System;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using Toaba.Http;
using UnityEngine;
using UnityEngine.Scripting;

namespace Toaba
{
    public sealed partial class PlayersService
    {
        /// <summary>
        /// Signs in on Google's or Apple's page in the system browser, which returns to the game on
        /// a local port. It waits for the player: cancel it to stop waiting. A code is needed only
        /// when the provider's email isn't verified.
        /// </summary>
        public Awaitable<EmailVerification> SignInWithBrowserAsync(string provider, CancellationToken cancellation = default) =>
            BrowserAsync(provider, linking: false, cancellation);

        /// <summary>
        /// Adds a Google or Apple account to the signed-in player through the system browser. The
        /// player keeps their ID and progress.
        /// </summary>
        public Awaitable<EmailVerification> LinkWithBrowserAsync(string provider, CancellationToken cancellation = default) =>
            BrowserAsync(provider, linking: true, cancellation);

        async Awaitable<EmailVerification> BrowserAsync(string provider, bool linking, CancellationToken cancellation)
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                throw new PlatformNotSupportedException("Browser sign-in isn't available on WebGL yet.");
            if (!linking)
                EnsureSignedOut();
            var verifier = Crypto.RandomUrlSafe();
            using var callback = new LoopbackCallback();
            openBrowser(await AuthorizeUrlAsync(provider, callback.Url, verifier, linking, cancellation));
            return await FinishInBrowserAsync(await callback.WaitAsync(cancellation), verifier, linking, cancellation);
        }

        // Sign-in opens a URL built here; linking asks Toaba for one, with the player's token.
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

        // The browser returns a one-use code, which only this verifier can trade for tokens (PKCE),
        // or an error. An unverified provider email returns a flow for the emailed code instead.
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
    }
}
