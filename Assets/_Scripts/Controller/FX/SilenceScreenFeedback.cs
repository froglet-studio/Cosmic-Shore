using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the SILENCED pilot sees on their own screen (Docs/ElementalAbilitySystem/SILENCE.md §5):
    /// a very subtle camera shake at the onset, and a subtle danger-coloured vignette that holds for
    /// the whole window and drains when it runs out. Fleet-wide by construction - it listens to
    /// <see cref="VesselSilence.VesselSilenced"/>, not to any vessel - so every hull, present and
    /// future, signals silence the same way.
    ///
    /// <para><b>Only the local human pilot.</b> A silence recorded for anybody else (a rival, an AI,
    /// a replica of another peer's hull) changes nothing on this screen. Which pilot is watched is
    /// re-decided at every onset, so a vessel swap or a Hijack pilot swap follows the pilot.</para>
    ///
    /// <para>The vignette is the gameplay post-processing profile's own Vignette override, driven
    /// through <see cref="PostProcessingManager.SetSilenceVignette"/> (the speed tunnel's home/offset
    /// shape). Its colour is the active palette's DANGER signal colour - the rim every danger prism
    /// wears - so the edge of the screen says "danger" in the game's own red. The shake is
    /// <see cref="CustomCameraController.Shake"/>, which keeps a stronger shake already playing.
    /// Every number is on <see cref="VesselSilenceConfigSO"/>.</para>
    ///
    /// <para>Self-installs once per play session, like <c>HumanFlightRecorder</c>: a hidden,
    /// scene-independent object with nothing to wire.</para>
    /// </summary>
    public sealed class SilenceScreenFeedback : MonoBehaviour
    {
        static SilenceScreenFeedback s_instance;

        string _watchedPilot;
        float _weight;
        bool _pushed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance) return;
            var go = new GameObject("[Silence Screen Feedback]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<SilenceScreenFeedback>();
        }

        void OnEnable() => VesselSilence.VesselSilenced += OnVesselSilenced;

        void OnDisable()
        {
            VesselSilence.VesselSilenced -= OnVesselSilenced;
            Release();
        }

        void OnVesselSilenced(IVesselStatus status)
        {
            if (!IsLocalHumanPilot(status)) return;

            // Onset = this screen was not already showing a silence. An extension of a live window
            // (or a new silence landing while the last one is still draining) only keeps the
            // vignette up; it does not shake again.
            bool onset = _weight <= 0.0001f;
            _watchedPilot = status.PlayerName;
            if (onset) ShakeCamera(VesselSilence.Settings);
        }

        void Update()
        {
            if (string.IsNullOrEmpty(_watchedPilot)) return;

            var config = VesselSilence.Settings;
            bool silenced = VesselSilence.IsSilenced(_watchedPilot);
            float seconds = silenced ? config.VignetteFadeInSeconds : config.VignetteFadeOutSeconds;
            float target = silenced ? 1f : 0f;
            _weight = seconds > 0f
                ? Mathf.MoveTowards(_weight, target, Time.unscaledDeltaTime / seconds)
                : target;

            if (_weight > 0.0001f)
            {
                // SmoothStep on the drive so the edge eases in and out rather than ramping linearly.
                Push(Mathf.SmoothStep(0f, 1f, _weight), config);
                return;
            }

            Release();
            if (!silenced) _watchedPilot = null;
        }

        void Push(float weight, VesselSilenceConfigSO config)
        {
            var post = PostProcessingManager.Instance;
            if (!post) return;
            post.SetSilenceVignette(weight, VignetteColor(config), config.VignetteIntensity,
                                    config.VignetteSmoothness);
            _pushed = true;
        }

        void Release()
        {
            if (!_pushed) return;
            _pushed = false;
            _weight = 0f;
            var post = PostProcessingManager.Instance;
            if (post) post.SetSilenceVignette(0f, Color.black, 0f, 0f);
        }

        static Color VignetteColor(VesselSilenceConfigSO config)
        {
            var palette = PrismLit.ColorSet;
            var danger = palette ? palette.GetDangerSignalColor() : default;
            if (danger.a <= 0f) danger = config.FallbackDangerColor;
            var c = Color.Lerp(Color.black, danger, config.VignetteColorStrength);
            c.a = 1f;
            return c;
        }

        static void ShakeCamera(VesselSilenceConfigSO config)
        {
            if (config.OnsetShakeIntensity <= 0f || config.OnsetShakeSeconds <= 0f) return;
            if (CameraManager.Instance != null &&
                CameraManager.Instance.GetActiveController() is CustomCameraController cam)
                cam.Shake(config.OnsetShakeIntensity, config.OnsetShakeSeconds);
        }

        static bool IsLocalHumanPilot(IVesselStatus status)
        {
            if (status?.Player == null) return false;
            return status.IsLocalUser && !status.AutoPilotEnabled;
        }
    }
}
