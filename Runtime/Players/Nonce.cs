using System;
using System.Security.Cryptography;
using System.Text;

namespace Toaba
{
    /// <summary>
    /// A one-time value that ties a native ID token to this sign-in. Give <see cref="Hashed"/> to
    /// Google's or Apple's SDK and <see cref="Raw"/> to Toaba.
    /// </summary>
    public sealed class Nonce
    {
        public string Raw { get; }

        /// <summary>The lowercase SHA-256 hex digest of <see cref="Raw"/>.</summary>
        public string Hashed { get; }

        internal Nonce(string raw)
        {
            Raw = raw;
            using var sha256 = SHA256.Create();
            Hashed = BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-", "").ToLowerInvariant();
        }

        public static Nonce Create()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create())
                random.GetBytes(bytes);
            return new Nonce(Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'));
        }
    }
}
