using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace Toaba
{
    /// <summary>Ways Toaba can sign players in.</summary>
    public static class Provider
    {
        public const string Email = "email";
        public const string Google = "google";
        public const string Apple = "apple";
    }

    /// <summary>One way the player can sign in, such as their email or a Google account.</summary>
    public sealed class Identity
    {
        public string Id { get; }

        /// <summary>One of <see cref="Toaba.Provider"/>.</summary>
        public string Provider { get; }

        public DateTimeOffset CreateTime { get; }

        [JsonConstructor, Preserve]
        internal Identity(string id, string provider, DateTimeOffset createTime)
        {
            Id = id;
            Provider = provider;
            CreateTime = createTime;
        }
    }
}
