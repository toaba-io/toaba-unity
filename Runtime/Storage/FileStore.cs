using System.IO;
using System.Runtime.InteropServices;

namespace Toaba
{
    // One file per key, replaced whole on each write. On Linux only the user can read it.
    internal sealed class FileStore : ISecureStore
    {
        readonly string folder;

        public FileStore(string folder) => this.folder = folder;

        public string Read(string key)
        {
            var path = PathOf(key);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        public void Write(string key, string value)
        {
            Directory.CreateDirectory(folder);
            var path = PathOf(key);
            var temp = path + ".tmp";
            File.WriteAllText(temp, "");
            OwnerOnly(temp);
            File.WriteAllText(temp, value);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        public void Delete(string key) => File.Delete(PathOf(key));

        string PathOf(string key) => Path.Combine(folder, key);

#if UNITY_EDITOR_LINUX || (!UNITY_EDITOR && UNITY_STANDALONE_LINUX)
        const uint OwnerReadWrite = 0b110_000_000; // 0600

        [DllImport("libc.so.6", SetLastError = true)]
        static extern int chmod(string path, uint mode);

        static void OwnerOnly(string path)
        {
            if (chmod(path, OwnerReadWrite) != 0)
                throw new IOException($"Couldn't make {path} private (errno {Marshal.GetLastWin32Error()}).");
        }
#else
        static void OwnerOnly(string path)
        {
        }
#endif
    }
}
