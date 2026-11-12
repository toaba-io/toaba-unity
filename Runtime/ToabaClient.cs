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

        internal ApiClient Api { get; }

        public ToabaClient(string appId, string publishableKey)
        {
            if (string.IsNullOrWhiteSpace(appId))
                throw new ArgumentException("An app ID is required.", nameof(appId));
            if (string.IsNullOrWhiteSpace(publishableKey))
                throw new ArgumentException("A publishable key is required.", nameof(publishableKey));
            AppId = appId.Trim();
            Api = new ApiClient(publishableKey.Trim());
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

        // Statics survive entering Play Mode when domain reload is off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetShared() => shared = null;
    }
}
