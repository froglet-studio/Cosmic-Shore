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
    ///   momentum without changing it. Free flight coasts.
    ///
    /// Controls:
    ///   Each stick's X positions that side's cursor on the horizontal screen
    ///   axis through the vessel: inward = at vessel, outward = screen edge.
    ///   Pulling a trigger fires that side's tether from the cursor, straight
    ///   into the scene along the camera ray. Hit a prism → anchor. Max range
    ///   → spawn an anchor prism and latch on.
    ///
    ///   One anchor:  vessel is constrained to the anchor sphere. Forward is
    ///   projected onto the tangent plane; course lerps toward it (drift feel).
    ///   Speed is preserved — single tether only redirects.
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
        [SerializeField] float tetherRadius = 0.08f;
        [SerializeField] Material tetherMaterial;

        [Header("Spinneret Arms")]
        [Tooltip("Visual thickness of the arm capsule.")]
        [SerializeField] float armRadius = 0.04f;

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

        [Header("Lightsaber Sweep")]
        [Tooltip("Anchored tethers destroy every prism they sweep through (except the anchors).")]
        [SerializeField] bool sweepDestroysPrisms = true;
        [Tooltip("Max world-distance the vessel can move between sweep sub-casts. Lower = denser coverage at high speed.")]
        [SerializeField] float sweepStepDistance = 4f;
        [Tooltip("How fast the kill-pulse on the tether visual decays.")]
        [SerializeField] float sweepPulseDecay = 4f;

        [Header("Speed Feel")]
        [Tooltip("Tether visual thickens up to this multiplier as speed rises (lightsaber heat).")]
        [SerializeField] float speedThicknessBoost = 1.5f;
        [Tooltip("Speed at which the thickness boost saturates.")]
        [SerializeField] float speedThicknessRef = 80f;

        [Header("Anchor Prism")]
        [SerializeField] Vector3 anchorPrismScale = new(6f, 6f, 6f);
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnChannel;

        // ---- Internal types ----

        enum SwingState { FreeFlight, SingleAnchor, DualAnchor }

        struct TetherState
        {
            public bool triggerHeld;
            public bool isAnchored;
            public bool isFiring;
            public Transform anchor;
            public float ropeLength;
            public float extension;
            public Vector3 fireOrigin;
            public Vector3 fireDirection;
            public Transform capsule;
            public MeshRenderer capsuleRenderer;
            public float sweepPulse;
        }

        // ---- Public API ----

        /// <summary>True when the vessel is attached to at least one anchor.</summary>
        public bool IsSwinging => currentState != SwingState.FreeFlight;

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
        static readonly RaycastHit[] sweepHits = new RaycastHit[32];

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

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");
            if (shader == null) return;

            sharedTetherMaterial = new Material(shader);
            // HDR cyan — blooms under URP post-processing for the lightsaber read
            var hot = new Color(0f, 2f, 2f, 1f);
            sharedTetherMaterial.color = hot;
            sharedTetherMaterial.SetColor("_BaseColor", hot);
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
        //  CURSOR POSITIONING (per-stick, horizontal screen axis)
        // ==================================================================

        Camera GetGameplayCamera()
        {
            var controller = CameraManager.Instance != null ? CameraManager.Instance.GetActiveController() : null;
            if (controller is CustomCameraController ccc && ccc.Camera != null)
                return ccc.Camera;
            return Camera.main;
        }

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
        /// World-space cursor position for a given side, pinned to the
        /// horizontal screen axis through the vessel. The matching stick's X
        /// slides it between the vessel (inward) and the screen edge (outward).
        /// </summary>
        Vector3 GetCursorWorldPosition(bool left)
        {
            var cam = GetGameplayCamera();
            float travel = GetCursorTravel(left);

            if (cam == null)
                return transform.position + (left ? -transform.right : transform.right) * (20f * travel);

            Vector3 vesselScreen = cam.WorldToScreenPoint(transform.position);
            if (vesselScreen.z <= 0f) // vessel behind camera — projection is garbage
                return transform.position + (left ? -transform.right : transform.right) * (20f * travel);

            float edgeX = left ? 0f : Screen.width;
            float cursorX = Mathf.Lerp(vesselScreen.x, edgeX, travel);
            return cam.ScreenToWorldPoint(new Vector3(cursorX, vesselScreen.y, vesselScreen.z));
        }

        /// <summary>
        /// Spinneret arm tip position and the aim target. The tether fires
        /// from the cursor straight into the scene along the camera ray
        /// (cursor − camera), so what you see is exactly where it goes.
        /// </summary>
        (Vector3 tipPos, Vector3 aimTarget) GetSpinneretAim(bool left)
        {
            Vector3 tip = GetCursorWorldPosition(left);

            var cam = GetGameplayCamera();
            if (cam == null)
                return (tip, tip + transform.forward * maxTetherLength);

            Vector3 fireDir = (tip - cam.transform.position).normalized;

            int layerMask = 1 << TrailBlocksLayer;
            if (Physics.Raycast(tip, fireDir, out var hit, maxTetherLength, layerMask))
                return (tip, hit.point);
            return (tip, tip + fireDir * maxTetherLength);
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
            tether.extension = 0f;
            tether.fireOrigin = tipPosition;
            Vector3 dir = aimTarget - tipPosition;
            tether.fireDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            tether.capsuleRenderer.enabled = true;
        }

        void ReleaseTether(ref TetherState tether)
        {
            tether.isAnchored = false;
            tether.isFiring = false;
            tether.anchor = null;
            tether.sweepPulse = 0f;
            tether.capsuleRenderer.enabled = false;
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
                    {
                        freeFlightCourse = lastVelocity.normalized;
                        speed = lastVelocity.magnitude;
                    }
                    VesselStatus.Course = freeFlightCourse;
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

            if (tether.extension >= maxTetherLength)
            {
                Vector3 spawnPos = tether.fireOrigin + tether.fireDirection * maxTetherLength;
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
        /// While anchored, the taut tether destroys every prism it passes
        /// through — except the anchors themselves. The vessel's movement this
        /// frame is sub-sampled so fast swings don't skip prisms between
        /// frames: the tether is cast from interpolated vessel positions to
        /// the anchor.
        /// </summary>
        void SweepTether(ref TetherState tether, Vector3 prevPos)
        {
            if (!tether.isAnchored || tether.anchor == null) return;

            Vector3 anchorPos = tether.anchor.position;
            float moved = Vector3.Distance(prevPos, transform.position);
            int steps = Mathf.Clamp(Mathf.CeilToInt(moved / Mathf.Max(sweepStepDistance, 0.5f)), 1, 4);

            Transform otherAnchor = (tether.anchor == leftTether.anchor) ? rightTether.anchor : leftTether.anchor;
            int layerMask = 1 << TrailBlocksLayer;
            bool killedSomething = false;

            for (int s = 1; s <= steps; s++)
            {
                Vector3 origin = Vector3.Lerp(prevPos, transform.position, (float)s / steps);
                Vector3 toAnchor = anchorPos - origin;
                float dist = toAnchor.magnitude;
                if (dist < 0.01f) continue;
                Vector3 dir = toAnchor / dist;

                int hitCount = Physics.RaycastNonAlloc(origin, dir, sweepHits, dist, layerMask);
                for (int i = 0; i < hitCount; i++)
                {
                    var t = sweepHits[i].collider.transform;
                    if (t == tether.anchor || t == otherAnchor) continue;
                    if (!sweepHits[i].collider.TryGetComponent<Prism>(out var prism) || prism.destroyed) continue;

                    prism.Damage(lastVelocity, VesselStatus.Domain, VesselStatus.PlayerName);
                    killedSomething = true;
                }
            }

            if (killedSomething)
                tether.sweepPulse = 1f;
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

            // MinimumSpeed is a stranding floor, not a throttle — tune to 0
            // on the prefab to disable. Boost pickups still apply.
            float effectiveSpeed = Mathf.Max(speed, MinimumSpeed) * throttleMultiplier;
            if (VesselStatus.IsBoosting)
                effectiveSpeed *= VesselStatus.BoostMultiplier;
            if (VesselStatus.IsChargedBoostDischarging)
                effectiveSpeed *= VesselStatus.ChargedBoostCharge;

            VesselStatus.Speed = effectiveSpeed;
            VesselStatus.Course = freeFlightCourse;

            transform.position += (effectiveSpeed * freeFlightCourse + velocityShift) * Time.deltaTime;
            lastVelocity = effectiveSpeed * freeFlightCourse;
        }

        // ---- Single anchor: preserve speed, redirect on the sphere ----

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

            // Speed preserved — floored so a stalled spider can still swing out
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
            // speed field intentionally untouched — single tether never changes it
        }

        // ---- Dual anchor: angular momentum conservation + pump injection ----
        //
        // L = ω·h² is conserved. Shrinking radius → ω ∝ 1/h² → v ∝ 1/h.
        // Active contraction injects energy: L += pumpGain · |dh/dt| · |ω|.
        // Each pump cycle ratchets L upward. Release at short radius to fly.

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

            Vector3 prevPos = transform.position;
            transform.position = center + h * (Mathf.Cos(circleAngle) * u + Mathf.Sin(circleAngle) * v);

            Vector3 displacement = transform.position - prevPos;
            lastVelocity = dt > 0.0001f ? displacement / dt : Vector3.zero;
            speed = lastVelocity.magnitude;

            Vector3 tangent = (-Mathf.Sin(circleAngle) * u + Mathf.Cos(circleAngle) * v).normalized;
            if (circleAngularVelocity < 0f) tangent = -tangent;

            VesselStatus.Speed = speed;
            VesselStatus.Course = displacement.sqrMagnitude > 0.001f ? displacement.normalized : tangent;
        }

        // ==================================================================
        //  HELPERS
        // ==================================================================

        bool LeftIsActiveAnchor() => leftTether.isAnchored && leftTether.triggerHeld;

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
            circleAngularVelocity = Vector3.Dot(lastVelocity, tangent) / h;

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
