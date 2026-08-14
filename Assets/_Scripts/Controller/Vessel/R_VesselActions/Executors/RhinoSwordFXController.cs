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
    ///     streak's shape never inherits the blade's scale). Its whole SHAPE is authored on the
    ///     component — width, time, taper curve, gradient, material — and this controller never
    ///     writes any of it; all it does is PLACE the emitter half a head-width back down the
    ///     blade so the band's top edge lands on the sword's tip at whatever width is dialled
    ///     in. Tinted with the live blade colour via MaterialPropertyBlock, so the streak
    ///     changes with the sword through every state.
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
        [Tooltip("The sword's tip streak. A fuselage child (so the blade's scale never distorts it). " +
                 "TUNE IT ON THE COMPONENT — width, time, taper curve, gradient and material are all " +
                 "yours and nothing here overwrites them; this only re-seats the emitter each frame so " +
                 "the top edge of the band stays on the blade's tip at whatever width you set.")]
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
        float _appliedTracerWidth = float.NaN;   // last widthMultiplier seen, for the curve re-read
        float _tracerHeadWidthFactor = 1f;       // authored width curve evaluated at the emitting end

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
        /// Seat the streak so its top edge sits on the sword's TIP, whatever width is authored.
        ///
        /// The streak's SHAPE is yours: width, time, taper curve, gradient and material are all
        /// authored on the TrailRenderer and this never writes them. All it owns is placement —
        /// and because a TrailRenderer lays its width symmetrically about the emitter's path, an
        /// emitter parked on the tip would hang half the band out past the point of the sword.
        /// So it sits half a head-width back down the blade, which puts the band's top edge on
        /// the tip and the rest of it running down the blade, at any width you dial in.
        ///
        /// (Exact while the sword is swinging across its own axis — a swipe or a chop — which is
        /// when the ribbon is visible at all. The width direction is perpendicular to the path
        /// travelled, so on a thrust straight along the blade there is no "top edge" to align.)
        /// </summary>
        void SeatTracer()
        {
            if (!bladeTracer) return;

            Vector3 tip = PointAlongBlade(1f);
            Vector3 hilt = PointAlongBlade(0f);
            Vector3 towardTip = tip - hilt;
            towardTip = towardTip.sqrMagnitude > 1e-6f ? towardTip.normalized : transform.up;

            // Width at the EMITTING end of the streak: the multiplier scales the authored curve,
            // and the curve's value at t=0 is the end being laid down right now. Re-read the
            // curve only when the multiplier moves (its getter allocates, and a designer tuning
            // one is almost always tuning the other) — otherwise this is a float read per frame.
            float multiplier = bladeTracer.widthMultiplier;
            if (!Mathf.Approximately(multiplier, _appliedTracerWidth))
            {
                _appliedTracerWidth = multiplier;
                var curve = bladeTracer.widthCurve;
                _tracerHeadWidthFactor = curve != null && curve.length > 0 ? curve.Evaluate(0f) : 1f;
            }

            float headWidth = multiplier * _tracerHeadWidthFactor;
            bladeTracer.transform.position = tip - towardTip * (headWidth * 0.5f);
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
