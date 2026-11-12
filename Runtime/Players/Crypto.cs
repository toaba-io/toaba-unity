using System;
using System.Security.Cryptography;
using System.Text;

namespace Toaba
{
    internal static class Crypto
    {
        /// <summary>32 random bytes as 43 URL-safe characters.</summary>
        public static string RandomUrlSafe()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return Base64Url(bytes);
        }

        public static byte[] Sha256(string text)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        public static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
