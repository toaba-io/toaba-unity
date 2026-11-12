using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
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

    internal interface ISessionStore
    {
        /// <summary>The saved session, or null when there is none.</summary>
        string Load();

        void Save(string session);
        void Clear();
    }

    internal sealed class FileSessionStore : ISessionStore
    {
        readonly string path;

        public FileSessionStore(string appId) =>
            path = Path.Combine(Application.persistentDataPath, "Toaba", appId, "session.json");

        public string Load() => File.Exists(path) ? File.ReadAllText(path) : null;

        public void Save(string session)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, session);
        }

        public void Clear() => File.Delete(path);
    }
}
