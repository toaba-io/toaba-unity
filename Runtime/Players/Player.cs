using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace Toaba
{
    /// <summary>The signed-in player's account.</summary>
    public sealed class Player
    {
        public string Id { get; }

        /// <summary>Shared by the player's apps, or null when not set.</summary>
        public string Username { get; }

        public string Email { get; }
        public bool EmailVerified { get; }

        /// <summary>True for a guest who hasn't added a way to sign in again.</summary>
        public bool IsAnonymous { get; }

        public DateTimeOffset CreateTime { get; }

        [JsonConstructor, Preserve]
        internal Player(string id, string username, string email, bool emailVerified, bool anonymous, DateTimeOffset createTime)
        {
            Id = id;
            Username = username;
            Email = email;
            EmailVerified = emailVerified;
            IsAnonymous = anonymous;
            CreateTime = createTime;
        }
    }

    // Always sends username, so null clears it rather than being left out.
    [Preserve]
    internal sealed class UsernameChange
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string Username;
    }
}
