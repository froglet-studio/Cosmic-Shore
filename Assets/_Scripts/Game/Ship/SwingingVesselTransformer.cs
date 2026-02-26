using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Utility;
using CosmicShore.Utilities;

/// <summary>
/// Spider vessel transformer: dual-tether swinging through the hypersea.
///
/// Speed model:
///   The spider has NO throttle. Speed is purely displacement-based.
///   The only way to gain speed is dual-tether pumping (XDiff changes
///   tether lengths). Single tether redirects momentum without changing
///   speed. Free flight coasts at current velocity.
///
/// Controls overview:
///   Free flight (no triggers held):
///     Sticks control orientation only (pitch/yaw/roll). Vessel coasts
///     at whatever speed it had. Course is decoupled from forward.
///
///   Pull a trigger:
///     Fires a tether from the spinneret arm tip on that side. If it hits
///     a prism, the tether anchors. At max range, a new prism is spawned.
///
///   Hold one trigger (single anchor):
///     Vessel is constrained to the sphere around the anchor. Maintains
///     current speed. Forward is projected onto the tangent plane. Course
///     lerps toward projected forward (drift feel). Only direction changes.
///
///   Hold both triggers (dual anchor):
///     Vessel is on the intersection circle of both spheres. XDiff sets
///     a target circle radius (inward=larger, outward=smaller). Actual
///     radius lerps toward target. Displacement from radius change + angular
///     motion determines speed. This is the ONLY way to change speed.
///
///   Release a trigger:
///     Detach tether, carry velocity into the next state.
///
/// Spinneret arms:
///     World-space arms extend from the vessel toward each side's screen
///     edge. XDiff controls arm length: 0 (inward) = retracted, 0.5 (neutral)
///     = halfway to edge, 1 (outward) = full screen edge. The arm tip IS the
///     tether fire point — no parallax. Arms replace crosshairs entirely.
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
    [Tooltip("How fast the actual tether length lerps toward the target during dual-anchor pumping.")]
    [SerializeField] float tetherLengthLerpSpeed = 3f;

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

    // Dual-anchor geometry
    float dualAnchorA;
    float dualAnchorHomeH;
    float currentH; // actual circle radius, lerps toward XDiff target

    // Momentum tracking across state transitions
    Vector3 lastVelocity;

    // Free-flight course direction — decoupled from transform.forward
    Vector3 freeFlightCourse;

    // Spinneret arm visuals (world-space capsules extending from vessel)
    Transform leftArm;
    MeshRenderer leftArmRenderer;
    Transform rightArm;
    MeshRenderer rightArmRenderer;

    // Deferred anchor spawns
    Vector3? pendingLeftSpawnPos;
    Vector3? pendingRightSpawnPos;

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
    }

    void CreateTetherCapsule(string childName, ref TetherState tether)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = childName;

        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        ApplyTetherMaterial(mr);

        mr.enabled = false;
        tether.capsule = go.transform;
        tether.capsuleRenderer = mr;
    }

    void CreateArmCapsule(string childName, out Transform armTransform, out MeshRenderer armRenderer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = childName;

        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        ApplyTetherMaterial(mr);

        mr.enabled = true;
        armTransform = go.transform;
        armRenderer = mr;
    }

    void ApplyTetherMaterial(MeshRenderer mr)
    {
        if (tetherMaterial != null)
        {
            mr.sharedMaterial = tetherMaterial;
            return;
        }

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            var mat = new Material(shader);
            var bright = new Color(0f, 1f, 1f, 1f);
            mat.color = bright;
            mat.SetColor("_BaseColor", bright);
            mr.material = mat;
        }
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
    //  SPINNERET AIMING
    // ==================================================================

    Camera GetGameplayCamera()
    {
        var controller = CameraManager.Instance?.GetActiveController();
        if (controller is CustomCameraController ccc)
            return ccc.Camera;
        return Camera.main;
    }

    /// <summary>
    /// Computes the world-space half-width of the camera frustum at the vessel's depth.
    /// This is how far (in world units) the screen edge is from the vessel's position
    /// projected onto the camera's horizontal plane.
    /// </summary>
    float GetScreenEdgeDistance()
    {
        var cam = GetGameplayCamera();
        if (cam == null) return 20f; // sane fallback

        float depth = Vector3.Dot(
            transform.position - cam.transform.position,
            cam.transform.forward);
        depth = Mathf.Max(depth, 0.1f);

        if (cam.orthographic)
            return cam.orthographicSize * cam.aspect;

        float halfHeight = depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return halfHeight * cam.aspect;
    }

    /// <summary>
    /// Computes the arm length for a given side based on XDiff.
    /// XDiff controls both arms symmetrically: 0.5 (neutral) = halfway to edge,
    /// 0 (inward) = arms at vessel center, 1 (outward) = arms at screen edge.
    /// </summary>
    float GetArmLength()
    {
        float xDiff = InputStatus?.XDiff ?? 0.5f;
        float edgeDist = GetScreenEdgeDistance();
        // XDiff 0→0, 0.5→half, 1→full edge
        return edgeDist * xDiff;
    }

    /// <summary>
    /// Screen-space aim position for a given side — the arm points from vessel
    /// center toward the screen edge on that side.
    /// </summary>
    Vector3 GetAimScreenPosition(bool left)
    {
        float sh = Screen.height;
        float sw = Screen.width;

        // Arms point toward their respective screen edges
        // Left arm → left edge, right arm → right edge
        float x = left ? 0f : sw;
        return new Vector3(x, sh * 0.5f, 0f);
    }

    /// <summary>
    /// Computes the world-space aim target by raycasting from the gameplay
    /// camera through the screen-edge position on this arm's side.
    /// </summary>
    Vector3 GetAimTarget(bool left)
    {
        var cam = GetGameplayCamera();
        if (cam == null) return transform.position + transform.forward * maxTetherLength;

        Vector3 screenPos = GetAimScreenPosition(left);
        Ray ray = cam.ScreenPointToRay(screenPos);

        int layerMask = 1 << TrailBlocksLayer;
        if (Physics.Raycast(ray, out var hit, maxTetherLength * 2f, layerMask))
            return hit.point;
        return ray.GetPoint(maxTetherLength);
    }

    /// <summary>
    /// Computes the arm direction (unit vector) for a given side.
    /// The arm always points toward its screen edge from the vessel center.
    /// </summary>
    Vector3 GetArmDirection(bool left)
    {
        var cam = GetGameplayCamera();
        if (cam == null) return left ? -transform.right : transform.right;

        Vector3 screenEdge = GetAimScreenPosition(left);
        Ray edgeRay = cam.ScreenPointToRay(screenEdge);

        // Intersect with the plane at the vessel's depth
        Vector3 camFwd = cam.transform.forward;
        float depth = Vector3.Dot(transform.position - cam.transform.position, camFwd);
        if (depth < 0.1f) depth = 0.1f;

        float denom = Vector3.Dot(edgeRay.direction, camFwd);
        if (Mathf.Abs(denom) < 0.0001f) return left ? -transform.right : transform.right;

        float t = depth / denom;
        Vector3 edgeWorldPos = edgeRay.origin + edgeRay.direction * t;

        Vector3 dir = edgeWorldPos - transform.position;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : (left ? -transform.right : transform.right);
    }

    /// <summary>
    /// Computes the spinneret arm tip position and the aim target.
    /// The tip extends from vessel center toward the screen edge, scaled by XDiff.
    /// </summary>
    (Vector3 tipPos, Vector3 aimTarget) GetSpinneretAim(bool left)
    {
        float len = GetArmLength();
        Vector3 dir = GetArmDirection(left);
        Vector3 tip = transform.position + dir * len;

        Vector3 target = GetAimTarget(left);
        return (tip, target);
    }

    /// <summary>
    /// Computes the arm tip position for a given side based on current tether state.
    /// Anchored: points toward anchor. Firing: points in fire direction. Free: points toward screen edge.
    /// Length always controlled by XDiff.
    /// </summary>
    Vector3 GetArmTipPosition(TetherState tether, bool isLeft)
    {
        float len = GetArmLength();

        Vector3 dir;
        if (tether.isAnchored && tether.anchor != null)
            dir = (tether.anchor.position - transform.position).normalized;
        else if (tether.isFiring)
            dir = tether.fireDirection;
        else
            dir = GetArmDirection(isLeft);

        return transform.position + dir * len;
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
        tether.capsuleRenderer.enabled = false;
    }

    // ==================================================================
    //  UPDATE LOOP
    // ==================================================================

    protected override void Update()
    {
        if (VesselStatus == null || VesselStatus.IsStationary)
            return;

        ProcessDeferredSpawn(ref leftTether, ref pendingLeftSpawnPos);
        ProcessDeferredSpawn(ref rightTether, ref pendingRightSpawnPos);

        UpdateFiringTether(ref leftTether, true);
        UpdateFiringTether(ref rightTether, false);
        ValidateAnchor(ref leftTether);
        ValidateAnchor(ref rightTether);

        UpdateSpinneretArms();
        UpdateTetherVisual(ref leftTether, true);
        UpdateTetherVisual(ref rightTether, false);

        DetermineState();

        base.Update();
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
        // Tether visual extends from the arm tip to the anchor/fire endpoint
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

        tether.capsuleRenderer.enabled = true;

        tether.capsule.position = (start + end) * 0.5f;
        tether.capsule.rotation = Quaternion.FromToRotation(Vector3.up, (end - start) / distance);
        tether.capsule.localScale = new Vector3(tetherRadius * 2f, distance * 0.5f, tetherRadius * 2f);
    }

    // ==================================================================
    //  ROTATION OVERRIDE
    // ==================================================================

    protected override void RotateShip()
    {
        // Always use standard two-thumb rotation regardless of swing state.
        base.RotateShip();
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

    // ---- Free flight: coast at current velocity, no throttle ----

    void FreeFlightMove()
    {
        if (VesselStatus == null) return;

        // No throttle, no cruise speed. The spider only coasts.
        // Boost from gameplay interactions (crystals, etc.) still applies.
        float effectiveSpeed = speed * throttleMultiplier;
        if (VesselStatus.IsBoosting)
            effectiveSpeed *= VesselStatus.BoostMultiplier;
        if (VesselStatus.IsChargedBoostDischarging)
            effectiveSpeed *= VesselStatus.ChargedBoostCharge;

        VesselStatus.Speed = effectiveSpeed;
        VesselStatus.Course = freeFlightCourse;

        transform.position += (effectiveSpeed * freeFlightCourse + velocityShift) * Time.deltaTime;
        lastVelocity = effectiveSpeed * freeFlightCourse;
    }

    // ---- Single anchor: maintain speed, redirect on sphere ----
    //
    // Speed doesn't change — only direction. Forward is projected onto the
    // sphere tangent plane. Course lerps toward projected forward (drift feel).

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

        // Project vessel forward onto tangent plane
        Vector3 projForward = transform.forward - Vector3.Dot(transform.forward, radial) * radial;
        if (projForward.sqrMagnitude > 0.001f)
            projForward.Normalize();
        else
            projForward = sphereCourse;

        // Lerp course toward projected forward (drift feel)
        sphereCourse = Vector3.Slerp(sphereCourse, projForward, courseLerp * Time.deltaTime);

        // Re-project onto tangent plane for numerical safety
        sphereCourse = sphereCourse - Vector3.Dot(sphereCourse, radial) * radial;
        if (sphereCourse.sqrMagnitude > 0.001f)
            sphereCourse.Normalize();
        else
            sphereCourse = projForward;

        // Speed is maintained — no throttle. Boost still applies.
        float effectiveSpeed = speed * throttleMultiplier;
        if (VesselStatus.IsBoosting) effectiveSpeed *= VesselStatus.BoostMultiplier;
        if (VesselStatus.IsChargedBoostDischarging) effectiveSpeed *= VesselStatus.ChargedBoostCharge;

        // Move along course on the sphere surface
        Vector3 prevPos = transform.position;
        Vector3 newPos = transform.position + sphereCourse * effectiveSpeed * Time.deltaTime;

        // Re-project onto sphere to maintain constraint
        Vector3 newRadial = newPos - anchorPos;
        if (newRadial.sqrMagnitude > 0.001f)
            newRadial.Normalize();
        else
            newRadial = radial;
        transform.position = anchorPos + newRadial * radius;

        // Track actual velocity for momentum on release
        Vector3 displacement = transform.position - prevPos;
        lastVelocity = Time.deltaTime > 0.0001f ? displacement / Time.deltaTime : Vector3.zero;

        VesselStatus.Speed = lastVelocity.magnitude;
        VesselStatus.Course = sphereCourse;
        // speed stays unchanged — single tether never changes speed
    }

    // ---- Dual anchor: pump tether lengths to gain speed ----
    //
    // XDiff sets target circle radius. Actual radius lerps toward target.
    // The displacement from angular motion + radius change IS the speed.
    // This is the ONLY way the spider changes speed.

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

        // XDiff → target radius: inward(0)→2× home, neutral(0.5)→1× home, outward(1)→~0
        float xDiff = InputStatus?.XDiff ?? 0.5f;
        float radiusMult = Mathf.Clamp(2f * (1f - xDiff), 0.05f, 2f);
        float targetH = dualAnchorHomeH * radiusMult;

        // Lerp actual radius toward target
        float oldH = currentH;
        currentH = Mathf.Lerp(currentH, targetH, tetherLengthLerpSpeed * Time.deltaTime);
        float h = Mathf.Max(currentH, 0.01f);

        // Update rope lengths to maintain circle center while changing radius
        leftTether.ropeLength = Mathf.Sqrt(dualAnchorA * dualAnchorA + h * h);
        float dMinusA = d - dualAnchorA;
        rightTether.ropeLength = Mathf.Sqrt(dMinusA * dMinusA + h * h);

        // Bail if geometry degenerates
        if (d > leftTether.ropeLength + rightTether.ropeLength)
        {
            FreeFlightMove();
            return;
        }

        Vector3 axis = (a2 - a1).normalized;
        Vector3 center = a1 + axis * dualAnchorA;

        // Orthonormal basis for the circle plane
        Vector3 u = Vector3.Cross(axis, Vector3.up).normalized;
        if (u.sqrMagnitude < 0.01f)
            u = Vector3.Cross(axis, Vector3.forward).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;

        // Tangent at current angle on circle
        Vector3 tangent = (-Mathf.Sin(circleAngle) * u + Mathf.Cos(circleAngle) * v).normalized;

        // Project forward onto circle plane (remove axis component)
        Vector3 projForward = transform.forward - Vector3.Dot(transform.forward, axis) * axis;
        if (projForward.sqrMagnitude > 0.001f)
            projForward.Normalize();
        else
            projForward = tangent;

        // Desired angular velocity direction from projected forward
        float desiredDir = Mathf.Sign(Vector3.Dot(projForward, tangent));
        if (Mathf.Abs(Vector3.Dot(projForward, tangent)) < 0.01f)
            desiredDir = Mathf.Sign(circleAngularVelocity);

        // Desired angular velocity preserves current linear speed on the new radius
        float currentLinearSpeed = Mathf.Abs(circleAngularVelocity) * Mathf.Max(oldH, 0.01f);
        float desiredAngVel = desiredDir * currentLinearSpeed / h;

        // Lerp angular velocity toward desired (drift feel)
        circleAngularVelocity = Mathf.Lerp(circleAngularVelocity, desiredAngVel, courseLerp * Time.deltaTime);

        // Advance angle
        circleAngle += circleAngularVelocity * Time.deltaTime;

        // Position on circle
        Vector3 prevPos = transform.position;
        Vector3 targetPos = center + h * (Mathf.Cos(circleAngle) * u + Mathf.Sin(circleAngle) * v);
        transform.position = Vector3.Lerp(prevPos, targetPos, LERP_AMOUNT * Time.deltaTime);

        // Speed is purely displacement-based
        Vector3 displacement = transform.position - prevPos;
        lastVelocity = Time.deltaTime > 0.0001f ? displacement / Time.deltaTime : Vector3.zero;

        // Update speed from actual displacement — this is how the spider gains/loses speed
        speed = lastVelocity.magnitude;

        Vector3 moveTangent = (-Mathf.Sin(circleAngle) * u + Mathf.Cos(circleAngle) * v).normalized;
        if (circleAngularVelocity < 0f) moveTangent = -moveTangent;

        VesselStatus.Speed = speed;
        VesselStatus.Course = displacement.sqrMagnitude > 0.001f ? displacement.normalized : moveTangent;
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

        // Snap: project current course onto tangent plane
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

        // Store home geometry
        dualAnchorA = (r1 * r1 - r2 * r2 + d * d) / (2f * d);
        float hSq = Mathf.Max(r1 * r1 - dualAnchorA * dualAnchorA, 0f);
        dualAnchorHomeH = Mathf.Sqrt(hSq);
        currentH = dualAnchorHomeH;

        // Initialize circle angle from current position
        Vector3 axis = (a2 - a1).normalized;
        Vector3 center = a1 + axis * dualAnchorA;

        Vector3 u = Vector3.Cross(axis, Vector3.up).normalized;
        if (u.sqrMagnitude < 0.01f)
            u = Vector3.Cross(axis, Vector3.forward).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;

        Vector3 offset = transform.position - center;
        circleAngle = Mathf.Atan2(Vector3.Dot(offset, v), Vector3.Dot(offset, u));

        // Initialize angular velocity from current velocity
        Vector3 tangent = (-Mathf.Sin(circleAngle) * u + Mathf.Cos(circleAngle) * v).normalized;
        float h = Mathf.Max(dualAnchorHomeH, 0.01f);
        circleAngularVelocity = Vector3.Dot(lastVelocity, tangent) / h;
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
