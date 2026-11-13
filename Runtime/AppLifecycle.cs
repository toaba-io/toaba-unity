using System;
using UnityEngine;

namespace Toaba
{
    // Application.focusChanged alone misses an Android pause with the keyboard open.
    internal sealed class AppLifecycle : MonoBehaviour
    {
        public bool InForeground { get; private set; } = true;

        /// <summary>Raised once per return, though pause and focus often change together.</summary>
        public event Action Resumed;

        public event Action Updated;

        public static AppLifecycle Create()
        {
            var host = new GameObject("Toaba") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(host);
            return host.AddComponent<AppLifecycle>();
        }

        void Update() => Updated?.Invoke();

        void OnApplicationPause(bool paused) => Changed(!paused);

        void OnApplicationFocus(bool focused) => Changed(focused);

        void Changed(bool active)
        {
            if (!active)
                InForeground = false;
            else if (!InForeground)
            {
                InForeground = true;
                Resumed?.Invoke();
            }
        }
    }
}
