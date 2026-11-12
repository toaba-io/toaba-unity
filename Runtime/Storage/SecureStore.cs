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
            var folder = Path.Combine(Application.persistentDataPath, "Toaba", appId);
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
            return new DpapiStore(new FileStore(folder));
#else
            return new FileStore(folder);
#endif
        }
    }
}
