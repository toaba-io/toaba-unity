using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Toaba
{
    // The x-toaba-* headers that describe the game on Players requests.
    internal static class ClientContext
    {
        const string InstallIdKey = "toaba.install_id";

        public static Dictionary<string, string> Headers()
        {
            var (platform, manufacturer) = Platform();
            var headers = new Dictionary<string, string>();
            Add(headers, "x-toaba-platform", platform);
            Add(headers, "x-toaba-os-version", SystemInfo.operatingSystem);
            Add(headers, "x-toaba-device", SystemInfo.deviceModel);
            Add(headers, "x-toaba-manufacturer", manufacturer);
            Add(headers, "x-toaba-build", Application.version);
            Add(headers, "x-toaba-sdk", Sdk.Name + "/" + Sdk.Version);
            Add(headers, "x-toaba-locale", Locale());
            Add(headers, "x-toaba-install-id", InstallId());
            return headers;
        }

        static (string, string) Platform() => Application.platform switch
        {
            RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => ("windows", null),
            RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => ("macos", "Apple"),
            RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => ("linux", null),
            RuntimePlatform.WebGLPlayer => ("web", null),
            RuntimePlatform.IPhonePlayer => ("ios", "Apple"),
            RuntimePlatform.Android => ("android", AndroidManufacturer()),
            RuntimePlatform.Switch => ("switch", "Nintendo"),
            RuntimePlatform.PS4 or RuntimePlatform.PS5 => ("playstation", "Sony"),
            RuntimePlatform.GameCoreXboxOne or RuntimePlatform.GameCoreXboxSeries => ("xbox", "Microsoft"),
            _ => (null, null),
        };

        // CultureInfo is unreliable on mobile and empty on WebGL.
        static string Locale()
        {
#if !UNITY_EDITOR && UNITY_IOS
            return ToabaPreferredLanguage();
#elif !UNITY_EDITOR && UNITY_ANDROID
            using var locales = new AndroidJavaClass("java.util.Locale");
            using var locale = locales.CallStatic<AndroidJavaObject>("getDefault");
            return locale.Call<string>("toLanguageTag");
#elif !UNITY_EDITOR && UNITY_WEBGL
            return ToabaPageLanguage();
#else
            return CultureInfo.CurrentUICulture.Name;
#endif
        }

        static string AndroidManufacturer()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            using var build = new AndroidJavaClass("android.os.Build");
            return build.GetStatic<string>("MANUFACTURER");
#else
            return null;
#endif
        }

        // PlayerPrefs, unlike the secure store, is cleared when the game is uninstalled.
        static string InstallId()
        {
            var id = PlayerPrefs.GetString(InstallIdKey);
            if (id.Length == 0)
            {
                id = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(InstallIdKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }

        // Header values must be printable ASCII.
        static void Add(Dictionary<string, string> headers, string name, string value)
        {
            var clean = new string((value ?? "").Where(c => c >= ' ' && c <= '~').ToArray()).Trim();
            if (clean.Length > 0)
                headers[name] = clean;
        }

#if !UNITY_EDITOR && UNITY_IOS
        [DllImport("__Internal")]
        static extern string ToabaPreferredLanguage();
#elif !UNITY_EDITOR && UNITY_WEBGL
        [DllImport("__Internal")]
        static extern string ToabaPageLanguage();
#endif
    }
}
