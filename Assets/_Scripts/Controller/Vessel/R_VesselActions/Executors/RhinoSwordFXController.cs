using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino energy sword's LOOK — a prefab-authored component on the blade root
    /// (the ForceFieldSkimmer), driven by <see cref="ShieldSkimmerScaleDriver"/> (state)
    /// and tuned entirely through <see cref="ShieldSkimmerScaleConfigSO"/> plus the
    /// authored sibling assets. Four layers:
    ///
    ///  1. HEAT RAMP — the blade blends from its authored teal toward the full-energy
    ///     cyan and brightens as stored energy fills. The blade uses the SHARED
    ///     FresnelMaterial, so this never touches <c>renderer.material</c> — it drives
    ///     <c>_Color</c> through a MaterialPropertyBlock (RGB above 1 feeds gameplay
    ///     bloom; the AstroLeagueBall impact-flash precedent).
    ///  2. ENERGIZE — the centerpiece. While CHARGING the blade leans toward white with
    ///     escalating anticipation arcs; the IGNITION instant blends it white-hot over
    ///     <c>ColorTransitionSeconds</c> and detonates a crackle burst along the whole
    ///     blade through the authored <see cref="ForcefieldCrackleController"/> (the
    ///     capsule-adapted forcefield-crackle overlay).
    ///  3. IMPACT FEEDBACK — a decaying white-out flash per prism destroyed plus a
    ///     crackle spark at the exact blade point that made contact
    ///     (<see cref="SkimmerSwingKinematics.ClosestBladePoint"/>); a dim DENIED spark
    ///     when a non-energized blade bounces off a super-shielded prism.
    ///  4. TIP TRACERS — two authored TrailRenderers seated at the blade tips each frame
    ///     (parented to the fuselage in the prefab so their width doesn't scale with the
    ///     growing blade), tinted with the live blade colour via MaterialPropertyBlock.
    ///
    /// Camera shake (super-shield pop, crystal burst) fires for the LOCAL human pilot
    /// only. See <c>RHINO_ENERGY_SWORD.md</c>.
    /// </summary>
    public sealed class RhinoSwordFXController : MonoBehaviour
    {
        static readonly int ColorId     = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int Color0Id    = Shader.PropertyToID("_Color0");
        static readonly int Color1Id    = Shader.PropertyToID("_Color1");

        [Header("Config (all tuning lives here)")]
        [SerializeField] private ShieldSkimmerScaleConfigSO config;

        [Header("Authored refs")]
        [Tooltip("The blade body's renderer (the FresnelMaterial capsule). Falls back to this GameObject's MeshRenderer.")]
        [SerializeField] private MeshRenderer bodyRenderer;
        [Tooltip("The crackle overlay driver on the blade (capsule surface mode). Falls back to this GameObject's controller.")]
        [SerializeField] private ForcefieldCrackleController crackle;
        [Tooltip("Authored tracer streak seated at the blade's TIP each frame (a fuselage child, so width ignores blade growth).")]
        [SerializeField] private TrailRenderer tipTracer;
        [Tooltip("Authored tracer streak seated at the blade's HILT-side tip each frame.")]
        [SerializeField] private TrailRenderer hiltTracer;

        Skimmer _skimmer;
        SkimmerSwingKinematics _swing;

        MaterialPropertyBlock _bodyMpb;
        MaterialPropertyBlock _tracerMpb;
        bool _hasColorProp;
        Color _authored;          // FresnelMaterial's authored teal, read once from sharedMaterial
        Color _appliedBodyColor;
        bool _bodyColorApplied;
        Color _appliedTracerColor;
        bool _tracerColorApplied;

        float _flash;             // 0 = none, 1 = full white-out; decays each Tick
        float _energizedBlend;    // 0 = heat ramp, 1 = white-hot; eased by ColorTransitionSeconds
        float _nextChargeCrackle;
        bool _warned;

        void OnEnable()
        {
            _bodyMpb ??= new MaterialPropertyBlock();
            _tracerMpb ??= new MaterialPropertyBlock();

            TryGetComponent(out _skimmer);
            TryGetComponent(out _swing);
            if (!bodyRenderer) TryGetComponent(out bodyRenderer);
            if (!crackle) TryGetComponent(out crackle);

            _authored = new Color(0.055f, 0.755f, 0.712f, 1f); // FresnelMaterial teal fallback
            _hasColorProp = false;
            if (bodyRenderer && bodyRenderer.sharedMaterial)
            {
                var mat = bodyRenderer.sharedMaterial;
                _hasColorProp = mat.HasProperty(ColorId);
                if (_hasColorProp) _authored = mat.GetColor(ColorId);
            }

            _flash = 0f;
            _energizedBlend = 0f;
            _bodyColorApplied = false;
            _tracerColorApplied = false;

            SeatTracers();
            if (tipTracer) tipTracer.Clear();
            if (hiltTracer) hiltTracer.Clear();

            WarnOnceIfUnwired();
        }

        void OnDisable()
        {
            // Drop the per-renderer overrides so the shared materials show through again.
            if (bodyRenderer) bodyRenderer.SetPropertyBlock(null);
            if (tipTracer) tipTracer.SetPropertyBlock(null);
            if (hiltTracer) hiltTracer.SetPropertyBlock(null);
            _bodyColorApplied = false;
            _tracerColorApplied = false;
            _flash = 0f;
            _energizedBlend = 0f;
        }

        // ── driver entry points ────────────────────────────────────────────────

        /// <summary>Per-frame look update, called by the driver after its state ticks.</summary>
        public void Tick(float energy01, RhinoSwordEnergizePhase phase, float charge01, float dt)
        {
            if (config == null) return;

            _flash = Mathf.MoveTowards(_flash, 0f, dt / config.FlashDecaySeconds);

            // Energized blend: white-hot while lit, a rising lean while charging
            // (anticipation — the blade visibly wants to ignite), nothing otherwise.
            float blendTarget = phase switch
            {
                RhinoSwordEnergizePhase.Energized => 1f,
                RhinoSwordEnergizePhase.Charging  => 0.35f * charge01,
                _                                 => 0f
            };
            _energizedBlend = Mathf.MoveTowards(_energizedBlend, blendTarget, dt / config.ColorTransitionSeconds);

            float vis = config.VisibilityMultiplier;
            Color baseVisible = ScaleRgb(_authored, vis);
            Color fullVisible = ScaleRgb(config.FullEnergyColor, vis);
            Color energizedVisible = ScaleRgb(config.EnergizedColor, vis);

            // Heat ramp: colour AND brightness climb with stored energy…
            Color ramp = Color.Lerp(baseVisible, fullVisible, energy01);
            float brightness = Mathf.Lerp(1f, config.FullEnergyBrightness, energy01);
            Color color = ScaleRgb(ramp, brightness);

            // …the energize blend rides above the ramp, and the impact flash above both.
            color = Color.Lerp(color, energizedVisible, _energizedBlend);
            if (_flash > 0f)
                color = Color.Lerp(color, config.FlashColor, _flash);

            ApplyBodyColor(color);
            SeatTracers();
            ApplyTracerColor(color);

            // Anticipation arcs while charging: small, quickening in weight with charge.
            if (phase == RhinoSwordEnergizePhase.Charging && crackle && Time.time >= _nextChargeCrackle)
            {
                _nextChargeCrackle = Time.time + config.ChargeCrackleInterval;
                float intensity = Mathf.Lerp(config.ChargeCrackleIntensity / 3f, config.ChargeCrackleIntensity, charge01);
                crackle.AddImpact(PointAlongBlade(Random.value), config.ChargeCrackleInterval * 2f,
                                  intensity, config.SparkWorldRadius * 0.6f);
            }
        }

        /// <summary>Phase transitions. The Energized edge is the ignition centerpiece.</summary>
        public void NotifyEnergizePhaseChanged(RhinoSwordEnergizePhase phase)
        {
            if (config == null) return;

            if (phase == RhinoSwordEnergizePhase.Energized)
            {
                Flash(config.PopFlashAmount);
                if (crackle)
                {
                    // Arc sites spread hilt→tip so the whole blade catches at once.
                    int sites = config.IgniteCrackleSites;
                    for (int i = 0; i < sites; i++)
                    {
                        float t = sites == 1 ? 0.5f : (float)i / (sites - 1);
                        crackle.AddImpact(PointAlongBlade(t), config.IgniteCrackleSeconds,
                                          config.IgniteCrackleIntensity, config.SparkWorldRadius * 1.5f);
                    }
                }
            }
        }

        /// <summary>A prism the sword destroyed: flash + a spark at the contact's blade point
        /// (+ a local camera shake when a super-shield popped).</summary>
        public void NotifyKill(bool superShielded, Vector3 prismWorldPosition)
        {
            if (config == null) return;
            Flash(superShielded ? config.PopFlashAmount : config.HitFlashAmount);

            if (crackle)
                crackle.AddImpact(BladePointNear(prismWorldPosition), config.SparkSeconds,
                                  superShielded ? config.SparkIntensity * 1.4f : config.SparkIntensity,
                                  config.SparkWorldRadius);

            if (superShielded)
                TryShakeCamera(config.PopShakeIntensity, config.PopShakeDuration);
        }

        /// <summary>A non-energized blade touched a super-shielded prism (the bounce): a dim
        /// spark that teaches the energize ritual without rewarding the contact.</summary>
        public void NotifyPopDenied(Vector3 prismWorldPosition)
        {
            if (config == null) return;
            Flash(config.HitFlashAmount * 0.5f);
            if (crackle)
                crackle.AddImpact(BladePointNear(prismWorldPosition), config.SparkSeconds * 0.7f,
                                  config.DeniedSparkIntensity, config.SparkWorldRadius * 0.7f);
        }

        /// <summary>The elemental-crystal burst: full flash, a whole-blade crackle scaled by
        /// the energy consumed, and the burst camera shake.</summary>
        public void NotifyCrystalBurst(float energyConsumed01)
        {
            if (config == null) return;
            Flash(config.PopFlashAmount);

            if (crackle)
            {
                int sites = config.IgniteCrackleSites;
                float intensity = Mathf.Lerp(config.SparkIntensity, config.IgniteCrackleIntensity, energyConsumed01);
                for (int i = 0; i < sites; i++)
                {
                    float t = sites == 1 ? 0.5f : (float)i / (sites - 1);
                    crackle.AddImpact(PointAlongBlade(t), config.IgniteCrackleSeconds,
                                      intensity, config.SparkWorldRadius * 1.5f);
                }
            }

            TryShakeCamera(config.BurstShakeMaxIntensity * Mathf.Clamp01(energyConsumed01),
                           config.BurstShakeDuration);
        }

        // ── internals ──────────────────────────────────────────────────────────

        /// <summary>Kick an impact flash; stronger pulses override weaker ones mid-decay.</summary>
        void Flash(float amount01) => _flash = Mathf.Max(_flash, Mathf.Clamp01(amount01));

        Vector3 PointAlongBlade(float t01)
        {
            if (_swing && _swing.IsReady) return _swing.PointAlongBlade(t01);
            // No swing model: interpolate along the capsule's own axis (unit capsule spans
            // local y ∈ [-1, 1], so the world tip offset is up * lossyScale.y).
            float half = transform.lossyScale.y;
            return transform.position + transform.up * Mathf.Lerp(-half, half, Mathf.Clamp01(t01));
        }

        Vector3 BladePointNear(Vector3 worldPoint)
        {
            if (_swing && _swing.IsReady) return _swing.ClosestBladePoint(worldPoint);
            return transform.position;
        }

        void ApplyBodyColor(Color color)
        {
            if (!bodyRenderer || !_hasColorProp) return;
            // Steady state (fixed energy, no flash) holds one colour — skip the MPB round-trip.
            if (_bodyColorApplied && ColorsClose(color, _appliedBodyColor)) return;
            _appliedBodyColor = color;
            _bodyColorApplied = true;

            bodyRenderer.GetPropertyBlock(_bodyMpb);
            _bodyMpb.SetColor(ColorId, color);
            bodyRenderer.SetPropertyBlock(_bodyMpb);
        }

        void SeatTracers()
        {
            // Unit capsule spans local y ∈ [-1, 1]; world tip offset = up * lossyScale.y.
            Vector3 center = transform.position;
            Vector3 up = transform.up;
            float half = transform.lossyScale.y * 0.95f;
            if (tipTracer) tipTracer.transform.position = center + up * half;
            if (hiltTracer) hiltTracer.transform.position = center - up * half;
        }

        void ApplyTracerColor(Color color)
        {
            if (!tipTracer && !hiltTracer) return;
            // Steady state holds one colour for long stretches — only rebuild the gradient
            // and property block when the colour actually moved (no per-frame allocation).
            if (_tracerColorApplied && ColorsClose(color, _appliedTracerColor)) return;
            _appliedTracerColor = color;
            _tracerColorApplied = true;

            // Drive every colour property the authored material might expose (TrailViewer's
            // _Color0/_Color1, generic _Color/_BaseColor) through a shared MPB — never
            // renderer.material — plus the vertex gradient for the along-streak alpha fade.
            _tracerMpb.Clear();
            _tracerMpb.SetColor(BaseColorId, color);
            _tracerMpb.SetColor(ColorId, color);
            _tracerMpb.SetColor(Color0Id, color);
            _tracerMpb.SetColor(Color1Id, color);

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

            if (tipTracer)
            {
                tipTracer.SetPropertyBlock(_tracerMpb);
                tipTracer.colorGradient = grad;
            }
            if (hiltTracer)
            {
                hiltTracer.SetPropertyBlock(_tracerMpb);
                hiltTracer.colorGradient = grad;
            }
        }

        // Local human pilot only (and not while autopiloting, e.g. the Menu_Main lava lamp) —
        // remote/AI Rhinos must not rattle this client's camera.
        void TryShakeCamera(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;

            var status = _skimmer ? _skimmer.VesselStatus : null;
            if (status == null || !status.IsLocalUser || status.AutoPilotEnabled) return;

            if (CameraManager.Instance != null &&
                CameraManager.Instance.GetActiveController() is CustomCameraController cam)
                cam.Shake(intensity, duration);
        }

        void WarnOnceIfUnwired()
        {
            if (_warned) return;
            if (config != null && bodyRenderer && crackle && tipTracer && hiltTracer) return;
            _warned = true;
            CSDebug.LogWarning($"[{nameof(RhinoSwordFXController)}] '{name}' is missing authored FX wiring — " +
                               $"config: {(config ? "ok" : "MISSING")}, bodyRenderer: {(bodyRenderer ? "ok" : "MISSING")}, " +
                               $"crackle: {(crackle ? "ok" : "MISSING")}, tracers: {(tipTracer ? "ok" : "MISSING")}/" +
                               $"{(hiltTracer ? "ok" : "MISSING")}. The sword runs, but that layer of its look is dark — " +
                               "author the reference on the Rhino prefab.");
        }

        static bool ColorsClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.01f;

        static Color ScaleRgb(Color c, float mul) => new Color(c.r * mul, c.g * mul, c.b * mul, c.a);
    }
}
