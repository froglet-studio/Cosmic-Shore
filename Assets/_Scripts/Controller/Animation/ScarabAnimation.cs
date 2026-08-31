using System.Collections.Generic;
using CosmicShore.Core;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puppeteers the Scarab's hull (parts built by <see cref="ScarabHullBuilder"/> /
    /// <see cref="ScarabHullForm"/>). Design record: R_VesselActions/SCARAB.md §3.0.
    ///
    /// A vessel that does not move its own parts does not read as flying, however good the
    /// flight model underneath it is. AMPLITUDES ARE FLEET-SCALE ON PURPOSE — `RhinoAnimation`
    /// (80° wings, 25° fuselage) is the calibration, and "if you can't see it from the chase
    /// camera it isn't doing its job" (the 50 u fixed chase of ScarabCameraSettingsSO).
    ///
    /// THE PIPELINE: every driven part has ONE writer — a per-channel <see cref="AngularSpring"/>
    /// this component owns. All contributors (flight pose, drift language, event flourishes,
    /// idle life) SUM into one target per channel per frame, upstream of the spring; event
    /// flourishes enter as velocity IMPULSES, so a dash flourish peaks immediately and decays
    /// through the same spring instead of fighting an easing. The base class's Idle() branch is
    /// overridden into the same path (base Update routes to Idle() INSTEAD of puppetry when the
    /// input goes idle — a second writer lerping parts to rest would fight every held pose).
    ///
    /// INPUTS ARE SOURCED HERE, NOT FROM THE DISPATCH ARGS. The base passes one-thumb hulls
    /// (pitch, yaw, 0, 0) — the Scarab's throttle arg was ALWAYS zero, so the elytra throttle
    /// sweep was dead code from the day it shipped (D-2 in the branch record). This class reads
    /// the vessel's real controls: the analog right trigger (autopilot reads as fully held —
    /// the transformer's own rule, mirrored so an AI Scarab doesn't cruise with slack wing
    /// cases), the raw left stick for the horn (the spring IS the filter; easing + spring is
    /// two low-pass filters on the pilot's aim instrument), and the eased stick for the body.
    /// Slip — the angle between the nose and the actual course — is DERIVED GEOMETRY, so the
    /// drift language works identically on every peer with no flag replication.
    ///
    /// The motion is a beetle's, not an aircraft's:
    /// - <b>Elytra</b> crack open outward under yaw and speed (outside of the turn opens
    ///   further), sweep back under real throttle, and the OUTSIDE case air-brakes in a drift.
    /// - <b>Legs</b> hang when slow / tuck at speed (normalized against the LIVE top speed, so
    ///   Time levels don't pin them tucked), row fore/aft with pitch, and paddle INTO the slide
    ///   during a drift.
    /// - <b>Horn</b> leads the turn on a fast critically-damped spring — the pilot's aim line.
    /// - <b>Antennae</b> are the secondary-motion showcase: heavy under-damped springs that lag,
    ///   whip on the juke, and scan at idle.
    /// - <b>The juke</b> snaps the whole silhouette — symmetric splay (elytra thrown open, legs
    ///   flung, antennae whipped), deliberately direction-agnostic: it stays legible inside the
    ///   dash's own 360° spin and reads identically on every peer from the rollSign-only event.
    /// </summary>
    class ScarabAnimation : VesselAnimation
    {
        [SerializeField] Transform LeftElytron;
        [SerializeField] Transform RightElytron;
        [SerializeField] Transform Horn;
        [SerializeField] Transform[] LeftLegs = new Transform[3];
        [SerializeField] Transform[] RightLegs = new Transform[3];
        [SerializeField] Transform LeftAntenna;
        [SerializeField] Transform RightAntenna;

        [Header("Flight pose (degrees — see the class note on fleet scale)")]
        [Tooltip("Degrees the wing cases crack open at full stick.")]
        [SerializeField] float elytraFlare = 40f;
        [Tooltip("Degrees the wing cases ride open just from carrying speed.")]
        [SerializeField] float elytraCruiseFlare = 14f;
        [Tooltip("Degrees the wing cases sweep back along the hull under throttle.")]
        [SerializeField] float elytraSweep = 16f;
        [Tooltip("Degrees the horn swings against the nose.")]
        [SerializeField] float hornScaler = 34f;
        [Tooltip("Degrees the legs hang DOWN when slow — landing gear out.")]
        [SerializeField] float legHang = 42f;
        [Tooltip("Degrees the legs fold UP against the shell at speed.")]
        [SerializeField] float legTuck = 30f;
        [Tooltip("Degrees the legs row fore/aft with pitch.")]
        [SerializeField] float legRow = 26f;
        [Tooltip("Fraction of the vessel's LIVE top speed at which the legs are fully tucked. " +
                 "Normalized against ScarabVesselTransformer.CurrentTopSpeed so the Time " +
                 "element's higher ceiling doesn't pin the fleet's best throttle read at " +
                 "'tucked' from two-thirds throttle up.")]
        [SerializeField, Range(0.2f, 1f)] float legTuckAtTopSpeedFraction = 0.8f;
        [Tooltip("Fallback top speed when the transformer isn't the Scarab's (never expected " +
                 "on this vessel; keeps the read sane on a miswired prefab).")]
        [SerializeField, Min(1f)] float fallbackTopSpeed = 180f;
        [Tooltip("Degrees of antenna response to the stick (the personality is the spring, " +
                 "not the amplitude).")]
        [SerializeField] float antennaScaler = 16f;

        [Header("Drift language (slip-driven — works on every peer)")]
        [Tooltip("Slip angle (nose vs course, degrees) at which the drift pose is fully on.")]
        [SerializeField, Min(1f)] float driftSlipFullDegrees = 25f;
        [Tooltip("Extra degrees the OUTSIDE wing case opens as an air brake in a full drift.")]
        [SerializeField] float driftElytraBrake = 24f;
        [Tooltip("Degrees the legs paddle into the slide in a full drift.")]
        [SerializeField] float driftLegPaddle = 22f;

        [Header("Springs (rad/s, damping ratio — per part group)")]
        [Tooltip("The horn is the pilot's aim instrument: fast and critically damped, ~0.13 s " +
                 "to settle, never a wobble.")]
        [SerializeField] Vector2 hornSpring = new(30f, 1f);
        [SerializeField] Vector2 elytraSpring = new(20f, 0.95f);
        [Tooltip("Under-damped so a leg genuinely settles — ζ 0.6 is ~8-16% visible overshoot; " +
                 "the 0.8 the first draft used measures 1.5% and reads as nothing at 50 u.")]
        [SerializeField] Vector2 legSpring = new(12f, 0.6f);
        [SerializeField] Vector2 antennaSpring = new(22f, 0.4f);

        [Header("Juke flourish (velocity impulses, degrees/second)")]
        [Tooltip("Kick thrown into both wing cases (outward) when a dash's roll starts — on " +
                 "EVERY peer, off ScarabJukeController.OnJukeRollStarted.")]
        [SerializeField] float jukeElytraKick = 320f;
        [SerializeField] float jukeLegKick = 260f;
        [SerializeField] float jukeAntennaKick = 520f;
        [Tooltip("Owner-local extra: a velocity-shift jump (snap dash, knockback) above this " +
                 "many u/s kicks the legs and antennae — flight feel the stick never sees.")]
        [SerializeField, Min(1f)] float shoveKickThreshold = 25f;

        [Header("Idle life (slow periodic motion, chase-camera scale)")]
        [Tooltip("Degrees of slow leg ripple when slow or idle. Sub-5° is sub-5px at 50 u.")]
        [SerializeField] float idleLegRipple = 10f;
        [SerializeField] float idleAntennaScan = 18f;
        [SerializeField] float idleElytraBreathe = 7f;
        [Tooltip("Breaths per second-ish; each channel runs its own phase so the set never " +
                 "moves as one rack.")]
        [SerializeField, Min(0.01f)] float idleRateHz = 0.25f;

        [Header("Wiring")]
        [Tooltip("The juke controller whose OnJukeRollStarted drives the dash flourish. " +
                 "Resolved from the root when empty.")]
        [SerializeField] ScarabJukeController jukeController;

        [Header("Audio")]
        [Tooltip("FMOD event for the dash's whoosh, played at the visual roll's start on every " +
                 "machine (spatialized). Leave empty for silence.")]
        [SerializeField] EventReference jukeWhooshEvent;

        [Header("Hull flare (MPB — the part-per-mesh answer to the base's material clone)")]
        [Tooltip("How hard FlareBody(1) lifts the hull's _ColorMultiplier. The base class's " +
                 "flare API writes renderer.materials on a SkinnedMeshRenderer this vessel " +
                 "doesn't render; these overrides drive the visible hull through per-renderer " +
                 "MaterialPropertyBlocks instead — get-modify-set, restored by writing the rest " +
                 "value, never cleared (the shared-channel law: EchoSight drives the same " +
                 "property).")]
        [SerializeField, Min(0f)] float flareGain = 2.5f;

        // ---- spring state: 3 channels per driven part, one writer -------------------------
        struct Springs
        {
            public AngularSpring.State Pitch, Yaw, Roll;
        }

        readonly Dictionary<Transform, Springs> _springs = new();
        readonly Dictionary<Transform, Vector2> _springParams = new(); // x = omega, y = zeta

        static readonly int ColorMultiplierId = Shader.PropertyToID("_ColorMultiplier");
        readonly List<Renderer> _flareRenderers = new();
        MaterialPropertyBlock _flareBlock;
        float _appliedFlare = 1f;

        float _idlePhase;
        float _previousShove;
        float _lastJukeFlourishTime = float.NegativeInfinity;
        ScarabVesselTransformer _scarabTransformer;

        protected override void ResolveParts()
        {
            LeftElytron = ResolvePart(LeftElytron, "elytron.l");
            RightElytron = ResolvePart(RightElytron, "elytron.r");
            Horn = ResolvePart(Horn, "horn");
            LeftAntenna = ResolvePart(LeftAntenna, "antenna.l");
            RightAntenna = ResolvePart(RightAntenna, "antenna.r");

            for (int i = 0; i < LeftLegs.Length; i++)
                LeftLegs[i] = ResolvePart(LeftLegs[i], $"leg.l{i + 1}");
            for (int i = 0; i < RightLegs.Length; i++)
                RightLegs[i] = ResolvePart(RightLegs[i], $"leg.r{i + 1}");

            // The procedural parts rest at identity, so this is currently a no-op — but it is
            // what lets authored art with angled rest poses drop in later without tearing flat.
            CaptureRestRotations(LeftElytron, RightElytron, Horn, LeftAntenna, RightAntenna);
            CaptureRestRotations(LeftLegs);
            CaptureRestRotations(RightLegs);

            RegisterSpring(Horn, hornSpring);
            RegisterSpring(LeftElytron, elytraSpring);
            RegisterSpring(RightElytron, elytraSpring);
            RegisterSpring(LeftAntenna, antennaSpring);
            RegisterSpring(RightAntenna, antennaSpring);
            foreach (var leg in LeftLegs) RegisterSpring(leg, legSpring);
            foreach (var leg in RightLegs) RegisterSpring(leg, legSpring);

            ReportUnresolvedParts();
        }

        void RegisterSpring(Transform part, Vector2 springParams)
        {
            if (!part) return;
            _springs[part] = default;
            _springParams[part] = springParams;
        }

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            _scarabTransformer = vesselStatus.VesselTransformer as ScarabVesselTransformer;

            // Detach-first: a vessel swap re-runs Initialize on live components, and a stranded
            // handler is the recorded triple-shipped bug class. The teardown in OnDisable is
            // unconditional for the same reason.
            if (!jukeController) jukeController = GetComponent<ScarabJukeController>();
            if (jukeController)
            {
                jukeController.OnJukeRollStarted -= HandleJukeRollStarted;
                jukeController.OnJukeRollStarted += HandleJukeRollStarted;
            }

            CollectFlareRenderers();
        }

        void OnDisable()
        {
            if (jukeController)
                jukeController.OnJukeRollStarted -= HandleJukeRollStarted;

            // Never leave a half-played flourish or a lifted flare behind (pooling / vessel
            // swap safety): springs to rest velocity, hull back to rest brightness.
            foreach (var part in new List<Transform>(_springs.Keys))
                _springs[part] = default;
            ApplyFlare(1f);
        }

        void OnEnable()
        {
            // Re-attach after a disable cycle (the subscription is torn down above). Initialize
            // has not run on a fresh spawn yet — jukeController is null there and the attach
            // happens in Initialize instead.
            if (jukeController)
            {
                jukeController.OnJukeRollStarted -= HandleJukeRollStarted;
                jukeController.OnJukeRollStarted += HandleJukeRollStarted;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (jukeController)
                jukeController.OnJukeRollStarted -= HandleJukeRollStarted;
        }

        // ---- the one pose pipeline --------------------------------------------------------

        protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
            => DrivePose(idle: false);

        /// <summary>Base Update routes here INSTEAD of puppetry whenever the input reads idle.
        /// Routing it into the same pipeline (with the sticks at zero) is what keeps one writer
        /// per channel — the base implementation is a second writer lerping parts to rest.</summary>
        protected override void Idle() => DrivePose(idle: true);

        void DrivePose(bool idle)
        {
            var status = VesselStatus;
            if (status == null) return;
            var input = status.InputStatus;
            float dt = Time.deltaTime;

            // -------- signals ---------------------------------------------------------------
            Vector2 rawStick = !idle && input != null ? input.LeftNormalizedJoystickPosition : Vector2.zero;
            Vector2 easedStick = !idle && input != null ? input.EasedLeftJoystickPosition : Vector2.zero;

            // The vessel's REAL throttle (D-2): the analog right trigger, with the
            // transformer's own autopilot rule mirrored so AI wing cases ride swept.
            float throttle01 = status.AutoPilotEnabled ? 1f
                : (!idle && input != null ? Mathf.Clamp01(input.RightTriggerAnalog) : 0f);

            float topSpeed = _scarabTransformer ? _scarabTransformer.CurrentTopSpeed : fallbackTopSpeed;
            float speed01 = Mathf.Clamp01(status.Speed / Mathf.Max(1f, topSpeed * legTuckAtTopSpeedFraction));

            // Slip: how hard the nose disagrees with the travel direction — geometry, not a
            // flag, so every peer computes the same drift pose off replicated motion.
            float slip = 0f;
            if (status.Course.sqrMagnitude > 1e-4f && status.Speed > 1f)
                slip = Vector3.SignedAngle(transform.forward, status.Course, transform.up);
            float drift01 = Mathf.Clamp01(Mathf.Abs(slip) / driftSlipFullDegrees);
            float slipSign = Mathf.Sign(slip);

            // Owner-local shove read (snap dash, knockbacks): a jump on the velocity-shift
            // channel kicks the limbs. Remote peers get the juke's kick from the event instead.
            var transformer = status.VesselTransformer;
            float shove = transformer ? transformer.VelocityShift.magnitude : 0f;
            // The juke's own displacement rides this channel on the owner — the event kick
            // already covers it, so the shove read stands down briefly or the owner's flourish
            // would double while every peer's plays once.
            if (shove - _previousShove > shoveKickThreshold
                && Time.time - _lastJukeFlourishTime > 0.35f)
                KickLimbs(0.6f);
            _previousShove = shove;

            // Idle life fades in as flight fades out, and each channel runs its own phase.
            _idlePhase += dt * idleRateHz * Mathf.PI * 2f;
            float idle01 = Mathf.Clamp01(1f - speed01 * 2f) * (idle ? 1f : 0.6f);

            // -------- targets ---------------------------------------------------------------
            float baseFlare = elytraCruiseFlare * speed01;
            float turn = Mathf.Clamp(easedStick.x, -1f, 1f);
            float sweep = -elytraSweep * throttle01;
            float breathe = Mathf.Sin(_idlePhase) * idleElytraBreathe * idle01;
            // Outside of the turn opens further; outside of the SLIDE air-brakes.
            float flareRight = baseFlare + elytraFlare * Mathf.Max(0f, turn)
                               + driftElytraBrake * drift01 * Mathf.Max(0f, -slipSign) + breathe;
            float flareLeft = baseFlare + elytraFlare * Mathf.Max(0f, -turn)
                              + driftElytraBrake * drift01 * Mathf.Max(0f, slipSign) + breathe;
            StepPart(RightElytron, 0f, sweep, -flareRight, dt);
            StepPart(LeftElytron, 0f, -sweep, flareLeft, dt);

            // Horn: raw stick on a fast critical spring — the aim line must not lag or wobble.
            StepPart(Horn, -rawStick.y * hornScaler, rawStick.x * hornScaler * 0.5f, 0f, dt);

            // Antennae: gentle stick response + idle scan; the ζ 0.4 spring is the show.
            float scan = Mathf.Sin(_idlePhase * 0.7f + 1.3f) * idleAntennaScan * idle01;
            StepPart(LeftAntenna, -easedStick.y * antennaScaler + scan,
                     -easedStick.x * antennaScaler * 0.5f, 0f, dt);
            StepPart(RightAntenna, -easedStick.y * antennaScaler + scan,
                     -easedStick.x * antennaScaler * 0.5f, 0f, dt);

            // Legs: signed hang↔tuck arc off measured speed, fore/aft row with pitch, a paddle
            // into the slide while drifting, and a travelling ripple at idle.
            float leg = Mathf.Lerp(legHang, -legTuck, speed01);
            float row = legRow * easedStick.y;
            float paddle = driftLegPaddle * drift01 * slipSign;
            for (int i = 0; i < LeftLegs.Length; i++)
            {
                float stagger = i == 1 ? 0.65f : 1f;
                float phase = i == 1 ? -1f : 1f;
                float ripple = Mathf.Sin(_idlePhase * 2f + i * 1.05f) * idleLegRipple * idle01;
                StepPart(LeftLegs[i], row * phase + paddle, 0f, leg * stagger + ripple, dt);
                StepPart(RightLegs[i], row * phase - paddle, 0f, -(leg * stagger + ripple), dt);
            }
        }

        void StepPart(Transform part, float pitch, float yaw, float roll, float dt)
        {
            if (!part || !_springs.TryGetValue(part, out var springs)) return;
            var p = _springParams[part];
            springs.Pitch = AngularSpring.Step(springs.Pitch, pitch, p.x, p.y, dt);
            springs.Yaw = AngularSpring.Step(springs.Yaw, yaw, p.x, p.y, dt);
            springs.Roll = AngularSpring.Step(springs.Roll, roll, p.x, p.y, dt);
            _springs[part] = springs;

            part.localRotation =
                Quaternion.Euler(springs.Pitch.Position, springs.Yaw.Position, springs.Roll.Position)
                * RestRotationOf(part);
        }

        // ---- event flourishes -------------------------------------------------------------

        /// <summary>
        /// The dash flourish, on EVERY machine that plays the juke's visual roll (the owner
        /// directly, remote peers via the ClientRpc). Deliberately SYMMETRIC — a pose coded in
        /// hull-local direction scrambles inside the dash's own 360° spin, and the event
        /// carries no dash direction on remote peers by design — so the read is: the whole
        /// beetle throws itself open. Photons only (the cosmetic-path law).
        /// </summary>
        void HandleJukeRollStarted(float rollSign, float duration)
        {
            _lastJukeFlourishTime = Time.time;
            Kick(RightElytron, roll: -jukeElytraKick);
            Kick(LeftElytron, roll: jukeElytraKick);
            KickLimbs(1f);
            Kick(Horn, pitch: -jukeAntennaKick * 0.3f);
            if (!jukeWhooshEvent.IsNull && AudioSystem.Instance)
                AudioSystem.Instance.PlaySFXEvent(jukeWhooshEvent, transform.position);
        }

        void KickLimbs(float scale)
        {
            for (int i = 0; i < LeftLegs.Length; i++)
            {
                Kick(LeftLegs[i], roll: jukeLegKick * scale);
                Kick(RightLegs[i], roll: -jukeLegKick * scale);
            }
            Kick(LeftAntenna, pitch: -jukeAntennaKick * scale);
            Kick(RightAntenna, pitch: -jukeAntennaKick * scale);
        }

        void Kick(Transform part, float pitch = 0f, float yaw = 0f, float roll = 0f)
        {
            if (!part || !_springs.TryGetValue(part, out var springs)) return;
            AngularSpring.AddImpulse(ref springs.Pitch, pitch);
            AngularSpring.AddImpulse(ref springs.Yaw, yaw);
            AngularSpring.AddImpulse(ref springs.Roll, roll);
            _springs[part] = springs;
        }

        // ---- hull flare (MPB overrides of the base's material-clone API) --------------------

        void CollectFlareRenderers()
        {
            _flareRenderers.Clear();
            var builder = GetComponentInChildren<ScarabHullBuilder>(true);
            if (!builder) return;
            var own = builder.GetComponent<MeshRenderer>();
            if (own) _flareRenderers.Add(own);
            for (int i = 0; i < builder.transform.childCount; i++)
            {
                var r = builder.transform.GetChild(i).GetComponent<MeshRenderer>();
                if (r) _flareRenderers.Add(r);
            }
        }

        void ApplyFlare(float multiplier)
        {
            if (Mathf.Approximately(multiplier, _appliedFlare)) return;
            _appliedFlare = multiplier;
            _flareBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < _flareRenderers.Count; i++)
            {
                var r = _flareRenderers[i];
                if (!r) continue;
                // Get-modify-set: preserves every other system's channel on this renderer
                // (the vision band's tint, EchoSight's colours). Restore is writing the rest
                // value — never SetPropertyBlock(null), which erases everyone (CLAUDE.md law).
                r.GetPropertyBlock(_flareBlock);
                _flareBlock.SetFloat(ColorMultiplierId, multiplier);
                r.SetPropertyBlock(_flareBlock);
            }
        }

        public override void FlareEngine() => ApplyFlare(1f + flareGain);
        public override void StopFlareEngine() => ApplyFlare(1f);
        public override void FlareBody() => ApplyFlare(1f + flareGain);
        public override void FlareBody(float amount) => ApplyFlare(1f + Mathf.Clamp01(amount) * flareGain);
        public override void StopFlareBody() => ApplyFlare(1f);

        protected override void AssignTransforms()
        {
            Transforms.Add(LeftElytron);
            Transforms.Add(RightElytron);
            Transforms.Add(Horn);
            Transforms.Add(LeftAntenna);
            Transforms.Add(RightAntenna);
            for (int i = 0; i < LeftLegs.Length; i++) Transforms.Add(LeftLegs[i]);
            for (int i = 0; i < RightLegs.Length; i++) Transforms.Add(RightLegs[i]);
        }
    }
}
