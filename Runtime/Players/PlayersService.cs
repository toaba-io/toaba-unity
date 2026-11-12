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
        bool signingIn;

        /// <summary>Raised when the player signs in, not when a saved session is restored.</summary>
        public event Action SignedIn;

        public event Action<SignOutReason> SignedOut;

        /// <summary>True from startup when a session was saved on an earlier launch.</summary>
        public bool IsSignedIn => session != null;

        public string PlayerId => session?.PlayerId;

        /// <summary>Which saved player on this device is in use. See <see cref="SwitchProfile"/>.</summary>
        public string Profile { get; private set; } = "default";

        internal PlayersService(ApiClient api, ISecureStore store)
        {
            this.api = api;
            this.store = store;
            session = Restore();
        }

        /// <summary>
        /// Switches to another saved player on this device, such as a second player for testing,
        /// and restores its session if it has one. The current player stays saved under its profile.
        /// </summary>
        public void SwitchProfile(string profile)
        {
            if (!ProfileName.IsMatch(profile ?? ""))
                throw new ArgumentException("A profile is 1 to 30 lowercase letters, digits, '-' or '_'.", nameof(profile));
            if (signingIn || session?.Refreshing != null)
                throw new InvalidOperationException("Wait for the sign-in or refresh to finish.");
            Profile = profile;
            session = Restore();
        }

        /// <summary>
        /// Signs in as a new guest player. Throws InvalidOperationException when a player is
        /// signed in, because the guest's progress would be lost.
        /// </summary>
        public async Awaitable SignInAnonymouslyAsync(CancellationToken cancellation = default)
        {
            if (session != null || signingIn)
                throw new InvalidOperationException("A player is already signed in. Sign out first.");
            signingIn = true;
            try
            {
                Adopt(await api.SendAsync<Session>(
                    "POST", "/v1/auth/sessions", new { Anonymous = true }, cancellation: cancellation));
                SignedIn?.Invoke();
            }
            finally
            {
                signingIn = false;
            }
        }

        /// <summary>
        /// The player's access token, for your own servers. It's refreshed first when it expires
        /// within 5 minutes.
        /// </summary>
        public async Awaitable<string> GetAccessTokenAsync()
        {
            var current = session ?? throw new InvalidOperationException("No player is signed in.");
            if (current.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds() > RefreshMarginSeconds)
                return current.AccessToken;
            return (await Refresh(current)).AccessToken;
        }

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
                    catch (ToabaException error) when (error.Code == "session_busy" && attempt == 1)
                    {
                        await api.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
                    }
                    catch (ToabaException error) when (
                        error.Code is "invalid_refresh_token" or "refresh_token_reused" or "session_expired")
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
            next.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + next.AccessTokenTtlSeconds;
            session = next;
            UseStore("save", () => store.Write(Profile, Json.Serialize(next)));
        }

        void End(SignOutReason reason)
        {
            session = null;
            UseStore("delete", () => store.Delete(Profile));
            SignedOut?.Invoke(reason);
        }

        Session Restore()
        {
            string saved = null;
            UseStore("read", () => saved = store.Read(Profile));
            if (saved == null)
                return null;
            try
            {
                var restored = Json.Deserialize<Session>(saved);
                if (!string.IsNullOrEmpty(restored?.RefreshToken))
                    return restored;
            }
            catch (JsonException)
            {
            }
            UseStore("delete", () => store.Delete(Profile));
            return null;
        }

        // A failing store mustn't fail the call: the session still works until the game quits.
        static void UseStore(string action, Action use)
        {
            try
            {
                use();
            }
            catch (Exception error)
            {
                Debug.LogError($"Toaba couldn't {action} the saved session: {error.Message}");
            }
        }
    }
}
