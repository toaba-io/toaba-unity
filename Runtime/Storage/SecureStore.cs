using System.IO;
using UnityEngine;

namespace Toaba
{
    // Where the SDK keeps secrets on this device, such as each profile's session.
    internal interface ISecureStore
    {
        /// <summary>The value saved under the key, or null when there is none.</summary>
        string Read(string key);

        void Write(string key, string value);
        void Delete(string key);
    }

    internal static class SecureStore
    {
        // Chosen at compile time, checking the Editor before the build target: a Linux Editor
        // targeting Windows still defines UNITY_STANDALONE_WIN.
        public static ISecureStore ForPlatform(string appId)
        {
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
            return new DpapiStore(new FileStore(Folder(appId)));
#elif UNITY_EDITOR_OSX || (!UNITY_EDITOR && UNITY_STANDALONE_OSX)
            return new KeychainStore($"io.toaba.{appId}");
#elif !UNITY_EDITOR && UNITY_WEBGL
            return new BrowserStore($"toaba.{appId}.");
#else
            return new FileStore(Folder(appId));
#endif
        }

        static string Folder(string appId) => Path.Combine(Application.persistentDataPath, "Toaba", appId);
    }
}
