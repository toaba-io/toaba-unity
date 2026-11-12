using System.Runtime.InteropServices;
using UnityEngine;

namespace Toaba
{
    internal interface IBrowser
    {
        /// <summary>Null outside a browser.</summary>
        string PageUrl { get; }

        void Open(string url);
        void ReplacePageUrl(string url);
    }

    internal static class Browser
    {
        public static IBrowser ForPlatform()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            return new WebPage();
#else
            return new SystemBrowser();
#endif
        }
    }

    internal sealed class SystemBrowser : IBrowser
    {
        public string PageUrl => null;
        public void Open(string url) => Application.OpenURL(url);
        public void ReplacePageUrl(string url) { }
    }

#if !UNITY_EDITOR && UNITY_WEBGL
    // Application.OpenURL would open a new tab.
    internal sealed class WebPage : IBrowser
    {
        public string PageUrl => Application.absoluteURL;
        public void Open(string url) => ToabaPageNavigate(url);
        public void ReplacePageUrl(string url) => ToabaPageReplace(url);

        [DllImport("__Internal")]
        static extern void ToabaPageNavigate(string url);

        [DllImport("__Internal")]
        static extern void ToabaPageReplace(string url);
    }
#endif
}
