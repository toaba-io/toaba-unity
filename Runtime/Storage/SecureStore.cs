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
        public static ISecureStore ForPlatform(string appId) =>
            new FileStore(Path.Combine(Application.persistentDataPath, "Toaba", appId));
    }
}
