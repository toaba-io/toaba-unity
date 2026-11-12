#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Toaba
{
    // Encrypts values to the Windows user with DPAPI, making the calls .NET's ProtectedData makes,
    // which .NET Standard 2.1 doesn't expose.
    internal sealed class DpapiStore : ISecureStore
    {
        const int UiForbidden = 0x1;

        readonly ISecureStore inner;

        public DpapiStore(ISecureStore inner) => this.inner = inner;

        public string Read(string key)
        {
            var saved = inner.Read(key);
            return saved == null ? null : Encoding.UTF8.GetString(Transform(Convert.FromBase64String(saved), protect: false));
        }

        public void Write(string key, string value) =>
            inner.Write(key, Convert.ToBase64String(Transform(Encoding.UTF8.GetBytes(value), protect: true)));

        public void Delete(string key) => inner.Delete(key);

        static byte[] Transform(byte[] data, bool protect)
        {
            // DPAPI rejects a null buffer, even for no data.
            var input = new DataBlob { Size = (uint)data.Length, Data = Marshal.AllocHGlobal(Math.Max(data.Length, 1)) };
            var output = default(DataBlob);
            try
            {
                Marshal.Copy(data, 0, input.Data, data.Length);
                var done = protect
                    ? CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, out output);
                if (!done)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                var result = new byte[output.Size];
                Marshal.Copy(output.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.Data);
                LocalFree(output.Data);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DataBlob
        {
            public uint Size;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", SetLastError = true)]
        static extern bool CryptProtectData(
            ref DataBlob dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true)]
        static extern bool CryptUnprotectData(
            ref DataBlob dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr memory);
    }
}
#endif
