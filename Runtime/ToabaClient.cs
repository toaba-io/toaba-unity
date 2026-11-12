using System;
using Toaba.Http;
using UnityEngine;

namespace Toaba
{
    /// <summary>
    /// One Toaba app. The static services use the app set in Project Settings → Toaba; create a
    /// client only to use another app.
    /// </summary>
    public sealed class ToabaClient
    {
        static ToabaClient shared;

        public string AppId { get; }

        public PlayersService Players { get; }

        public ToabaClient(string appId, string publishableKey)
            : this(
                Required(appId, nameof(appId)),
                new ApiClient(Required(publishableKey, nameof(publishableKey)), headers: ClientContext.Headers()),
                null)
        {
        }

        internal ToabaClient(string appId, ApiClient api, ISecureStore store, IBrowser browser = null)
        {
            AppId = appId;
            Players = new PlayersService(api, store ?? SecureStore.ForPlatform(appId), browser);
        }

        internal static ToabaClient Shared => shared ??= FromSettings();

        static ToabaClient FromSettings()
        {
            var settings = ToabaSettings.Load();
            if (settings == null || string.IsNullOrWhiteSpace(settings.AppId) || string.IsNullOrWhiteSpace(settings.PublishableKey))
                throw new InvalidOperationException(
                    "Toaba isn't set up: enter the app ID and publishable key in Project Settings → Toaba.");
            return new ToabaClient(settings.AppId, settings.PublishableKey);
        }

        static string Required(string value, string name) =>
            string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();

        // Statics survive entering Play Mode when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared() => shared = null;
    }
}
