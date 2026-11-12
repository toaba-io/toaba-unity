using System;

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
            Hashed = BitConverter.ToString(Crypto.Sha256(raw)).Replace("-", "").ToLowerInvariant();
        }

        public static Nonce Create() => new Nonce(Crypto.RandomUrlSafe());
    }
}
