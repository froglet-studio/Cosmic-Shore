using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spider vessel transformer: dual-tether swinging through the hypersea.
    /// Think gibbon, not aircraft — momentum is earned by swinging, never throttled.
    ///
    /// Speed model:
    ///   The spider has NO throttle. Speed is purely displacement-based.
    ///   Dual-tether pumping (angular momentum conservation + pump energy
    ///   injection) is the only way to gain speed. Single tether redirects
    ///   momentum. Free flight coasts.
    ///
    /// Dissipation (the other half of a real swing):
    ///   In space there is no gravity arc or air to bound the pendulum, so
    ///   the dissipation is restored explicitly — one physical story, four
    ///   mechanisms, each dialable to zero:
    ///     - Quadratic drag while anchored (swingDragK) acts on L, so a
    ///       terminal velocity EMERGES where pump injection balances drag —
    ///       no hard clamp, no throttle.
    ///     - The same quadratic drag in free flight (freeFlightDragK)
    ///       coasts a hard launch back down to a driftable speed;
    ///       MinimumSpeed floors it so the vessel can never strand.
    ///     - Reverse-pump bleed (reversePumpBleed): expanding the circle
    ///       does negative work, exactly mirroring the injection term.
    ///       Spread the sticks to pump up, relax them to bleed down — the
    ///       symmetric, discoverable, skill-based brake.
    ///     - Air control (airControlDegPerSec): in free flight the coast
    ///       course steers toward the nose at a constant rate, so an
    ///       overshot launch always has a way back to the prismscape.
    ///
    /// Controls:
    ///   Each stick positions that side's cursor in screen space: X slides it
    ///   along the horizontal axis through the vessel (inward = at vessel,
    ///   outward = side edge), Y slides it vertically toward the top/bottom edge
    ///   — so you aim in a full 2D cone, not just left/right. Pulling a trigger
    ///   fires that side's tether from the cursor, straight into the scene along
    ///   the camera ray. Hit a prism → anchor. Max range → spawn an anchor prism
    ///   and latch on.
    ///
    ///   One anchor:  vessel is constrained to the anchor sphere. Forward is
    ///   projected onto the tangent plane; course lerps toward it (drift feel).
    ///   A single tether redirects momentum and bleeds it under swing drag —
    ///   it never adds speed.
    ///
    ///   Two anchors: vessel rides the intersection circle of both tether
    ///   spheres. XDiff (the spread of both sticks) sets the target circle
    ///   radius. Angular momentum L = ω·h² is conserved, so contracting the
    ///   radius spins you up like an ice skater (v ∝ 1/h), and each active
    ///   contraction injects pump energy into L. Pump, release, fly.
    ///
    /// Lightsaber tethers:
    ///   While anchored, tethers destroy every prism they sweep through
    ///   (except the anchors themselves). Swing through a trail and slice it.
    /// </summary>
    public class SwingingVesselTransformer : VesselTransformer
    {
        [Header("Tether")]
        [SerializeField] float tetherSpeed = 300f;
        [SerializeField] float maxTetherLength = 150f;
        [Tooltip("Base world-radius of the tether capsule. Thick enough to read as a 3D rod with shading, not a hairline.")]
        [SerializeField] float tetherRadius = 0.14f;
        [Tooltip("Optional lit material override. Left empty, a shaded HDR-cyan URP/Lit material is built at runtime so the beam catches scene light (depth) and blooms.")]
        [SerializeField] Material tetherMaterial;
        [Tooltip("Seconds the tether takes to retract to the arm on release. Continuity-of-existence law: a player-visible beam must never instant-pop out. The ignite already grows from zero; this animates the retract.")]
        [SerializeField] float tetherRetractDuration = 0.15f;

        [Header("Spinneret Arms")]
        [Tooltip("Visual thickness of the arm capsule.")]
        [SerializeField] float armRadius = 0.07f;

        [Header("Aiming")]
        [Tooltip("Stick Y also steers the cursor vertically (toward the top/bottom screen edge), not just horizontally along the vessel's screen axis — so you can fire tethers up and down. 0 = horizontal-only aiming (legacy).")]
        [SerializeField] float verticalAimGain = 1f;
        [Tooltip("Flip if pushing the stick up aims the cursor down — stick Y sign varies by input device.")]
        [SerializeField] bool invertVerticalAim = false;

        [Header("Course Lerp")]
        [Tooltip("How fast the tethered course catches up to the projected forward (drift feel).")]
        [SerializeField] float courseLerp = 1.5f;

        [Header("Tether Length")]
        [Tooltip("How fast the actual circle radius lerps toward the XDiff target during dual-anchor pumping.")]
        [SerializeField] float tetherLengthLerpSpeed = 3f;

        [Header("Angular Momentum Pump")]
        [Tooltip("Energy injected per unit of contraction rate × angular velocity. Higher = faster speed gain per pump.")]
        [SerializeField] float pumpGain = 0.5f;
        [Tooltip("Minimum circle radius — caps the 1/r speed spike near the axis.")]
        [SerializeField] float minCircleRadius = 1f;

        [Header("Dissipation & Air Control")]
        [Tooltip("Quadratic drag while anchored, units 1/meters (≈ 1/braking-length-scale). Dual-anchor: dL/dt = −k·v·L (quadratic-in-v drag expressed on the persistent L). Single-anchor: dv/dt = −k·v². Terminal velocity EMERGES where pump injection balances this — no hard clamp, no throttle. Lower = higher top speed; at 0.0012 drag removes ~18%/s at v=150. 0 = frictionless (runaway).")]
        [SerializeField] float swingDragK = 0.0012f;
        [Tooltip("Quadratic drag in free flight, units 1/meters: dv/dt = −k·v². Coasts a hard launch down gracefully; lower = keeps speed longer (150→100 in ~5s at 0.001). MinimumSpeed still floors movement, so the vessel can never strand. 0 = release speed kept forever.")]
        [SerializeField] float freeFlightDragK = 0.001f;
        [Tooltip("Negative work done on L when the circle EXPANDS (sticks relaxed inward), exactly mirroring the pump injection term. Spread the sticks to pump up, relax them to bleed down — the symmetric, skill-based brake. Start equal to pumpGain.")]
        [SerializeField] float reversePumpBleed = 0.5f;
        [Tooltip("Degrees/second the free-flight course steers toward the vessel's nose. Constant-rate (RotateTowards, not Slerp) so steering authority is predictable at any speed — this is the 'way back' after overshooting every prism. 0 = ballistic (soft-lock risk).")]
        [SerializeField] float airControlDegPerSec = 45f;
        [Tooltip("Constant per-second fractional bleed of L while dual-anchored: L *= (1 − damp·dt). Guarantees pumping plateaus even if swingDragK is tuned near zero. 0 = off (default).")]
        [SerializeField] float swingDamp = 0f;
        [Tooltip("Extra tether range earned by speed: range = maxTetherLength · (1 + scale · speed/speedThicknessRef), so a fast launch can still find an anchor. 0 = off (default).")]
        [SerializeField] float rangeSpeedScale = 0f;

        [Header("Lightsaber Sweep")]
        [Tooltip("Anchored tethers destroy every prism they sweep through (except the anchors).")]
        [SerializeField] bool sweepDestroysPrisms = true;
        [Tooltip("Blade kerf — a prism whose centre is within this distance of the taut tether line gets sliced. The blade has thickness, so it cuts everything it visually sweeps (a zero-width ray would miss prism centres it grazes).")]
        [SerializeField] float sweepBladeRadius = 2.5f;
        [Tooltip("Max world-distance the vessel can move between sweep sub-steps. Lower = denser coverage at high speed.")]
        [SerializeField] float sweepStepDistance = 4f;
        [Tooltip("How fast the kill-pulse on the tether visual decays.")]
        [SerializeField] float sweepPulseDecay = 4f;

        [Header("Speed Feel")]
        [Tooltip("Tether visual thickens up to this multiplier as speed rises (lightsaber heat).")]
        [SerializeField] float speedThicknessBoost = 1.5f;
        [Tooltip("Speed at which the thickness boost saturates. Align to the tuned terminal velocity (hard-pump peak target ≈ 120–180) so 'white hot' means 'as fast as this vessel goes'. Also normalizes release-shake scaling and the rangeSpeedScale bonus.")]
        [SerializeField] float speedThicknessRef = 150f;

        [Header("Juice")]
        [Tooltip("Camera-shake intensity on tether release, scaled by speed/speedThicknessRef — a hard slingshot kicks, a gentle detach whispers. Local player only. 0 = off.")]
        [SerializeField] float releaseShakeIntensity = 0.8f;
        [Tooltip("Seconds the release shake decays over.")]
        [SerializeField] float releaseShakeDuration = 0.25f;
        [Tooltip("Noise gate: releases below this fraction of speedThicknessRef don't shake at all.")]
        [SerializeField] float releaseShakeSpeedFloor = 0.05f;
        [Tooltip("Camera-shake tick when the lightsaber sweep slices at least one prism this frame. Local player only. 0 = off.")]
        [SerializeField] float sweepKillShakeIntensity = 0.2f;
        [Tooltip("Seconds the sweep-kill tick decays over.")]
        [SerializeField] float sweepKillShakeDuration = 0.1f;

        [Header("Anchor Prism")]
        [SerializeField] Vector3 anchorPrismScale = new(6f, 6f, 6f);
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnChannel;

        // ---- Internal types ----

        public enum SwingState { FreeFlight = 0, SingleAnchor = 1, DualAnchor = 2 }

        struct TetherState
        {
            public bool triggerHeld;
            public bool isAnchored;
            public bool isFiring;
            public Transform anchor;
            public float ropeLength;
            public float extension;
            public float maxRange;
            public Vector3 fireOrigin;
            public Vector3 fireDirection;
            public Transform capsule;
            public MeshRenderer capsuleRenderer;
            public float sweepPulse;
            // Continuity-of-existence retract: far end slides back to the arm.
            public bool isRetracting;
            public float retractTimer;
            public Vector3 retractEnd;
        }

        // ---- Public API ----

        /// <summary>True when the vessel is attached to at least one anchor.</summary>
        public bool IsSwinging => currentState != SwingState.FreeFlight;

        /// <summary>Current swing state — read-only, for HUD/telemetry.</summary>
        public SwingState State => currentState;

        /// <summary>Persistent displacement-earned speed (m/s) — the momentum the vessel carries between states.</summary>
        public float CurrentSpeed => speed;

        /// <summary>Signed angular momentum L = ω·h² (per unit mass). Stale outside DualAnchor.</summary>
        public float AngularMomentum => angularMomentum;

        /// <summary>Current dual-anchor circle radius h (meters). Stale outside DualAnchor.</summary>
        public float CircleRadius => currentH;

        /// <summary>Zero-alloc state name for the debug telemetry readout.</summary>
        public string StateName => StateNames[(int)currentState];
        static readonly string[] StateNames = { "FreeFlight", "SingleAnchor", "DualAnchor" };

        /// <summary>Called by SwingActionSO.StartAction — enables swing mode.</summary>
        public void StartSwing() { }

        /// <summary>Called by SwingActionSO.StopAction — releases all tethers.</summary>
        public void ReleaseSwing()
        {
            ReleaseTether(ref leftTether);
            leftTether.triggerHeld = false;
            ReleaseTether(ref rightTether);
            rightTether.triggerHeld = false;
        }

        // ---- State ----

        TetherState leftTether;
        TetherState rightTether;
        SwingState currentState;

        // Sphere navigation (single anchor) — course on tangent plane
        Vector3 sphereCourse;

        // Circle navigation (dual anchor)
        float circleAngle;
        float circleAngularVelocity;
        float angularMomentum; // L = ω·h² (signed, per unit mass)

        // Dual-anchor geometry
        float dualAnchorA;
        float dualAnchorHomeH;
        float currentH;

        // Momentum tracking across state transitions
        Vector3 lastVelocity;

        // Free-flight course — decoupled from transform.forward
        Vector3 freeFlightCourse;

        // Spinneret arm visuals (world-space capsules from vessel to cursor)
        Transform leftArm;
        MeshRenderer leftArmRenderer;
        Transform rightArm;
        MeshRenderer rightArmRenderer;

        // Deferred anchor spawns
        Vector3? pendingLeftSpawnPos;
        Vector3? pendingRightSpawnPos;

        Material sharedTetherMaterial;
        // Reused scratch for the spatial-index sweep query (allocation-free; the
        // tick consumes it within one call, so one shared list is safe).
        static readonly List<Prism> sweepScratch = new(128);

        int trailBlocksLayer = -1;
        int TrailBlocksLayer
        {
            get
            {
                if (trailBlocksLayer < 0)
                    trailBlocksLayer = LayerMask.NameToLayer("TrailBlocks");
                return trailBlocksLayer;
            }
        }

        // ---- Lifecycle ----

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);

            EnsureSharedMaterial();
            CreateTetherCapsule("LeftTether", ref leftTether);
            CreateTetherCapsule("RightTether", ref rightTether);
            CreateArmCapsule("LeftArm", out leftArm, out leftArmRenderer);
            CreateArmCapsule("RightArm", out rightArm, out rightArmRenderer);

            freeFlightCourse = transform.forward;
            if (VesselStatus != null)
                VesselStatus.Course = freeFlightCourse;

            var handler = VesselStatus?.ActionHandler;
            if (handler != null)
            {
                handler.OnInputEventStarted += HandleInputStarted;
                handler.OnInputEventStopped += HandleInputStopped;
            }
        }

        void OnDisable()
        {
            var handler = VesselStatus?.ActionHandler;
            if (handler != null)
            {
                handler.OnInputEventStarted -= HandleInputStarted;
                handler.OnInputEventStopped -= HandleInputStopped;
            }
        }

        void OnDestroy()
        {
            if (leftTether.capsule) Destroy(leftTether.capsule.gameObject);
            if (rightTether.capsule) Destroy(rightTether.capsule.gameObject);
            if (leftArm) Destroy(leftArm.gameObject);
            if (rightArm) Destroy(rightArm.gameObject);
            if (sharedTetherMaterial && sharedTetherMaterial != tetherMaterial)
                Destroy(sharedTetherMaterial);
        }

        void EnsureSharedMaterial()
        {
            if (tetherMaterial != null)
            {
                sharedTetherMaterial = tetherMaterial;
                return;
            }

            // LIT, not Unlit: an unlit tube renders as a flat silhouette with no
            // depth cue — you can't tell where in space the beam is. URP/Lit gives
            // the capsule a real shaded gradient + a glossy specular streak down
            // its length (high smoothness), so it reads as a 3D rod; the HDR
            // emission still blooms for the lightsaber glow. Base colour is kept
            // dim so the shading isn't blown out to a flat white by the emission.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            bool lit = shader != null;
            if (!lit)
                shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return;

            sharedTetherMaterial = new Material(shader);

            if (lit)
            {
                var baseColor = new Color(0f, 0.35f, 0.4f, 1f); // dim so shading shows
                var emission = new Color(0f, 1.6f, 1.8f, 1f);   // HDR cyan glow (blooms)
                sharedTetherMaterial.color = baseColor;
                sharedTetherMaterial.SetColor("_BaseColor", baseColor);
                sharedTetherMaterial.SetFloat("_Metallic", 0f);
                sharedTetherMaterial.SetFloat("_Smoothness", 0.85f); // glossy → 3D specular streak
                sharedTetherMaterial.EnableKeyword("_EMISSION");
                sharedTetherMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                sharedTetherMaterial.SetColor("_EmissionColor", emission);
            }
            else
            {
                // Unlit fallback (older pipelines) — HDR cyan, flat but visible.
                var hot = new Color(0f, 2f, 2f, 1f);
                sharedTetherMaterial.color = hot;
                sharedTetherMaterial.SetColor("_BaseColor", hot);
            }
        }

        void CreateTetherCapsule(string childName, ref TetherState tether)
        {
            var go = CreateCapsule(childName, out var mr);
            mr.enabled = false;
            tether.capsule = go.transform;
            tether.capsuleRenderer = mr;
        }

        void CreateArmCapsule(string childName, out Transform armTransform, out MeshRenderer armRenderer)
        {
            var go = CreateCapsule(childName, out armRenderer);
            armRenderer.enabled = true;
            armTransform = go.transform;
        }

        GameObject CreateCapsule(string childName, out MeshRenderer mr)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = childName;

            if (go.TryGetComponent<Collider>(out var col))
                Destroy(col);

            mr = go.GetComponent<MeshRenderer>();
            if (sharedTetherMaterial != null)
                mr.sharedMaterial = sharedTetherMaterial;
            return go;
        }

        // ==================================================================
        //  INPUT
        // ==================================================================

        void HandleInputStarted(InputEvents ie)
        {
            if (ie == InputEvents.LeftStickAction)
            {
                leftTether.triggerHeld = true;
                if (!leftTether.isAnchored)
                {
                    var (tipPos, aimTarget) = GetSpinneretAim(true);
                    FireTetherFromTip(ref leftTether, tipPos, aimTarget);
                }
            }
            else if (ie == InputEvents.RightStickAction)
            {
                rightTether.triggerHeld = true;
                if (!rightTether.isAnchored)
                {
                    var (tipPos, aimTarget) = GetSpinneretAim(false);
                    FireTetherFromTip(ref rightTether, tipPos, aimTarget);
                }
            }
        }

        void HandleInputStopped(InputEvents ie)
        {
            if (ie == InputEvents.LeftStickAction)
            {
                leftTether.triggerHeld = false;
                ReleaseTether(ref leftTether);
            }
            else if (ie == InputEvents.RightStickAction)
            {
                rightTether.triggerHeld = false;
                ReleaseTether(ref rightTether);
            }
        }

        // ==================================================================
        //  CURSOR POSITIONING (per-stick, screen-space: X = horizontal, Y = vertical)
        // ==================================================================

        CustomCameraController GetCameraController()
        {
            var controller = CameraManager.Instance != null ? CameraManager.Instance.GetActiveController() : null;
            return controller as CustomCameraController;
        }

        Camera GetGameplayCamera()
        {
            var ccc = GetCameraController();
            if (ccc != null && ccc.Camera != null)
                return ccc.Camera;
            return Camera.main;
        }

        /// <summary>
        /// Tether range including the speed-earned bonus: a hard launch can
        /// reach anchors a parked spider can't. rangeSpeedScale=0 (default)
        /// keeps the base maxTetherLength.
        /// </summary>
        float EffectiveTetherRange =>
            maxTetherLength * (1f + rangeSpeedScale * speed / Mathf.Max(speedThicknessRef, 1f));

        /// <summary>
        /// Per-stick travel toward the screen edge, in [0,1].
        /// Left stick pushed left (outward) sends the left cursor to the left
        /// edge; pushed right (inward) brings it back to the vessel. Mirrored
        /// for the right stick. Neutral rests halfway.
        /// </summary>
        float GetCursorTravel(bool left)
        {
            if (InputStatus == null) return 0.5f;
            float stickX = left
                ? InputStatus.EasedLeftJoystickPosition.x
                : InputStatus.EasedRightJoystickPosition.x;
            float t = left ? (1f - stickX) * 0.5f : (1f + stickX) * 0.5f;
            return Mathf.Clamp01(t);
        }

        /// <summary>
        /// Per-stick vertical travel toward the top (+) or bottom (−) screen
        /// edge, in [−1,1], from the matching stick's Y. verticalAimGain scales
        /// the reach (0 = horizontal-only); invertVerticalAim flips it for
        /// devices whose stick-up reports negative Y.
        /// </summary>
        float GetCursorVertical(bool left)
        {
            if (InputStatus == null) return 0f;
            float stickY = left
                ? InputStatus.EasedLeftJoystickPosition.y
                : InputStatus.EasedRightJoystickPosition.y;
            if (invertVerticalAim) stickY = -stickY;
            return Mathf.Clamp(stickY * verticalAimGain, -1f, 1f);
        }

        /// <summary>
        /// World-space cursor position for a given side, expressed entirely in
        /// screen space (world-space depth-plane math drifted and was ripped out
        /// twice). Stick X slides the cursor along the horizontal screen axis
        /// between the vessel (inward) and the side edge (outward); stick Y
        /// slides it vertically toward the top/bottom edge. ScreenToWorldPoint at
        /// the vessel's own depth turns that 2D screen point back into the world.
        /// </summary>
        Vector3 GetCursorWorldPosition(bool left)
        {
            var cam = GetGameplayCamera();
            float travelX = GetCursorTravel(left);
            float travelY = GetCursorVertical(left);

            if (cam == null)
                return FallbackCursor(left, travelX, travelY);

            Vector3 vesselScreen = cam.WorldToScreenPoint(transform.position);
            if (vesselScreen.z <= 0f) // vessel behind camera — projection is garbage
                return FallbackCursor(left, travelX, travelY);

            float edgeX = left ? 0f : Screen.width;
            float cursorX = Mathf.Lerp(vesselScreen.x, edgeX, travelX);

            float edgeY = travelY >= 0f ? Screen.height : 0f;
            float cursorY = Mathf.Lerp(vesselScreen.y, edgeY, Mathf.Abs(travelY));

            return cam.ScreenToWorldPoint(new Vector3(cursorX, cursorY, vesselScreen.z));
        }

        /// <summary>No-camera fallback: nudge along the vessel's own right/up axes.</summary>
        Vector3 FallbackCursor(bool left, float travelX, float travelY)
            => transform.position
             + (left ? -transform.right : transform.right) * (20f * travelX)
             + transform.up * (20f * travelY);

        /// <summary>
        /// Spinneret arm tip position and the aim target. The tether fires
        /// from the cursor straight into the scene along the camera ray
        /// (cursor − camera), so what you see is exactly where it goes.
        /// </summary>
        (Vector3 tipPos, Vector3 aimTarget) GetSpinneretAim(bool left)
        {
            Vector3 tip = GetCursorWorldPosition(left);
            float range = EffectiveTetherRange;

            var cam = GetGameplayCamera();
            if (cam == null)
                return (tip, tip + transform.forward * range);

            Vector3 fireDir = (tip - cam.transform.position).normalized;

            int layerMask = 1 << TrailBlocksLayer;
            if (Physics.Raycast(tip, fireDir, out var hit, range, layerMask))
                return (tip, hit.point);
            return (tip, tip + fireDir * range);
        }

        /// <summary>
        /// Arm tip position based on tether state. Anchored: toward the
        /// anchor. Firing: along the fire direction. Free: at the cursor.
        /// Stick travel always controls reach.
        /// </summary>
        Vector3 GetArmTipPosition(TetherState tether, bool isLeft)
        {
            Vector3 cursorPos = GetCursorWorldPosition(isLeft);

            if (tether.isAnchored && tether.anchor != null)
            {
                float len = Vector3.Distance(transform.position, cursorPos);
                Vector3 dir = (tether.anchor.position - transform.position).normalized;
                return transform.position + dir * len;
            }

            if (tether.isFiring)
            {
                float len = Vector3.Distance(transform.position, cursorPos);
                return transform.position + tether.fireDirection * len;
            }

            return cursorPos;
        }

        void FireTetherFromTip(ref TetherState tether, Vector3 tipPosition, Vector3 aimTarget)
        {
            tether.isFiring = true;
            tether.isRetracting = false; // re-fire cancels any in-flight retract
            tether.extension = 0f;
            tether.maxRange = EffectiveTetherRange; // snapshot — one fire, one range
            tether.fireOrigin = tipPosition;
            Vector3 dir = aimTarget - tipPosition;
            tether.fireDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            tether.capsuleRenderer.enabled = true;
        }

        void ReleaseTether(ref TetherState tether)
        {
            // Continuity of existence: the lightsaber retracts to the spinneret
            // rather than vanishing. Capture the current far end and let
            // UpdateTetherVisual slide the visible beam back to the arm tip.
            bool wasVisible = tether.capsuleRenderer != null && tether.capsuleRenderer.enabled;
            if (wasVisible && tetherRetractDuration > 0f)
            {
                if (tether.isAnchored && tether.anchor != null)
                    tether.retractEnd = tether.anchor.position;
                else if (tether.isFiring)
                    tether.retractEnd = tether.fireOrigin + tether.fireDirection * tether.extension;
                // else: already retracting — keep the existing retractEnd target

                tether.retractTimer = tetherRetractDuration;
                tether.isRetracting = true;
            }
            else if (tether.capsuleRenderer != null)
            {
                tether.capsuleRenderer.enabled = false;
                tether.isRetracting = false;
            }

            tether.isAnchored = false;
            tether.isFiring = false;
            tether.anchor = null;
            tether.sweepPulse = 0f;
        }

        // ==================================================================
        //  UPDATE LOOP
        // ==================================================================

        protected override void Update()
        {
            if (VesselStatus == null || VesselStatus.IsStationary)
                return;

            Vector3 frameStartPos = transform.position;

            ProcessDeferredSpawn(ref leftTether, ref pendingLeftSpawnPos);
            ProcessDeferredSpawn(ref rightTether, ref pendingRightSpawnPos);

            UpdateFiringTether(ref leftTether, true);
            UpdateFiringTether(ref rightTether, false);
            ValidateAnchor(ref leftTether);
            ValidateAnchor(ref rightTether);

            DetermineState();

            base.Update(); // rotation + MoveShip (our overrides)

            // Lightsaber sweep covers the path moved this frame
            if (sweepDestroysPrisms)
            {
                SweepTether(ref leftTether, frameStartPos);
                SweepTether(ref rightTether, frameStartPos);
            }

            UpdateSpinneretArms();
            UpdateTetherVisual(ref leftTether, true);
            UpdateTetherVisual(ref rightTether, false);
        }

        void DetermineState()
        {
            bool leftActive = leftTether.isAnchored && leftTether.triggerHeld;
            bool rightActive = rightTether.isAnchored && rightTether.triggerHeld;

            SwingState next;
            if (leftActive && rightActive)       next = SwingState.DualAnchor;
            else if (leftActive || rightActive)  next = SwingState.SingleAnchor;
            else                                 next = SwingState.FreeFlight;

            if (next != currentState)
                TransitionTo(next);
        }

        void TransitionTo(SwingState next)
        {
            currentState = next;

            switch (next)
            {
                case SwingState.FreeFlight:
                    if (lastVelocity.sqrMagnitude > 0.01f)
                        freeFlightCourse = lastVelocity.normalized;
                    // The persistent speed field is deliberately NOT re-baked
                    // from lastVelocity: both anchored states already keep it
                    // honest (dual: |ω|·h, single: drag-decayed carry-in),
                    // while lastVelocity carries transient OUTPUT multipliers
                    // (boost, throttle mods, the MinimumSpeed floor). Baking
                    // those in turned every boosted anchor-release cycle into
                    // a permanent ×BoostMultiplier speed gain, compounding
                    // exponentially — the loophole around all the drag above.
                    VesselStatus.Course = freeFlightCourse;
                    PlayReleaseShake();
                    break;

                case SwingState.SingleAnchor:
                    InitSphereCourseFromCurrentState();
                    break;

                case SwingState.DualAnchor:
                    InitDualAnchorFromCurrentState();
                    break;
            }
        }

        // ==================================================================
        //  TETHER FIRING & VALIDATION
        // ==================================================================

        void UpdateFiringTether(ref TetherState tether, bool isLeft)
        {
            if (!tether.isFiring) return;

            float prevExt = tether.extension;
            tether.extension += tetherSpeed * Time.deltaTime;

            Vector3 prevTip = tether.fireOrigin + tether.fireDirection * prevExt;
            float segLen = tether.extension - prevExt;

            int layerMask = 1 << TrailBlocksLayer;
            if (Physics.Raycast(prevTip, tether.fireDirection, out var hit, segLen, layerMask))
            {
                if (hit.collider.TryGetComponent<Prism>(out var prism) && !prism.destroyed)
                {
                    float rl = Vector3.Distance(transform.position, hit.collider.transform.position);
                    AnchorTether(ref tether, hit.collider.transform, rl);
                    return;
                }
            }

            if (tether.extension >= tether.maxRange)
            {
                Vector3 spawnPos = tether.fireOrigin + tether.fireDirection * tether.maxRange;
                tether.isFiring = false;
                tether.capsuleRenderer.enabled = false;

                if (isLeft)
                    pendingLeftSpawnPos = spawnPos;
                else
                    pendingRightSpawnPos = spawnPos;
            }
        }

        void ProcessDeferredSpawn(ref TetherState tether, ref Vector3? pendingPos)
        {
            if (!pendingPos.HasValue) return;

            var pos = pendingPos.Value;
            pendingPos = null;

            var anchor = SpawnAnchorPrism(pos);
            if (anchor != null && tether.triggerHeld)
            {
                float rl = Vector3.Distance(transform.position, anchor.position);
                AnchorTether(ref tether, anchor, rl);
            }
        }

        void AnchorTether(ref TetherState tether, Transform anchor, float ropeLen)
        {
            tether.isFiring = false;
            tether.isAnchored = true;
            tether.anchor = anchor;
            tether.ropeLength = Mathf.Max(ropeLen, 1f);
        }

        void ValidateAnchor(ref TetherState tether)
        {
            if (!tether.isAnchored) return;

            if (tether.anchor == null)
            {
                ReleaseTether(ref tether);
                return;
            }

            if (tether.anchor.TryGetComponent<Prism>(out var prism) && prism.destroyed)
                ReleaseTether(ref tether);
        }

        // ==================================================================
        //  LIGHTSABER SWEEP
        // ==================================================================

        /// <summary>
        /// While anchored, the taut tether destroys every prism it sweeps
        /// through — except the anchors themselves. Queried through
        /// PrismSpatialIndex rather than physics raycasts: the index sees ALL
        /// live prism mass, including freshly-laid trail whose colliders are
        /// disabled for the first ~0.6s after spawn (a raycast would pass
        /// through that fresh mass ghostlike — see Docs/SPATIAL_INDEX.md). One
        /// sphere query covers the region swept this frame; each candidate is
        /// then point-to-segment tested against the (sub-stepped) tether line,
        /// so fast swings don't skip prisms between frames.
        /// </summary>
        void SweepTether(ref TetherState tether, Vector3 prevPos)
        {
            if (!tether.isAnchored || tether.anchor == null) return;

            var index = PrismSpatialIndex.Instance;
            if (index == null) return; // no spatial index in this scene → no registered prism mass to slice

            Vector3 anchorPos = tether.anchor.position;
            Vector3 currStart = transform.position;
            float moved = Vector3.Distance(prevPos, currStart);
            int steps = Mathf.Clamp(Mathf.CeilToInt(moved / Mathf.Max(sweepStepDistance, 0.5f)), 1, 4);

            Transform otherAnchor = (tether.anchor == leftTether.anchor) ? rightTether.anchor : leftTether.anchor;

            // One query bounding the whole swept region (current tether line +
            // the vessel's per-frame travel + blade kerf). By the triangle
            // inequality this contains every prism within sweepBladeRadius of
            // any sub-stepped tether line, so the per-candidate test below
            // never misses one.
            Vector3 mid = (currStart + anchorPos) * 0.5f;
            float queryRadius = Vector3.Distance(currStart, anchorPos) * 0.5f + sweepBladeRadius + moved;
            index.QuerySphere(mid, queryRadius, sweepScratch);
            if (sweepScratch.Count == 0) return;

            float bladeSq = sweepBladeRadius * sweepBladeRadius;
            bool killedSomething = false;

            for (int i = 0; i < sweepScratch.Count; i++)
            {
                var prism = sweepScratch[i];
                if (prism == null || prism.destroyed) continue;

                var t = prism.transform;
                if (t == tether.anchor || t == otherAnchor) continue;

                // Sliced if within the blade kerf of any sub-stepped tether line this frame.
                Vector3 p = t.position;
                bool sliced = false;
                for (int s = 1; s <= steps && !sliced; s++)
                {
                    Vector3 origin = steps == 1 ? currStart : Vector3.Lerp(prevPos, currStart, (float)s / steps);
                    if (PointToSegmentDistanceSq(p, origin, anchorPos) <= bladeSq)
                        sliced = true;
                }
                if (!sliced) continue;

                prism.Damage(lastVelocity, VesselStatus.Domain, VesselStatus.PlayerName);
                killedSomething = true;
            }

            if (killedSomething)
            {
                tether.sweepPulse = 1f;

                // Kill tick — a slice should be felt, not just seen. Shake's
                // stronger-wins rule keeps simultaneous dual-tether kills sane.
                if (sweepKillShakeIntensity > 0f && IsLocalHumanVessel)
                {
                    var cameraController = GetCameraController();
                    if (cameraController != null)
                        cameraController.Shake(sweepKillShakeIntensity, sweepKillShakeDuration);
                }
            }
        }

        /// <summary>Squared distance from point p to segment [a,b].</summary>
        static float PointToSegmentDistanceSq(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 1e-6f) return (p - a).sqrMagnitude;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq);
            Vector3 proj = a + t * ab;
            return (p - proj).sqrMagnitude;
        }

        // ==================================================================
        //  VISUALS
        // ==================================================================

        void UpdateSpinneretArms()
        {
            UpdateArmVisual(leftArm, leftArmRenderer, leftTether, true);
            UpdateArmVisual(rightArm, rightArmRenderer, rightTether, false);
        }

        void UpdateArmVisual(Transform arm, MeshRenderer renderer, TetherState tether, bool isLeft)
        {
            if (arm == null) return;

            Vector3 start = transform.position;
            Vector3 tip = GetArmTipPosition(tether, isLeft);
            float distance = Vector3.Distance(start, tip);

            if (distance < 0.01f)
            {
                renderer.enabled = false;
                return;
            }

            renderer.enabled = true;
            arm.position = (start + tip) * 0.5f;
            arm.rotation = Quaternion.FromToRotation(Vector3.up, (tip - start) / distance);
            arm.localScale = new Vector3(armRadius * 2f, distance * 0.5f, armRadius * 2f);
        }

        void UpdateTetherVisual(ref TetherState tether, bool isLeft)
        {
            tether.sweepPulse = Mathf.MoveTowards(tether.sweepPulse, 0f, sweepPulseDecay * Time.deltaTime);

            Vector3 start = GetArmTipPosition(tether, isLeft);
            Vector3 end;

            if (tether.isAnchored && tether.anchor != null)
                end = tether.anchor.position;
            else if (tether.isFiring)
                end = tether.fireOrigin + tether.fireDirection * tether.extension;
            else if (tether.isRetracting)
            {
                tether.retractTimer -= Time.deltaTime;
                if (tether.retractTimer <= 0f)
                {
                    tether.isRetracting = false;
                    tether.capsuleRenderer.enabled = false;
                    return;
                }
                // Far end slides from its release point back to the arm tip.
                float t = tetherRetractDuration > 0f ? tether.retractTimer / tetherRetractDuration : 0f;
                end = Vector3.Lerp(start, tether.retractEnd, t);
            }
            else
            {
                tether.capsuleRenderer.enabled = false;
                return;
            }

            float distance = Vector3.Distance(start, end);
            if (distance < 0.01f)
            {
                tether.capsuleRenderer.enabled = false;
                return;
            }

            // Lightsaber heat: thicker at speed, pulse on a kill
            float speedT = Mathf.Clamp01(speed / Mathf.Max(speedThicknessRef, 1f));
            float radius = tetherRadius
                         * Mathf.Lerp(1f, speedThicknessBoost, speedT)
                         * (1f + 0.75f * tether.sweepPulse);

            tether.capsuleRenderer.enabled = true;
            tether.capsule.position = (start + end) * 0.5f;
            tether.capsule.rotation = Quaternion.FromToRotation(Vector3.up, (end - start) / distance);
            tether.capsule.localScale = new Vector3(radius * 2f, distance * 0.5f, radius * 2f);
        }

        // ==================================================================
        //  MOVEMENT OVERRIDE
        // ==================================================================

        protected override void MoveShip()
        {
            switch (currentState)
            {
                case SwingState.FreeFlight:    FreeFlightMove();    break;
                case SwingState.SingleAnchor:  SingleAnchorMove();  break;
                case SwingState.DualAnchor:    DualAnchorMove();    break;
            }
        }

        // ---- Free flight: coast on momentum, no throttle ----

        void FreeFlightMove()
        {
            if (VesselStatus == null) return;

            float dt = Time.deltaTime;

            // Quadratic drag — a hard launch coasts down gracefully instead
            // of keeping its release speed forever (1/v − 1/v₀ = k·t, so
            // 150→75 in ~4s at k=0.0017). The MinimumSpeed floor below still
            // guarantees the vessel can never strand.
            speed = Mathf.Max(0f, speed - freeFlightDragK * speed * speed * dt);

            // Air control — steer the coast course toward the nose at a
            // constant rate (RotateTowards, not Slerp, so authority doesn't
            // fade with angle). This is the way back after overshooting
            // every prism in range.
            if (airControlDegPerSec > 0f)
                freeFlightCourse = Vector3.RotateTowards(
                    freeFlightCourse, transform.forward,
                    airControlDegPerSec * Mathf.Deg2Rad * dt, 0f);

            // MinimumSpeed is a stranding floor, not a throttle — tune to 0
            // on the prefab to disable. Boost pickups still apply.
            float effectiveSpeed = Mathf.Max(speed, MinimumSpeed) * throttleMultiplier;
            if (VesselStatus.IsBoosting)
                effectiveSpeed *= VesselStatus.BoostMultiplier;
            if (VesselStatus.IsChargedBoostDischarging)
                effectiveSpeed *= VesselStatus.ChargedBoostCharge;

            VesselStatus.Speed = effectiveSpeed;
            VesselStatus.Course = freeFlightCourse;

            transform.position += (effectiveSpeed * freeFlightCourse + velocityShift) * dt;
            lastVelocity = effectiveSpeed * freeFlightCourse;
        }

        // ---- Single anchor: redirect on the sphere, bleed under drag ----

        void SingleAnchorMove()
        {
            if (VesselStatus == null) return;

            bool isLeft = LeftIsActiveAnchor();
            TetherState anchored = isLeft ? leftTether : rightTether;

            if (anchored.anchor == null)
            {
                FreeFlightMove();
                return;
            }

            Vector3 anchorPos = anchored.anchor.position;
            float radius = anchored.ropeLength;
            Vector3 toVessel = transform.position - anchorPos;
            float dist = toVessel.magnitude;
            Vector3 radial = dist > 0.01f ? toVessel / dist : Vector3.forward;

            // Project vessel forward onto the tangent plane
            Vector3 projForward = transform.forward - Vector3.Dot(transform.forward, radial) * radial;
            if (projForward.sqrMagnitude > 0.001f)
                projForward.Normalize();
            else
                projForward = sphereCourse;

            // Course lerps toward projected forward (drift feel)
            sphereCourse = Vector3.Slerp(sphereCourse, projForward, courseLerp * Time.deltaTime);

            // Re-project for numerical safety
            sphereCourse -= Vector3.Dot(sphereCourse, radial) * radial;
            if (sphereCourse.sqrMagnitude > 0.001f)
                sphereCourse.Normalize();
            else
                sphereCourse = projForward;

            // Quadratic drag while anchored (dv/dt = −k·v²) — a single-tether
            // redirect is no longer a lossless momentum store; carry the arc,
            // don't park in it. Floored so a stalled spider can still swing out.
            speed = Mathf.Max(0f, speed - swingDragK * speed * speed * Time.deltaTime);

            float effectiveSpeed = Mathf.Max(speed, MinimumSpeed) * throttleMultiplier;
            if (VesselStatus.IsBoosting) effectiveSpeed *= VesselStatus.BoostMultiplier;
            if (VesselStatus.IsChargedBoostDischarging) effectiveSpeed *= VesselStatus.ChargedBoostCharge;

            Vector3 prevPos = transform.position;
            Vector3 newPos = transform.position + sphereCourse * effectiveSpeed * Time.deltaTime;

            // Snap back onto the sphere
            Vector3 newRadial = newPos - anchorPos;
            if (newRadial.sqrMagnitude > 0.001f)
                newRadial.Normalize();
            else
                newRadial = radial;
            transform.position = anchorPos + newRadial * radius;

            Vector3 displacement = transform.position - prevPos;
            lastVelocity = Time.deltaTime > 0.0001f ? displacement / Time.deltaTime : Vector3.zero;

            VesselStatus.Speed = lastVelocity.magnitude;
            VesselStatus.Course = sphereCourse;
            // speed field only decays (swing drag above) — a single tether
            // redirects momentum and bleeds it, never adds to it
        }

        // ---- Dual anchor: momentum conservation + pump ± dissipation ----
        //
        // L = ω·h² is conserved. Shrinking radius → ω ∝ 1/h² → v ∝ 1/h.
        // Active contraction injects energy: L += pumpGain · |dh/dt| · |ω|.
        // Expansion does the mirror-image negative work (reversePumpBleed),
        // and quadratic drag (swingDragK) bleeds L continuously, so speed
        // plateaus at an EMERGENT terminal velocity instead of running away.
        // Pump toward the plateau, release at short radius to fly.

        void DualAnchorMove()
        {
            if (VesselStatus == null) return;

            if (leftTether.anchor == null || rightTether.anchor == null)
            {
                FreeFlightMove();
                return;
            }

            Vector3 a1 = leftTether.anchor.position;
            Vector3 a2 = rightTether.anchor.position;
            float d = Vector3.Distance(a1, a2);

            if (d < 0.01f)
            {
                FreeFlightMove();
                return;
            }

            float dt = Time.deltaTime;

            // XDiff (stick spread) → target radius:
            // both sticks inward → 2× home, neutral → home, both outward → tight
            float xDiff = InputStatus?.XDiff ?? 0.5f;
            float radiusMult = Mathf.Clamp(2f * (1f - xDiff), 0.05f, 2f);
            float targetH = Mathf.Max(dualAnchorHomeH * radiusMult, minCircleRadius);

            float oldH = currentH;
            currentH = Mathf.Lerp(currentH, targetH, tetherLengthLerpSpeed * dt);
            currentH = Mathf.Max(currentH, minCircleRadius);
            float h = currentH;

            // Pump injection: contracting while spinning adds energy, scaled
            // by contraction rate × angular speed — fast spins reward harder.
            float dH = currentH - oldH;
            if (dH < 0f && dt > 0.0001f)
            {
                float contractionRate = Mathf.Abs(dH) / dt;
                float absOmega = Mathf.Abs(circleAngularVelocity);
                float sign = circleAngularVelocity >= 0f ? 1f : -1f;

                if (absOmega < 0.001f) // bootstrap kick so the first pump isn't dead
                    sign = 1f;

                angularMomentum += sign * pumpGain * contractionRate * Mathf.Max(absOmega, 0.1f) * dt;
            }
            else if (dH > 0f && dt > 0.0001f && reversePumpBleed > 0f)
            {
                // Reverse-pump bleed: expansion does negative work, mirroring
                // the injection term above. Spread the sticks to pump up,
                // relax them to bleed down — the skill-based brake. Integrated
                // in closed form (dL/L = −bleed·dh/h² ⇒ L ×= e^(−bleed·(1/h₀−1/h₁)))
                // so the result is framerate-independent: a left-endpoint ω
                // sample over-drains badly when a hitch frame delivers the
                // whole expansion at once (it floored L to zero where the
                // exact integral keeps ~79%).
                angularMomentum *= Mathf.Exp(-reversePumpBleed * (1f / oldH - 1f / currentH));
            }

            // Quadratic drag on L (dL/dt = −k·v·L with v = |ω|·h) — the
            // emergent speed cap: terminal velocity sits where the pump
            // injection above balances this bleed. No hard clamp anywhere.
            float vNow = Mathf.Abs(circleAngularVelocity) * h;
            angularMomentum *= Mathf.Max(0f, 1f - swingDragK * vNow * dt);

            // Constant damping floor (0 = off): guarantees pumping plateaus
            // even if the quadratic drag is tuned near zero.
            if (swingDamp > 0f)
                angularMomentum *= Mathf.Max(0f, 1f - swingDamp * dt);

            // Conservation: ω = L / h²
            circleAngularVelocity = angularMomentum / (h * h);

            // Rope lengths track the new radius around the fixed circle center
            leftTether.ropeLength = Mathf.Sqrt(dualAnchorA * dualAnchorA + h * h);
            float dMinusA = d - dualAnchorA;
            rightTether.ropeLength = Mathf.Sqrt(dMinusA * dMinusA + h * h);

            if (d > leftTether.ropeLength + rightTether.ropeLength)
            {
                FreeFlightMove();
                return;
            }

            Vector3 axis = (a2 - a1).normalized;
            Vector3 center = a1 + axis * dualAnchorA;

            Vector3 u = Vector3.Cross(axis, Vector3.up).normalized;
            if (u.sqrMagnitude < 0.01f)
                u = Vector3.Cross(axis, Vector3.forward).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;

            circleAngle += circleAngularVelocity * dt;

            transform.position = center + h * (Mathf.Cos(circleAngle) * u + Mathf.Sin(circleAngle) * v);

            Vector3 tangent = (-Mathf.Sin(circleAngle) * u + Mathf.Cos(circleAngle) * v).normalized;
            if (circleAngularVelocity < 0f) tangent = -tangent;

            // True tangential speed |ω|·h, NOT the per-frame chord. The chord
            // aliases at high ω·dt (v·sin(ωdt/2)/(ωdt/2)) — identical swings
            // released at wildly different speeds depending on framerate
            // (a 30fps hard-pump launched at ~25% of a 144fps one).
            speed = Mathf.Abs(circleAngularVelocity) * h;
            lastVelocity = speed * tangent;

            VesselStatus.Speed = speed;
            VesselStatus.Course = tangent;
        }

        // ==================================================================
        //  HELPERS
        // ==================================================================

        bool LeftIsActiveAnchor() => leftTether.isAnchored && leftTether.triggerHeld;

        /// <summary>Juice gate: only the local human's spider shakes the local camera.</summary>
        bool IsLocalHumanVessel =>
            VesselStatus is { IsLocalUser: true, IsInitializedAsAI: false };

        /// <summary>
        /// Camera kick on release, scaled by launch speed — a max-speed
        /// slingshot lands a real hit, a gentle detach barely registers.
        /// </summary>
        void PlayReleaseShake()
        {
            if (releaseShakeIntensity <= 0f || !IsLocalHumanVessel) return;

            float speedT = Mathf.Clamp01(speed / Mathf.Max(speedThicknessRef, 1f));
            if (speedT < releaseShakeSpeedFloor) return;

            var cameraController = GetCameraController();
            if (cameraController != null)
                cameraController.Shake(releaseShakeIntensity * speedT, releaseShakeDuration);
        }

        void InitSphereCourseFromCurrentState()
        {
            Transform anchor = LeftIsActiveAnchor() ? leftTether.anchor
                : (rightTether.isAnchored && rightTether.triggerHeld ? rightTether.anchor : null);

            if (anchor == null) return;

            Vector3 toVessel = transform.position - anchor.position;
            float dist = toVessel.magnitude;
            Vector3 radial = dist > 0.01f ? toVessel / dist : Vector3.forward;

            // Snap: project the current course onto the tangent plane
            Vector3 currentCourse = VesselStatus != null ? VesselStatus.Course : transform.forward;
            sphereCourse = currentCourse - Vector3.Dot(currentCourse, radial) * radial;

            if (sphereCourse.sqrMagnitude > 0.001f)
            {
                sphereCourse.Normalize();
                return;
            }

            sphereCourse = transform.forward - Vector3.Dot(transform.forward, radial) * radial;
            if (sphereCourse.sqrMagnitude > 0.001f)
                sphereCourse.Normalize();
            else
                sphereCourse = Vector3.Cross(radial, Vector3.up).normalized;
        }

        void InitDualAnchorFromCurrentState()
        {
            if (leftTether.anchor == null || rightTether.anchor == null) return;

            Vector3 a1 = leftTether.anchor.position;
            Vector3 a2 = rightTether.anchor.position;
            float d = Vector3.Distance(a1, a2);
            if (d < 0.01f) return;

            float r1 = leftTether.ropeLength;
            float r2 = rightTether.ropeLength;

            dualAnchorA = (r1 * r1 - r2 * r2 + d * d) / (2f * d);
            float hSq = Mathf.Max(r1 * r1 - dualAnchorA * dualAnchorA, 0f);
            dualAnchorHomeH = Mathf.Max(Mathf.Sqrt(hSq), minCircleRadius);
            currentH = dualAnchorHomeH;

            Vector3 axis = (a2 - a1).normalized;
            Vector3 center = a1 + axis * dualAnchorA;

            Vector3 u = Vector3.Cross(axis, Vector3.up).normalized;
            if (u.sqrMagnitude < 0.01f)
                u = Vector3.Cross(axis, Vector3.forward).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;

            Vector3 offset = transform.position - center;
            circleAngle = Mathf.Atan2(Vector3.Dot(offset, v), Vector3.Dot(offset, u));

            Vector3 tangent = (-Mathf.Sin(circleAngle) * u + Mathf.Cos(circleAngle) * v).normalized;
            float h = currentH;

            // Seed ω from the persistent momentum along the tangent —
            // direction from the actual motion, magnitude from the honest
            // speed field. lastVelocity's magnitude carries transient output
            // multipliers (boost, MinimumSpeed floor); letting those convert
            // into swing momentum at anchor time compounds across
            // boost-anchor-release cycles. Boost repositions in free flight;
            // it never buys L.
            Vector3 velDir = lastVelocity.sqrMagnitude > 0.0001f
                ? lastVelocity.normalized
                : transform.forward;
            circleAngularVelocity = speed * Vector3.Dot(velDir, tangent) / h;

            angularMomentum = circleAngularVelocity * h * h;
        }

        Transform SpawnAnchorPrism(Vector3 position)
        {
            if (prismSpawnChannel == null)
            {
                Debug.LogWarning("[SwingingVesselTransformer] No prism spawn channel assigned.");
                return null;
            }

            var ret = prismSpawnChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = VesselStatus.Domain,
                Rotation = Quaternion.identity,
                SpawnPosition = position,
                Scale = anchorPrismScale,
                PrismType = PrismType.Spider
            });

            if (ret.SpawnedObject == null)
            {
                Debug.LogWarning("[SwingingVesselTransformer] Failed to spawn anchor prism.");
                return null;
            }

            if (ret.SpawnedObject.TryGetComponent(out Prism prism))
            {
                prism.TargetScale = anchorPrismScale;
                prism.ChangeTeam(VesselStatus.Domain);
                prism.Initialize(VesselStatus.PlayerName);
            }

            return ret.SpawnedObject.transform;
        }
    }
}
