using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace Toaba
{
    [Preserve]
    internal sealed class Session
    {
        public string PlayerId;
        public string AccessToken;
        public string RefreshToken;
        public int AccessTokenTtlSeconds;

        /// <summary>When the access token expires, in Unix seconds.</summary>
        public long ExpiresAt;

        /// <summary>The refresh in flight from this session, shared by its callers. Not saved.</summary>
        internal Task<Session> Refreshing;
    }
}
