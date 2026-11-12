using System;
using System.Threading;
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
        readonly ApiClient api;
        readonly ISessionStore store;
        Session session;
        bool signingIn;

        /// <summary>Raised when the player signs in, not when a saved session is restored.</summary>
        public event Action SignedIn;

        public event Action<SignOutReason> SignedOut;

        /// <summary>True from startup when a session was saved on an earlier launch.</summary>
        public bool IsSignedIn => session != null;

        public string PlayerId => session?.PlayerId;

        internal PlayersService(ApiClient api, ISessionStore store)
        {
            this.api = api;
            this.store = store;
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
                var started = await api.SendAsync<Session>(
                    "POST", "/v1/auth/sessions", new { Anonymous = true }, cancellation: cancellation);
                Start(started);
            }
            finally
            {
                signingIn = false;
            }
        }

        /// <summary>Signs the player out on this device, even when Toaba can't be reached.</summary>
        public async Awaitable SignOutAsync(CancellationToken cancellation = default)
        {
            if (session == null)
                return;
            var ending = session;
            session = null;
            store.Clear();
            try
            {
                await api.SendAsync(
                    "DELETE", "/v1/auth/sessions/current", accessToken: ending.AccessToken, cancellation: cancellation);
            }
            catch (ToabaException)
            {
            }
            finally
            {
                SignedOut?.Invoke(SignOutReason.Requested);
            }
        }

        void Start(Session started)
        {
            started.ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + started.AccessTokenTtlSeconds;
            store.Save(Json.Serialize(started));
            session = started;
            SignedIn?.Invoke();
        }

        Session Restore()
        {
            var saved = store.Load();
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
            store.Clear();
            return null;
        }
    }
}
