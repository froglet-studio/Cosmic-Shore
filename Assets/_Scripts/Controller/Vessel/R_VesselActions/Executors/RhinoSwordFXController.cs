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
    ///  4. BLADE TRACERS — a comb of hairline authored TrailRenderers spread evenly down the
    ///     blade, tip (element 0) to hilt (fuselage-parented in the prefab so their shape never
    ///     inherits the blade's scale). Their whole SHAPE is authored on the components — width,
    ///     time, taper curve, gradient, material — and this controller never writes any of it;
    ///     all it does is PLACE each emitter, insetting the two end streaks by half their own
    ///     head width so widening one grows it into the blade rather than past the point. All
    ///     tinted with the live blade colour via MaterialPropertyBlock, so the streaks change
    ///     with the sword through every state.
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
        [Tooltip("The sword's streaks — hairline TrailRenderers spread evenly down the blade, tip " +
                 "(element 0) to hilt (last). Fuselage children, so the blade's scale never distorts " +
                 "them. TUNE THEM ON THE COMPONENTS — width, time, taper curve, gradient and material " +
                 "are all yours and nothing here overwrites them; this only re-seats each emitter " +
                 "every frame. Add or remove entries freely: the spread is derived from the count.")]
        [SerializeField] private TrailRenderer[] bladeTracers;

        Skimmer _skimmer;
        SkimmerSwingKinematics _swing;

        MaterialPropertyBlock _bodyMpb;
        MaterialPropertyBlock _tracerMpb;
        bool _hasColorProp;
        Color _appliedBodyColor;
        bool _bodyColorApplied;
        Color _appliedTracerColor;
        bool _tracerColorApplied;
        float[] _appliedTracerWidths;      // last widthMultiplier seen per tracer, for the curve re-read
        float[] _tracerHeadWidthFactors;   // authored width curve evaluated at each emitting end

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

            SeatTracers();
            ForEachTracer(tr => tr.Clear());

            WarnOnceIfUnwired();
        }

        void OnDisable()
        {
            // Drop the per-renderer overrides so the shared materials show through again.
            if (bodyRenderer) bodyRenderer.SetPropertyBlock(null);
            ForEachTracer(tr => tr.SetPropertyBlock(null));
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

        /// <summary>
        /// Spread the streaks evenly down the blade — element 0 on the TIP, the last on the HILT —
        /// so the sword draws a comb of hairlines through a swing rather than one slab.
        ///
        /// Their SHAPE is yours: width, time, taper curve, gradient and material are authored on
        /// each TrailRenderer and this never writes them. All it owns is placement, and the
        /// spacing is EVEN by construction: ONE span is computed for the whole set, inset at each
        /// end by half the head width of the streak that sits there (a TrailRenderer lays its
        /// width symmetrically about the emitter's path, so an end streak parked exactly on the
        /// point would hang half its band past it). Insetting each streak by its OWN width
        /// instead would hand every streak a different span, and the spacing would drift apart
        /// the moment two of them were tuned to different widths.
        ///
        /// (The inset is exact while the sword swings across its own axis — a swipe or chop,
        /// which is when the streaks are visible at all. The width direction is perpendicular to
        /// the path travelled, so on a thrust straight along the blade there is no end to align.)
        /// </summary>
        void SeatTracers()
        {
            if (bladeTracers == null || bladeTracers.Length == 0) return;

            Vector3 tip = PointAlongBlade(1f);
            Vector3 hilt = PointAlongBlade(0f);
            Vector3 towardTip = tip - hilt;
            towardTip = towardTip.sqrMagnitude > 1e-6f ? towardTip.normalized : transform.up;

            int count = bladeTracers.Length;
            EnsureTracerCaches(count);

            // One span for every streak — see above.
            Vector3 spanTip = tip - towardTip * (0.5f * HeadWidthAt(0));
            Vector3 spanHilt = hilt + towardTip * (0.5f * HeadWidthAt(count - 1));

            for (int i = 0; i < count; i++)
            {
                var tracer = bladeTracers[i];
                if (!tracer) continue;

                // 1 at element 0 (tip) running to 0 at the last (hilt).
                float t = count == 1 ? 1f : 1f - (float)i / (count - 1);
                tracer.transform.position = Vector3.Lerp(spanHilt, spanTip, t);
            }
        }

        float HeadWidthAt(int index)
        {
            var tracer = bladeTracers[index];
            return tracer ? HeadWidth(tracer, index) : 0f;
        }

        /// <summary>
        /// Width at the EMITTING end of a streak: the multiplier scales the authored curve, and
        /// the curve's value at t=0 is the end being laid down right now. The curve is re-read
        /// only when that tracer's multiplier moves — its getter allocates a fresh AnimationCurve,
        /// and a designer tuning one is almost always tuning the other.
        /// </summary>
        float HeadWidth(TrailRenderer tracer, int index)
        {
            float multiplier = tracer.widthMultiplier;
            if (!Mathf.Approximately(multiplier, _appliedTracerWidths[index]))
            {
                _appliedTracerWidths[index] = multiplier;
                var curve = tracer.widthCurve;
                _tracerHeadWidthFactors[index] = curve != null && curve.length > 0 ? curve.Evaluate(0f) : 1f;
            }
            return multiplier * _tracerHeadWidthFactors[index];
        }

        void ForEachTracer(System.Action<TrailRenderer> action)
        {
            if (bladeTracers == null) return;
            for (int i = 0; i < bladeTracers.Length; i++)
                if (bladeTracers[i]) action(bladeTracers[i]);
        }

        void EnsureTracerCaches(int count)
        {
            if (_appliedTracerWidths != null && _appliedTracerWidths.Length == count) return;
            _appliedTracerWidths = new float[count];
            _tracerHeadWidthFactors = new float[count];
            for (int i = 0; i < count; i++)
            {
                _appliedTracerWidths[i] = float.NaN;   // force the first curve read
                _tracerHeadWidthFactors[i] = 1f;
            }
        }

        void ApplyTracerColor(Color color)
        {
            if (bladeTracers == null || bladeTracers.Length == 0) return;
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

            for (int i = 0; i < bladeTracers.Length; i++)
            {
                var tracer = bladeTracers[i];
                if (!tracer) continue;
                tracer.SetPropertyBlock(_tracerMpb);
                tracer.colorGradient = grad;
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
            bool tracersWired = bladeTracers != null && bladeTracers.Length > 0;
            if (config != null && bodyRenderer && crackle && tracersWired) return;
            _warned = true;
            CSDebug.LogWarning($"[{nameof(RhinoSwordFXController)}] '{name}' is missing authored FX wiring — " +
                               $"config: {(config ? "ok" : "MISSING")}, bodyRenderer: {(bodyRenderer ? "ok" : "MISSING")}, " +
                               $"crackle: {(crackle ? "ok" : "MISSING")}, bladeTracers: {(tracersWired ? bladeTracers.Length + " wired" : "MISSING")}. " +
                               "The sword runs, but that layer of its look is dark — " +
                               "author the reference on the Rhino prefab.");
        }

        static bool ColorsClose(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.01f;

        static Color ScaleRgb(Color c, float mul) => new Color(c.r * mul, c.g * mul, c.b * mul, c.a);
    }
}
