#if !UNITY_EDITOR && UNITY_WEBGL
using System.IO;
using System.Runtime.InteropServices;

namespace Toaba
{
    // Keeps values in the browser's localStorage, as Supabase's JavaScript client does: a page has
    // nothing more private.
    internal sealed class BrowserStore : ISecureStore
    {
        readonly string prefix;

        public BrowserStore(string prefix) => this.prefix = prefix;

        public string Read(string key)
        {
            var value = ToabaStorageRead(prefix + key, out var failed);
            if (failed != 0)
                throw Refused();
            return value;
        }

        public void Write(string key, string value)
        {
            if (ToabaStorageWrite(prefix + key, value) == 0)
                throw Refused();
        }

        public void Delete(string key)
        {
            if (ToabaStorageDelete(prefix + key) == 0)
                throw Refused();
        }

        static IOException Refused() => new IOException("The browser didn't allow access to localStorage.");

        [DllImport("__Internal")]
        static extern string ToabaStorageRead(string key, out int failed);

        [DllImport("__Internal")]
        static extern int ToabaStorageWrite(string key, string value);

        [DllImport("__Internal")]
        static extern int ToabaStorageDelete(string key);
    }
}
#endif
