using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

namespace Toaba
{
    /// <summary>
    /// A code emailed to the player, waiting to be entered. It survives restarts: see
    /// <see cref="PlayersService.PendingEmail"/>.
    /// </summary>
    public sealed class EmailVerification
    {
        readonly PlayersService players;
        internal readonly PendingCode Pending;

        /// <summary>Where the code went, or null when it went to a Google or Apple account's email.</summary>
        public string Email { get; }

        /// <summary>False when the player is already done, which is the usual case.</summary>
        public bool NeedsCode => Pending != null;

        /// <summary>When the code stops working, when known.</summary>
        public DateTimeOffset? ExpireTime => Pending?.ExpireTime;

        internal EmailVerification(PlayersService players, string email, PendingCode pending)
        {
            this.players = players;
            Email = email;
            Pending = pending;
        }

        /// <summary>
        /// Enters the code the player received. A wrong or expired code throws ToabaException with
        /// <see cref="ErrorCodes.InvalidCode"/>, and the player can try again.
        /// </summary>
        public Awaitable VerifyAsync(string code, CancellationToken cancellation = default) =>
            players.VerifyAsync(this, code, cancellation);
    }

    // A flow from Toaba plus what's needed to resume it after a restart.
    [Preserve]
    internal sealed class PendingCode
    {
        public string Id;
        public DateTimeOffset? ExpireTime;
        public string Email;

        /// <summary>True when it adds the email to the signed-in player rather than signing in.</summary>
        public bool Linking;
    }
}
