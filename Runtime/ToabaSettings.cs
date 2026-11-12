using UnityEngine;

namespace Toaba
{
    // Edited in Project Settings → Toaba. It lives in a Resources folder so builds include it.
    internal sealed class ToabaSettings : ScriptableObject
    {
        public const string ResourceName = "ToabaSettings";

        [SerializeField] string appId;
        [SerializeField] string publishableKey;

        public string AppId => appId;
        public string PublishableKey => publishableKey;

        public static ToabaSettings Load() => Resources.Load<ToabaSettings>(ResourceName);
    }
}
