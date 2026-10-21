using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the SILENCED pilot sees on their own screen (Docs/ElementalAbilitySystem/SILENCE.md §5):
    /// a camera shake at the onset and a lighter one on every further hit, and a 3D PERIMETER that holds
    /// for the whole window: faceted shards on the screen's edge, where a vignette would sit, each facet
    /// showing the frame behind it bent, jolting and tearing with each hit and drawing back out when it
    /// runs out. Fleet-wide by construction - it listens to
    /// <see cref="VesselSilence.VesselSilenced"/>, not to any vessel - so every hull, present and
    /// future, signals silence the same way.
    ///
    /// <para><b>Only the local human pilot.</b> A silence recorded for anybody else (a rival, an AI,
    /// a replica of another peer's hull) changes nothing on this screen. Which pilot is watched is
    /// re-decided at every onset, so a vessel swap or a Hijack pilot swap follows the pilot.</para>
    ///
    /// <para><b>The perimeter</b> (Garrett, 2026-10-21: "instead of a 2d effect we just have a client side
    /// perimeter of 3d craziness on the perimeter where vignettes typically use 2d effects"). Where the
    /// shards sit and how they are cut is <see cref="SilencePerimeter"/>; this component builds their meshes
    /// once per arrangement and hands the frame to <see cref="SilencePerimeterPass"/>, which draws them on the
    /// local pilot's camera only. The 2D edge below is still here, off by default (strength 0).</para>
    ///
    /// <para><b>The 2D edge is an OVERLAY, not URP's Vignette.</b> URP's Vignette multiplies the frame by
    /// its colour, so a red one can only darken, and over the arena's near-black space it showed
    /// nothing (Garrett's Dog Fight run, 2026-10-21). The edge is a full-screen <c>RawImage</c> on
    /// its own Screen Space Overlay canvas, under the HUD, whose alpha is
    /// <see cref="SilenceVignetteMask"/> baked into a small texture and whose colour is the active
    /// palette's DANGER signal colour - the rim every danger prism wears - so it ADDS the game's own
    /// red over whatever is behind it. URP's Vignette is kept for one job only: an optional black
    /// darkening under the colour (<see cref="PostProcessingManager.SetSilenceVignette"/>), so the
    /// edge still reads over a bright scene. The shake is <see cref="CustomCameraController.Shake"/>,
    /// which keeps a stronger shake already playing. Every number is on
    /// <see cref="VesselSilenceConfigSO"/>, and the Silence Vignette Lab
    /// (<c>Docs/Studios/SilenceVignetteLab.html</c>) explores them over a stand-in Dog Fight.</para>
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
        float _pulse;
        bool _pushed;

        // The overlay: built on first use, kept for the session, hidden while nobody is silenced.
        Canvas _canvas;
        RawImage _edge;
        Texture2D _mask;
        int _maskWidth, _maskHeight;
        SilenceVignetteMask.Shape _maskShape;
        float _maskAspect;

        // The perimeter: one mesh per shard, rebuilt when the arrangement changes; one material for the session.
        Mesh[] _shards;
        SilencePerimeter.Settings _shardSettings;
        Material _perimeterMaterial;
        bool _warnedNoShader;

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

        void OnDestroy()
        {
            if (_mask) Destroy(_mask);
            DestroyShards();
            if (_perimeterMaterial) Destroy(_perimeterMaterial);
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
            else
            {
                ShakeCamera(config.HitShakeIntensity, config.HitShakeSeconds);
                _pulse = Mathf.Min(1f, _pulse + config.VignetteHitPulse);   // the edge flares, the shards jolt
            }
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

            float decay = config.VignetteHitPulseSeconds;
            _pulse = decay > 0f ? _pulse * Mathf.Exp(-Time.unscaledDeltaTime / decay) : 0f;

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
            PushPerimeter(weight, config);

            // The 2D edge: none at all at strength 0 (the default since the perimeter), not even the hit flare.
            if (config.VignetteIntensity > 0f)
            {
                EnsureOverlay(config);
                float breathe = SilenceVignetteMask.Breathe(Time.unscaledTime, config.VignetteBreatheDepth,
                                                            config.VignetteBreatheHz);
                // The mask texture carries the shape; the image's alpha is everything else (Opacity with
                // a mask of 1 - the texture multiplies the shape back in).
                var c = VignetteColor(config);
                c.a = SilenceVignetteMask.Opacity(1f, weight, config.VignetteIntensity, breathe, _pulse * weight);
                if (_edge)
                {
                    _edge.color = c;
                    if (!_edge.gameObject.activeSelf) _edge.gameObject.SetActive(true);
                }
            }
            else if (_edge && _edge.gameObject.activeSelf) _edge.gameObject.SetActive(false);

            var post = PostProcessingManager.Instance;
            if (post && config.VignetteDarken > 0f)
                post.SetSilenceVignette(weight, Color.black, config.VignetteDarken, config.VignetteSmoothness);
            _pushed = true;
        }

        void Release()
        {
            if (!_pushed) return;
            _pushed = false;
            _weight = 0f;
            _pulse = 0f;
            SilencePerimeterPass.Current = default;
            if (_edge) _edge.gameObject.SetActive(false);
            var post = PostProcessingManager.Instance;
            if (post) post.SetSilenceVignette(0f, Color.black, 0f, 0f);
        }

        /// <summary>
        /// Hands this frame's perimeter to <see cref="SilencePerimeterPass"/>: the local pilot's camera, the
        /// shard meshes (rebuilt only when the arrangement changes), the silence's eased weight, the hit
        /// flare and the look. With amount 0 or no shader, nothing is drawn.
        /// </summary>
        void PushPerimeter(float weight, VesselSilenceConfigSO config)
        {
            var settings = config.PerimeterSettings;
            var camera = PilotCamera();
            if (config.PerimeterAmount <= 0f || settings.Count <= 0 || !camera || !EnsurePerimeterMaterial(config))
            {
                SilencePerimeterPass.Current = default;
                return;
            }
            EnsureShards(settings);
            SilencePerimeterPass.Current = new SilencePerimeterPass.Frame
            {
                Active = true,
                Camera = camera,
                Material = _perimeterMaterial,
                Meshes = _shards,
                Settings = settings,
                Drive = weight,
                Pulse = _pulse * weight,
                Time = Time.unscaledTime,
                Amount = config.PerimeterAmount,
                Opacity = config.PerimeterOpacity,
                Glitch = config.PerimeterGlitch,
                Warp = config.PerimeterWarp,
                Look = new Vector4(config.PerimeterRefraction, config.PerimeterTwist, config.PerimeterAberration, config.PerimeterTint),
                Look2 = new Vector4(config.PerimeterRimGlow, config.PerimeterEdgeGlow, config.PerimeterFill, config.PerimeterInvert),
                Danger = DangerColor(config),
            };
        }

        bool EnsurePerimeterMaterial(VesselSilenceConfigSO config)
        {
            if (_perimeterMaterial) return true;
            var shader = config.PerimeterShader;
            if (!shader || !shader.isSupported)
            {
                if (!_warnedNoShader)
                    CSDebug.LogWarning("[Silence] The perimeter cannot draw: VesselSilenceConfig.perimeterShader is " +
                                       (shader ? "not supported on this device" : "empty") + ". Silence shows no perimeter.");
                _warnedNoShader = true;
                return false;
            }
            _perimeterMaterial = new Material(shader) { name = "SilencePerimeter", hideFlags = HideFlags.DontSave };
            return true;
        }

        void EnsureShards(in SilencePerimeter.Settings settings)
        {
            if (_shards != null && _shards.Length == settings.Count && _shardSettings.Shape == settings.Shape &&
                _shardSettings.Seed == settings.Seed) return;
            DestroyShards();
            _shards = new Mesh[settings.Count];
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var barycentrics = new List<Vector3>();
            for (int i = 0; i < settings.Count; i++)
            {
                positions.Clear(); normals.Clear(); barycentrics.Clear();
                SilencePerimeter.Geometry(settings, i, positions, normals, barycentrics);
                var triangles = new int[positions.Count];
                for (int t = 0; t < triangles.Length; t++) triangles[t] = t;
                var mesh = new Mesh { name = "SilenceShard" + i, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, barycentrics);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                _shards[i] = mesh;
            }
            _shardSettings = settings;
        }

        void DestroyShards()
        {
            if (_shards == null) return;
            foreach (var mesh in _shards) if (mesh) Destroy(mesh);
            _shards = null;
        }

        static Camera PilotCamera()
        {
            if (CameraManager.Instance != null &&
                CameraManager.Instance.GetActiveController() is CustomCameraController controller && controller.Camera)
                return controller.Camera;
            return Camera.main;
        }

        /// <summary>
        /// Builds the overlay canvas and image on first use, and re-bakes the mask texture whenever
        /// the screen's shape or the config's shape fields change (a few float compares a frame;
        /// a bake is a 256-wide texture, once).
        /// </summary>
        void EnsureOverlay(VesselSilenceConfigSO config)
        {
            if (!_canvas)
            {
                var root = new GameObject("SilenceVignette");
                root.transform.SetParent(transform, false);
                _canvas = root.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var image = new GameObject("Edge");
                image.transform.SetParent(root.transform, false);
                _edge = image.AddComponent<RawImage>();
                _edge.raycastTarget = false;
                var rect = _edge.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                image.SetActive(false);
            }
            _canvas.sortingOrder = config.VignetteSortingOrder;

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            var shape = config.VignetteShape;
            if (_mask && Mathf.Abs(aspect - _maskAspect) < 0.01f && SameShape(shape, _maskShape)) return;

            const int width = 256;
            int height = Mathf.Clamp(Mathf.RoundToInt(width / Mathf.Max(0.1f, aspect)), 16, 512);
            if (!_mask || _maskWidth != width || _maskHeight != height)
            {
                if (_mask) Destroy(_mask);
                _mask = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "SilenceVignetteMask",
                };
                _maskWidth = width;
                _maskHeight = height;
            }

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    byte a = (byte)Mathf.RoundToInt(255f * SilenceVignetteMask.Alpha(u, v, aspect, shape));
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
                }
            }
            _mask.SetPixels32(pixels);
            _mask.Apply(false);
            _edge.texture = _mask;
            _maskAspect = aspect;
            _maskShape = shape;
        }

        static bool SameShape(in SilenceVignetteMask.Shape a, in SilenceVignetteMask.Shape b) =>
            a.Style == b.Style && a.Facets == b.Facets && a.Inner == b.Inner && a.Feather == b.Feather &&
            a.Roundness == b.Roundness && a.Fill == b.Fill && a.RimStrength == b.RimStrength &&
            a.RimWidth == b.RimWidth;

        static Color DangerColor(VesselSilenceConfigSO config)
        {
            var palette = PrismLit.ColorSet;
            var danger = palette ? palette.GetDangerSignalColor() : default;
            if (danger.a <= 0f) danger = config.FallbackDangerColor;
            danger.a = 1f;
            return danger;
        }

        static Color VignetteColor(VesselSilenceConfigSO config)
        {
            var c = Color.Lerp(Color.black, DangerColor(config), config.VignetteColorStrength);
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
            sb.Append("silence: post-processing ").Append(post ? "found" : "missing (no darkening)")
              .Append(" | edge weight ").Append(s_instance ? s_instance._weight.ToString("0.00") : "-")
              .Append(", perimeter ").Append(SilencePerimeterPass.Current.Active
                  ? $"drawing {SilencePerimeterPass.Current.Settings.Count} shards on {SilencePerimeterPass.Current.Camera.name}"
                  : s_instance && s_instance._warnedNoShader ? "cannot draw (no shader)" : "idle")
              .Append(", 2D edge ").Append(s_instance && s_instance._edge && s_instance._edge.gameObject.activeSelf
                  ? "showing, alpha " + s_instance._edge.color.a.ToString("0.00") : "hidden");
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
