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
    ///  1. HEAT RAMP — the blade sits at the config's RESTING colour (white-hot, never the
    ///     pilot's domain colour: the sword friendly-fires, so a team-tinted blade would
    ///     read as safe to allies) and brightens as stored energy fills. The blade uses the
    ///     SHARED FresnelMaterial, so this never touches <c>renderer.material</c> — it
    ///     drives <c>_Color</c> through a MaterialPropertyBlock (RGB above 1 feeds gameplay
    ///     bloom; the AstroLeagueBall impact-flash precedent).
    ///  2. ENERGIZE — the centerpiece, and the only thing that moves the blade's HUE:
    ///     while CHARGING it leans toward the danger colour with escalating anticipation
    ///     arcs; the IGNITION instant blends it fully to <c>EnergizedColor</c> — the shared
    ///     <c>SO_ColorSet.Danger</c> red — over <c>ColorTransitionSeconds</c> and detonates
    ///     a crackle burst along the whole blade through the authored
    ///     <see cref="ForcefieldCrackleController"/> (the capsule-adapted overlay). Energy
    ///     is brightness, state is hue; the two signals can never be confused.
    ///  3. IMPACT FEEDBACK — a decaying white-out flash per prism destroyed plus a
    ///     crackle spark at the exact blade point that made contact
    ///     (<see cref="SkimmerSwingKinematics.ClosestBladePoint"/>); a dim DENIED spark
    ///     when a non-energized blade bounces off a super-shielded prism.
    ///  4. TIP TRACER — ONE authored TrailRenderer (fuselage-parented in the prefab so the
    ///     streak's shape never inherits the blade's scale) riding the sword's tip, slim, and
    ///     reaching about a quarter of the way back down the blade before the authored width
    ///     curve and alpha gradient grade it to nothing. Width and lifetime are both driven
    ///     from the blade's live length so that proportion survives the energy meter. Tinted
    ///     with the live blade colour via MaterialPropertyBlock, so the streak changes with
    ///     the sword through every state.
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
        [Tooltip("Authored swing ribbon. A fuselage child (so the blade's scale never distorts it), " +
                 "re-seated on the blade each frame at ShieldSkimmerScaleConfig.TracerBladeAnchor01 " +
                 "with its width driven to the blade's live length.")]
        [SerializeField] private TrailRenderer bladeTracer;

        Skimmer _skimmer;
        SkimmerSwingKinematics _swing;

        MaterialPropertyBlock _bodyMpb;
        MaterialPropertyBlock _tracerMpb;
        bool _hasColorProp;
        Color _appliedBodyColor;
        bool _bodyColorApplied;
        Color _appliedTracerColor;
        bool _tracerColorApplied;
        float _appliedTracerWidth = float.NaN;
        float _appliedTracerSeconds = float.NaN;

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

            // The blade's colour is AUTHORED IN THE CONFIG, never read off the shared
            // FresnelMaterial: the sword friendly-fires, so its colour must never drift toward
            // a domain/team read. All we ask the material is whether it can be tinted at all.
            _hasColorProp = bodyRenderer && bodyRenderer.sharedMaterial &&
                            bodyRenderer.sharedMaterial.HasProperty(ColorId);

            _flash = 0f;
            _energizedBlend = 0f;
            _bodyColorApplied = false;
            _tracerColorApplied = false;

            SeatTracer();
            if (bladeTracer) bladeTracer.Clear();

            WarnOnceIfUnwired();
        }

        void OnDisable()
        {
            // Drop the per-renderer overrides so the shared materials show through again.
            if (bodyRenderer) bodyRenderer.SetPropertyBlock(null);
            if (bladeTracer) bladeTracer.SetPropertyBlock(null);
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
            Color restVisible = ScaleRgb(config.RestingBladeColor, vis);
            Color fullVisible = ScaleRgb(config.FullEnergyColor, vis);
            Color energizedVisible = ScaleRgb(config.EnergizedColor, vis);

            // Heat ramp: stored energy reads as BRIGHTNESS (the resting and full-energy colours
            // share a hue on purpose — see the config).
            Color ramp = Color.Lerp(restVisible, fullVisible, energy01);
            float brightness = Mathf.Lerp(1f, config.FullEnergyBrightness, energy01);
            Color color = ScaleRgb(ramp, brightness);

            // …the energize blend rides above it and is the only thing that shifts HUE (to the
            // danger colour), and the impact flash rides above both.
            color = Color.Lerp(color, energizedVisible, _energizedBlend);
            if (_flash > 0f)
                color = Color.Lerp(color, config.FlashColor, _flash);

            ApplyBodyColor(color);
            SeatTracer();
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

        /// <summary>The blade's live world length, hilt to tip.</summary>
        float BladeWorldLength => 2f * (_swing && _swing.IsReady
            ? _swing.HalfLength
            : Mathf.Abs(transform.lossyScale.y));

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

        /// <summary>
        /// Ride the sword's TIP and keep the streak proportioned to the blade: a slim trace
        /// reaching about <see cref="ShieldSkimmerScaleConfigSO.TracerLengthBladeFraction"/> of
        /// the way back down the blade, graded to nothing by the TrailRenderer's authored width
        /// curve and alpha gradient. Both dimensions are driven from the blade's LIVE length so
        /// the proportion holds as the energy meter grows the sword — width as a small fraction
        /// of it, length by solving the trail's lifetime against a calibration speed (driving
        /// lifetime from live speed instead would retroactively expire points and pop the streak
        /// at the start of every swing).
        /// </summary>
        void SeatTracer()
        {
            if (!bladeTracer || config == null) return;
            bladeTracer.transform.position = PointAlongBlade(config.TracerBladeAnchor01);

            float length = BladeWorldLength;

            float width = length * config.TracerWidthBladeFraction;
            if (!Mathf.Approximately(width, _appliedTracerWidth))
            {
                _appliedTracerWidth = width;
                // widthMultiplier scales the authored width CURVE, so the taper stays designed.
                bladeTracer.widthMultiplier = width;
            }

            float seconds = config.TracerSecondsFor(length);
            if (!Mathf.Approximately(seconds, _appliedTracerSeconds))
            {
                _appliedTracerSeconds = seconds;
                bladeTracer.time = seconds;
            }
        }

        void ApplyTracerColor(Color color)
        {
            if (!bladeTracer) return;
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

            bladeTracer.SetPropertyBlock(_tracerMpb);
            bladeTracer.colorGradient = grad;
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
            if (config != null && bodyRenderer && crackle && bladeTracer) return;
            _warned = true;
            CSDebug.LogWarning($"[{nameof(RhinoSwordFXController)}] '{name}' is missing authored FX wiring — " +
                               $"config: {(config ? "ok" : "MISSING")}, bodyRenderer: {(bodyRenderer ? "ok" : "MISSING")}, " +
                               $"crackle: {(crackle ? "ok" : "MISSING")}, bladeTracer: {(bladeTracer ? "ok" : "MISSING")}. " +
                               "The sword runs, but that layer of its look is dark — " +
                               "author the reference on the Rhino prefab.");
        }

        static bool ColorsClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.01f;

        static Color ScaleRgb(Color c, float mul) => new Color(c.r * mul, c.g * mul, c.b * mul, c.a);
    }
}
