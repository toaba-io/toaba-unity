using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Toaba.Http;
using UnityEngine;

namespace Toaba
{
    public enum SignOutReason
    {
        /// <summary>The game signed the player out.</summary>
        Requested,

        /// <summary>Toaba ended the session, for example because it expired or was revoked.</summary>
        SessionEnded,
    }

    /// <summary>The player of one Toaba app. Most games use <see cref="Players"/>.</summary>
    public sealed class PlayersService
    {
        const int RefreshMarginSeconds = 300;
        static readonly Regex ProfileName = new Regex("^[a-z0-9_-]{1,30}$");

        readonly ApiClient api;
        readonly ISecureStore store;
        Session session;
        EmailVerification pendingEmail;
        bool exchanging;

        /// <summary>Raised when the player signs in, not when a saved session is restored.</summary>
        public event Action SignedIn;

        public event Action<SignOutReason> SignedOut;

        /// <summary>True from startup when a session was saved on an earlier launch.</summary>
        public bool IsSignedIn => session != null;

        public string PlayerId => session?.PlayerId;

        /// <summary>Which saved player on this device is in use. See <see cref="SwitchProfile"/>.</summary>
        public string Profile { get; private set; } = "default";

        /// <summary>
        /// The emailed code still waiting to be entered, even after a restart, or null. Show the
        /// code screen again when it's set.
        /// </summary>
        public EmailVerification PendingEmail =>
            pendingEmail != null && pendingEmail.ExpireTime > DateTimeOffset.UtcNow ? pendingEmail : null;

        string PendingKey => Profile + ".email";

        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        internal PlayersService(ApiClient api, ISecureStore store)
        {
            this.api = api;
            this.store = store;
            Restore();
        }

        /// <summary>
        /// Switches to another saved player on this device, such as a second player for testing,
        /// and restores its session if it has one. The current player stays saved under its profile.
        /// </summary>
        public void SwitchProfile(string profile)
        {
            if (!ProfileName.IsMatch(profile ?? ""))
                throw new ArgumentException("A profile is 1 to 30 lowercase letters, digits, '-' or '_'.", nameof(profile));
            if (exchanging || session?.Refreshing != null)
                throw new InvalidOperationException("Wait for the sign-in or refresh to finish.");
            Profile = profile;
            Restore();
        }

        /// <summary>
        /// Signs in as a new guest player. Throws InvalidOperationException when a player is
        /// signed in, because the guest's progress would be lost.
        /// </summary>
        public async Awaitable SignInAnonymouslyAsync(CancellationToken cancellation = default) =>
            await ExchangeAsync("/v1/auth/sessions", new { Anonymous = true }, linking: false, cancellation);

        /// <summary>
        /// Emails the player a code that signs them in, registering them if the address is new.
        /// To keep a guest's progress, use <see cref="AddEmailAsync"/> instead.
        /// </summary>
        public async Awaitable<EmailVerification> SignInWithEmailAsync(string email, CancellationToken cancellation = default)
        {
            var response = await ExchangeAsync("/v1/auth/sessions", new { Email = email }, linking: false, cancellation);
            return response.Status == 202 ? CodeSent(email, response, linking: false) : new EmailVerification(this, email, null);
        }

        /// <summary>
        /// Gives the signed-in guest an email address, emailing a code to confirm it. The player
        /// keeps their ID and progress.
        /// </summary>
        public async Awaitable<EmailVerification> AddEmailAsync(string email, CancellationToken cancellation = default)
        {
            var response = await SendAuthorizedAsync(
                "POST", "/v1/auth/players/me/identities", new { Provider = "email", Email = email }, cancellation);
            if (response.Status == 202)
                return CodeSent(email, response, linking: true);
            Adopt(response.Data<Session>());
            return new EmailVerification(this, email, null);
        }

        /// <summary>
        /// The player's access token, for your own servers. It's refreshed first when it expires
        /// within 5 minutes.
        /// </summary>
        public async Awaitable<string> GetAccessTokenAsync() => (await FreshSessionAsync()).AccessToken;

        /// <summary>Signs the player out on this device, even when Toaba can't be reached.</summary>
        public async Awaitable SignOutAsync(CancellationToken cancellation = default)
        {
            if (session == null)
                return;
            var accessToken = session.AccessToken;
            End(SignOutReason.Requested);
            try
            {
                await api.SendAsync(
                    "DELETE", "/v1/auth/sessions/current", accessToken: accessToken, cancellation: cancellation);
            }
            catch (ToabaException)
            {
            }
        }

        internal async Awaitable VerifyAsync(EmailVerification verification, string code, CancellationToken cancellation)
        {
            var flow = verification.Pending ?? throw new InvalidOperationException("No code is needed for this email.");
            await ExchangeAsync(
                $"/v1/auth/flows/{Uri.EscapeDataString(flow.Id)}/verify", new { Code = code }, flow.Linking, cancellation);
            if (pendingEmail == verification)
                ForgetPending();
        }

