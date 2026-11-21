using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Lets a key press start FMOD's audio in a WebGL player, as a click or a touch already does.
    ///
    /// <para>Browsers keep a page's audio suspended until a user gesture. FMOD's HTML5 output resumes on
    /// <c>click</c> and <c>touchend</c> only, so a keyboard-only player heard nothing. This installs,
    /// once at boot, a <c>keydown</c> listener that calls FMOD's own resume while FMOD is waiting for a
    /// gesture (<c>Assets/Plugins/WebGL/FrogletAudioResume.jslib</c>, which documents the FMOD
    /// version it reads). Nothing happens on any other platform.</para>
    /// </summary>
    public static class WebAudioResume
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            FrogletResumeAudioOnKeydown();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // The jslib import is the one platform-only API here, so the one #if.
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void FrogletResumeAudioOnKeydown();
#endif
    }
}
