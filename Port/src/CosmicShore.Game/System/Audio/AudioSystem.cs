using CosmicShore.Engine;

namespace CosmicShore.Core
{
    /// <summary>
    /// PORT Deviation #11 — type-preserving SHELL of the Wwise/FMOD-backed AudioSystem
    /// (original: Assets/_Scripts/System/Audio/AudioSystem.cs, 530+ lines). Landed early
    /// (V6) because ActionExecutorRegistry's [Inject] field and public AudioSystem
    /// property need the type. Only the public surface used by the vessel-layer closure
    /// is present; bodies no-op. The real port arrives with the phase-5 audio backend
    /// (the standalone client already has its own procedural AudioEngine).
    /// </summary>
    public class AudioSystem : MonoBehaviour
    {
        public static AudioSystem Instance { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        public void PlayGameplaySFX(GameplaySFXCategory category) { }

        public void PlayGameplaySFX(GameplaySFXCategory category, Vector3 worldPosition) { }

        // UI-shell surface (Arc F): menu click/open/close cues — no-op until the
        // phase-5 audio backend, same convention as the gameplay SFX above.
        public void PlayMenuAudio(MenuAudioCategory category) { }
    }
}
