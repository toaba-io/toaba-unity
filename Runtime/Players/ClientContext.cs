using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Toaba
{
    // The x-toaba-* headers that describe the game on Players requests.
    internal static class ClientContext
    {
        public static Dictionary<string, string> Headers()
        {
            var headers = new Dictionary<string, string>();
            Add(headers, "x-toaba-platform", Platform());
            Add(headers, "x-toaba-os-version", SystemInfo.operatingSystem);
            Add(headers, "x-toaba-device", SystemInfo.deviceModel);
            Add(headers, "x-toaba-build", Application.version);
            return headers;
        }

        static string Platform() => Application.platform switch
        {
            RuntimePlatform.WindowsPlayer or RuntimePlatform.WindowsEditor => "windows",
            RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor => "macos",
            RuntimePlatform.LinuxPlayer or RuntimePlatform.LinuxEditor => "linux",
            RuntimePlatform.WebGLPlayer => "web",
            RuntimePlatform.IPhonePlayer => "ios",
            RuntimePlatform.Android => "android",
            RuntimePlatform.Switch => "switch",
            RuntimePlatform.PS4 or RuntimePlatform.PS5 => "playstation",
            RuntimePlatform.GameCoreXboxOne or RuntimePlatform.GameCoreXboxSeries => "xbox",
            _ => null,
        };

        // Header values must be printable ASCII.
        static void Add(Dictionary<string, string> headers, string name, string value)
        {
            var clean = new string((value ?? "").Where(c => c >= ' ' && c <= '~').ToArray()).Trim();
            if (clean.Length > 0)
                headers[name] = clean;
        }
    }
}