        // Trades a credential for tokens: a 201 answer is the session, a 202 a code to enter first.
        // Signing in needs no player and raises SignedIn; linking keeps the signed-in player.
        async Awaitable<ApiResponse> ExchangeAsync(string path, object body, bool linking, CancellationToken cancellation)
        {
            if (exchanging)
                throw new InvalidOperationException("Wait for the sign-in to finish.");
            if (linking && session == null)
                throw new InvalidOperationException("No player is signed in.");
            if (!linking && session != null)
                throw new InvalidOperationException(
                    "A player is already signed in. Sign out first, or use AddEmailAsync to give a guest an email.");
            exchanging = true;
            try
            {
                var response = await api.SendAsync("POST", path, body, cancellation: cancellation);
                if (response.Status == 201)
                {
                    Adopt(response.Data<Session>());
                    if (!linking)
                        SignedIn?.Invoke();
                }
                return response;
            }
            finally
            {
                exchanging = false;
            }
        }

        EmailVerification CodeSent(string email, ApiResponse response, bool linking)
        {
            var flow = response.Data<PendingCode>();
            flow.Email = email;
            flow.Linking = linking;
            Save(PendingKey, flow);
            return pendingEmail = new EmailVerification(this, email, flow);
        }

        // Sends with the player's access token, refreshed first when needed and once more after a 401.
        async Awaitable<ApiResponse> SendAuthorizedAsync(string method, string path, object body, CancellationToken cancellation)
        {
            var used = await FreshSessionAsync();
            try
            {
                return await api.SendAsync(method, path, body, used.AccessToken, cancellation);
            }
            catch (ToabaException error) when (error.Code == ErrorCodes.InvalidAccessToken)
            {
                // Another call may have refreshed meanwhile; refreshing from the old token would end the session.
                var next = session != null && session != used ? session : await Refresh(used);
                return await api.SendAsync(method, path, body, next.AccessToken, cancellation);
            }
        }

        async Awaitable<Session> FreshSessionAsync()
        {
            var current = session ?? throw new InvalidOperationException("No player is signed in.");
            return current.ExpiresAt - Now > RefreshMarginSeconds ? current : await Refresh(current);
        }

        // Shared by concurrent callers, because Toaba ends a session whose refresh token is used
        // twice. Not cancellable: a refresh cut short could leave only the rotated-out token.
        Task<Session> Refresh(Session current) => current.Refreshing ??= RefreshAsync(current);

        async Task<Session> RefreshAsync(Session current)
        {
            try
            {
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        var refreshed = await api.SendAsync<Session>(
                            "POST", "/v1/auth/sessions/refresh", new { current.RefreshToken });
                        if (session == current)
                            Adopt(refreshed);
                        return refreshed;
                    }
                    catch (ToabaException error) when (error.Code == ErrorCodes.SessionBusy && attempt == 1)
                    {
                        await api.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
                    }
                    catch (ToabaException error) when (error.Code is ErrorCodes.InvalidRefreshToken
                                                       or ErrorCodes.RefreshTokenReused or ErrorCodes.SessionExpired)
                    {
                        if (session == current)
                            End(SignOutReason.SessionEnded);
                        throw;
                    }
                }
            }
            finally
            {
                current.Refreshing = null;
            }
        }

        void Adopt(Session next)
        {
            next.ExpiresAt = Now + next.AccessTokenTtlSeconds;
            session = next;
            Save(Profile, next);
        }

        void End(SignOutReason reason)
        {
            session = null;
            Forget(Profile);
            ForgetPending();
            SignedOut?.Invoke(reason);
        }

        void ForgetPending()
        {
            pendingEmail = null;
            Forget(PendingKey);
        }

        void Restore()
        {
            session = Load<Session>(Profile);
            if (session != null && string.IsNullOrEmpty(session.RefreshToken))
            {
                session = null;
                Forget(Profile);
            }
            var flow = Load<PendingCode>(PendingKey);
            pendingEmail = flow == null ? null : new EmailVerification(this, flow.Email, flow);
        }

        T Load<T>(string key) where T : class
        {
            string saved = null;
            UseStore("read", () => saved = store.Read(key));
            if (saved == null)
                return null;
            try
            {
                return Json.Deserialize<T>(saved);
            }
            catch (JsonException)
            {
                Forget(key);
                return null;
            }
        }

        void Save(string key, object value) => UseStore("save", () => store.Write(key, Json.Serialize(value)));

        void Forget(string key) => UseStore("delete", () => store.Delete(key));

        // A failing store mustn't fail the call: the session still works until the game quits.
        static void UseStore(string action, Action use)
        {
            try
            {
                use();
            }
            catch (Exception error)
            {
                Debug.LogError($"Toaba couldn't {action} its saved data: {error.Message}");
            }
        }
    }
}
