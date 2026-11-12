#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && UNITY_STANDALONE_OSX)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Toaba
{
    // Keeps each value as a generic password in the user's Keychain. It uses the legacy SecKeychain
    // calls throughout, as MSAL does for writes, to keep the P/Invoke small.
    internal sealed class KeychainStore : ISecureStore
    {
        const string Security = "/System/Library/Frameworks/Security.framework/Security";
        const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        const int ItemNotFound = -25300;

        readonly byte[] service;

        public KeychainStore(string service) => this.service = Encoding.UTF8.GetBytes(service);

        public string Read(string key)
        {
            var account = Encoding.UTF8.GetBytes(key);
            var status = SecKeychainFindGenericPassword(
                IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
                out var length, out var data, IntPtr.Zero);
            if (status == ItemNotFound)
                return null;
            Check(status);
            try
            {
                var bytes = new byte[length];
                Marshal.Copy(data, bytes, 0, bytes.Length);
                return Encoding.UTF8.GetString(bytes);
            }
            finally
            {
                SecKeychainItemFreeContent(IntPtr.Zero, data);
            }
        }

        public void Write(string key, string value)
        {
            var account = Encoding.UTF8.GetBytes(key);
            var secret = Encoding.UTF8.GetBytes(value);
            var item = FindItem(account);
            if (item == IntPtr.Zero)
            {
                Check(SecKeychainAddGenericPassword(
                    IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
                    (uint)secret.Length, secret, IntPtr.Zero));
                return;
            }
            try
            {
                Check(SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)secret.Length, secret));
            }
            finally
            {
                CFRelease(item);
            }
        }

        public void Delete(string key)
        {
            var item = FindItem(Encoding.UTF8.GetBytes(key));
            if (item == IntPtr.Zero)
                return;
            try
            {
                Check(SecKeychainItemDelete(item));
            }
            finally
            {
                CFRelease(item);
            }
        }

        // The account's item, or IntPtr.Zero. It doesn't read the secret, which can prompt for access.
        IntPtr FindItem(byte[] account)
        {
            var status = SecKeychainFindGenericPassword(
                IntPtr.Zero, (uint)service.Length, service, (uint)account.Length, account,
                IntPtr.Zero, IntPtr.Zero, out var item);
            if (status == ItemNotFound)
                return IntPtr.Zero;
            Check(status);
            return item;
        }

        static void Check(int status)
        {
            if (status != 0)
                throw new IOException($"The Keychain refused the request (OSStatus {status}).");
        }

        [DllImport(Security)]
        static extern int SecKeychainFindGenericPassword(
            IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account,
            out uint passwordLength, out IntPtr password, IntPtr item);

        [DllImport(Security)]
        static extern int SecKeychainFindGenericPassword(
            IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account,
            IntPtr passwordLength, IntPtr password, out IntPtr item);

        [DllImport(Security)]
        static extern int SecKeychainAddGenericPassword(
            IntPtr keychain, uint serviceLength, byte[] service, uint accountLength, byte[] account,
            uint passwordLength, byte[] password, IntPtr item);

        [DllImport(Security)]
        static extern int SecKeychainItemModifyAttributesAndData(
            IntPtr item, IntPtr attributes, uint length, byte[] data);

        [DllImport(Security)]
        static extern int SecKeychainItemDelete(IntPtr item);

        [DllImport(Security)]
        static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);

        [DllImport(CoreFoundation)]
        static extern void CFRelease(IntPtr item);
    }
}
#endif
