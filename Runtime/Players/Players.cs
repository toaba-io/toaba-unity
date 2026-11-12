using System;
using System.Threading;
using UnityEngine;

namespace Toaba
{
    /// <summary>The player, for the app set in Project Settings → Toaba.</summary>
    public static class Players
    {
        static PlayersService Service => ToabaClient.Shared.Players;

        /// <inheritdoc cref="PlayersService.SignedIn"/>
        public static event Action SignedIn
        {
            add => Service.SignedIn += value;
            remove => Service.SignedIn -= value;
        }

        /// <inheritdoc cref="PlayersService.SignedOut"/>
        public static event Action<SignOutReason> SignedOut
        {
            add => Service.SignedOut += value;
            remove => Service.SignedOut -= value;
        }

        /// <inheritdoc cref="PlayersService.IsSignedIn"/>
        public static bool IsSignedIn => Service.IsSignedIn;

        /// <inheritdoc cref="PlayersService.PlayerId"/>
        public static string PlayerId => Service.PlayerId;

        /// <inheritdoc cref="PlayersService.Profile"/>
        public static string Profile => Service.Profile;

        /// <inheritdoc cref="PlayersService.SwitchProfile"/>
        public static void SwitchProfile(string profile) => Service.SwitchProfile(profile);

        /// <inheritdoc cref="PlayersService.SignInAnonymouslyAsync"/>
        public static Awaitable SignInAnonymouslyAsync(CancellationToken cancellation = default) =>
            Service.SignInAnonymouslyAsync(cancellation);

        /// <inheritdoc cref="PlayersService.PendingEmail"/>
        public static EmailVerification PendingEmail => Service.PendingEmail;

        /// <inheritdoc cref="PlayersService.SignInWithEmailAsync"/>
        public static Awaitable<EmailVerification> SignInWithEmailAsync(string email, CancellationToken cancellation = default) =>
            Service.SignInWithEmailAsync(email, cancellation);

        /// <inheritdoc cref="PlayersService.AddEmailAsync"/>
        public static Awaitable<EmailVerification> AddEmailAsync(string email, CancellationToken cancellation = default) =>
            Service.AddEmailAsync(email, cancellation);

        /// <inheritdoc cref="PlayersService.SignInWithIdTokenAsync"/>
        public static Awaitable<EmailVerification> SignInWithIdTokenAsync(
            string provider, string idToken, string nonce = null, CancellationToken cancellation = default) =>
            Service.SignInWithIdTokenAsync(provider, idToken, nonce, cancellation);

        /// <inheritdoc cref="PlayersService.LinkIdTokenAsync"/>
        public static Awaitable<EmailVerification> LinkIdTokenAsync(
            string provider, string idToken, string nonce = null, CancellationToken cancellation = default) =>
            Service.LinkIdTokenAsync(provider, idToken, nonce, cancellation);

        /// <inheritdoc cref="PlayersService.GetIdentitiesAsync"/>
        public static Awaitable<Identity[]> GetIdentitiesAsync(CancellationToken cancellation = default) =>
            Service.GetIdentitiesAsync(cancellation);

        /// <inheritdoc cref="PlayersService.UnlinkAsync"/>
        public static Awaitable UnlinkAsync(Identity identity, CancellationToken cancellation = default) =>
            Service.UnlinkAsync(identity, cancellation);

        /// <inheritdoc cref="PlayersService.GetAccessTokenAsync"/>
        public static Awaitable<string> GetAccessTokenAsync() => Service.GetAccessTokenAsync();

        /// <inheritdoc cref="PlayersService.GetPlayerAsync"/>
        public static Awaitable<Player> GetPlayerAsync(CancellationToken cancellation = default) =>
            Service.GetPlayerAsync(cancellation);

        /// <inheritdoc cref="PlayersService.SetUsernameAsync"/>
        public static Awaitable<Player> SetUsernameAsync(string username, CancellationToken cancellation = default) =>
            Service.SetUsernameAsync(username, cancellation);

        /// <inheritdoc cref="PlayersService.DeleteAccountAsync"/>
        public static Awaitable DeleteAccountAsync(CancellationToken cancellation = default) =>
            Service.DeleteAccountAsync(cancellation);

        /// <inheritdoc cref="PlayersService.SignOutAsync"/>
        public static Awaitable SignOutAsync(SignOutScope scope = SignOutScope.ThisDevice, CancellationToken cancellation = default) =>
            Service.SignOutAsync(scope, cancellation);
    }
}
