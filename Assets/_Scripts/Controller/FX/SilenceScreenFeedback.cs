using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
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

        /// <summary>The console command (DiagnosticsHUD, dev builds and the editor):
        /// <c>silence</c> reports what silence did this session, <c>silence test [seconds]</c>
        /// silences your own pilot so the shake and vignette can be judged without a fight.</summary>
        public const string CommandName = "silence";

        string _watchedPilot;
        float _weight;
        bool _pushed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            DiagnosticsHUD.RegisterCommand(CommandName, Handle);
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

            // Onset = this screen was not already showing a silence: the stronger shake. Every
            // further hit that extends the window (or lands while the last one is still draining)
            // gets the lighter per-hit shake, so each hit is felt and not only the first - in a
            // Dog Fight the AI's rounds keep one window open for the whole fight.
            var config = VesselSilence.Settings;
            bool onset = _weight <= 0.0001f;
            _watchedPilot = status.PlayerName;
            if (onset) ShakeCamera(config.OnsetShakeIntensity, config.OnsetShakeSeconds);
            else ShakeCamera(config.HitShakeIntensity, config.HitShakeSeconds);
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

        static void ShakeCamera(float intensity, float seconds)
        {
            if (intensity <= 0f || seconds <= 0f) return;
            if (CameraManager.Instance != null &&
                CameraManager.Instance.GetActiveController() is CustomCameraController cam)
                cam.Shake(intensity, seconds);
        }

        /// <summary>
        /// <c>silence</c>: every pilot this machine has seen hit - the scored hits it decided against
        /// them (each one silences), the silence windows that began, the takes it settled for them
        /// and how many of those found nothing - and who is silenced now. <c>silence test [seconds]</c>: silence your own pilot for that long
        /// (default 4), to look at the shake and vignette in isolation from the economy.
        /// </summary>
        static string Handle(string[] args)
        {
            if (args != null && args.Length > 0 && args[0] == "test")
            {
                float seconds = 4f;
                if (args.Length > 1) float.TryParse(args[1], System.Globalization.NumberStyles.Float,
                                                    System.Globalization.CultureInfo.InvariantCulture, out seconds);
                var local = FindLocalPilot();
                if (local == null) return "silence test: no local human pilot is flying.";
                VesselSilence.Record(local, Mathf.Max(0.1f, seconds));
                return $"silence test: {local.PlayerName} silenced for {Mathf.Max(0.1f, seconds):0.#} s (screen feedback only " +
                       "on this machine; peers are not told).";
            }

            var sb = new System.Text.StringBuilder();
            var post = PostProcessingManager.Instance;
            sb.Append("silence: post-processing ").Append(post ? "found" : "MISSING (no vignette can show)")
              .Append(" | vignette weight ").Append(s_instance ? s_instance._weight.ToString("0.00") : "-");
            if (VesselSilence.Tallies.Count == 0)
                sb.Append(" | no scored hit has landed on anybody on this machine yet");
            foreach (var kv in VesselSilence.Tallies)
            {
                var t = kv.Value;
                sb.Append("\n  ").Append(kv.Key).Append(": ").Append(t.Hits).Append(" scored hits decided here, ")
                  .Append("silenced ").Append(t.Silences).Append("x | takes settled here ").Append(t.Takes)
                  .Append(" (").Append(t.EmptyTakes).Append(" took nothing), ").Append(t.Petals).Append(" petals lost");
                float left = VesselSilence.RemainingSeconds(kv.Key);
                if (left > 0f) sb.Append(" - SILENCED, ").Append(left.ToString("0.0")).Append(" s left");
            }
            return sb.ToString();
        }

        static IVesselStatus FindLocalPilot()
        {
            foreach (var s in FindObjectsByType<VesselStatus>(FindObjectsSortMode.None))
            {
                IVesselStatus status = s;
                if (IsLocalHumanPilot(status)) return status;
            }
            return null;
        }

        static bool IsLocalHumanPilot(IVesselStatus status)
        {
            if (status?.Player == null) return false;
            return status.IsLocalUser && !status.AutoPilotEnabled;
        }
    }
}
