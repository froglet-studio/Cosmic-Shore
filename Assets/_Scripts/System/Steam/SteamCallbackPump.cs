// Same platforms as SteamPlatform.cs: the whole type exists only where the Steam client runs.
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX) && !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Delivers Steam's callbacks (the Web API ticket among them) once a frame while the Steam API runs. Created by
    /// <see cref="SteamPlatform.TryStart"/>; never placed in a scene.
    /// </summary>
    sealed class SteamCallbackPump : MonoBehaviour
    {
        static SteamCallbackPump s_instance;

        public static void Create()
        {
            if (s_instance) return;
            var go = new GameObject(nameof(SteamCallbackPump));
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<SteamCallbackPump>();
        }

        void Update()
        {
            if (SteamPlatform.IsRunning)
                SteamAPI.RunCallbacks();
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
#endif
