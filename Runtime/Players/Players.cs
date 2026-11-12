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

        /// <inheritdoc cref="PlayersService.SignInAnonymouslyAsync"/>
        public static Awaitable SignInAnonymouslyAsync(CancellationToken cancellation = default) =>
            Service.SignInAnonymouslyAsync(cancellation);

        /// <inheritdoc cref="PlayersService.GetAccessTokenAsync"/>
        public static Awaitable<string> GetAccessTokenAsync() => Service.GetAccessTokenAsync();

        /// <inheritdoc cref="PlayersService.SignOutAsync"/>
        public static Awaitable SignOutAsync(CancellationToken cancellation = default) =>
            Service.SignOutAsync(cancellation);
    }
}
