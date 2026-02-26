using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Utility;
using CosmicShore.Utilities;

/// <summary>
/// Spider vessel transformer: dual-tether swinging through the hypersea.
///
/// Controls overview:
///   Free flight (no triggers held):
///     Standard two-thumb flying. Speed is a fixed cruise speed. Course
///     (movement direction) is fully decoupled from forward — joystick
///     rotation only changes the vessel's facing direction. Only tethers
///     change course (via swing momentum on release).
///
///   Pull a trigger:
///     Fires a visible tether from that side's cursor. If it hits a prism the tether
///     anchors there. If it reaches max range a new prism is spawned as the anchor.
///
///   Hold one trigger (single anchor):
///     Vessel is constrained to the sphere around the anchor. Pitch, yaw,
///     and roll still rotate the vessel normally. The vessel's forward
///     direction is projected onto the sphere's tangent plane to determine
///     movement direction, with drift-like course lerp for momentum feel.
///
///   Hold both triggers (dual anchor):
///     Vessel is constrained to the intersection circle of both anchor
///     spheres. Same rotation controls. Forward is projected onto the
///     circle tangent. XDiff adjusts tether lengths to change the circle
///     radius (thumbs inward = larger, outward = smaller).
///
///   Release a trigger:
///     Detach tether, carry swing momentum into the next state.
///
///   Crosshairs:
///     Each crosshair defaults to halfway between screen center and the
///     horizontal screen edge. Thumbstick inward moves toward vessel,
///     outward moves toward screen edge.
/// </summary>
public class SwingingVesselTransformer : VesselTransformer
{
    [Header("Tether")]
    [SerializeField] float tetherSpeed = 300f;
    [SerializeField] float maxTetherLength = 150f;
    [SerializeField] float tetherRadius = 0.08f;
    [SerializeField] Material tetherMaterial;

    [Header("Swing")]
    [SerializeField] float swingAngularSpeed = 2.5f;

    [Header("Course Lerp")]
    [Tooltip("How fast the tethered course catches up to the projected forward (drift feel).")]
    [SerializeField] float courseLerp = 1.5f;

    [Header("Free Flight")]
    [SerializeField] float cruiseSpeed = 25f;

    [Header("Crosshair")]
    [SerializeField] float crosshairSize = 32f;
    [SerializeField] float crosshairThickness = 3f;
    [SerializeField] Color crosshairColor = new Color(0f, 1f, 1f, 0.85f);

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

    // Dual-anchor home geometry (stored at transition, radius adjusted by XDiff)
    float dualAnchorA;
    float dualAnchorHomeH;

    // Momentum tracking across state transitions
    Vector3 lastVelocity;

    // Free-flight course direction — fully independent from transform.forward.
    // Only tethers (swing momentum) change this; joystick rotation does not.
    Vector3 freeFlightCourse;

    // Screen-space crosshair UI
    Canvas crosshairCanvas;
    RectTransform leftCrosshair;
    RectTransform rightCrosshair;

    // Deferred anchor spawns (spread prism creation across frames)
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

        CreateCrosshairUI();

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
        if (crosshairCanvas) Destroy(crosshairCanvas.gameObject);
    }

    void CreateTetherCapsule(string childName, ref TetherState tether)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = childName;

        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (tetherMaterial != null)
            mr.sharedMaterial = tetherMaterial;
        else
        {
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

        mr.enabled = false;
        tether.capsule = go.transform;
        tether.capsuleRenderer = mr;
    }

    void CreateCrosshairUI()
    {
        var canvasGo = new GameObject("SpiderCrosshairs");
        crosshairCanvas = canvasGo.AddComponent<Canvas>();
        crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        crosshairCanvas.sortingOrder = 100;
        canvasGo.AddComponent<CanvasScaler>();

        leftCrosshair = CreateCrosshairElement("LeftCrosshair");
        rightCrosshair = CreateCrosshairElement("RightCrosshair");
    }

    RectTransform CreateCrosshairElement(string elementName)
    {
        var go = new GameObject(elementName);
        go.transform.SetParent(crosshairCanvas.transform, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = Vector2.zero;

        CreateCrosshairLine(rect, new Vector2(crosshairSize, crosshairThickness));
        CreateCrosshairLine(rect, new Vector2(crosshairThickness, crosshairSize));
        CreateCrosshairLine(rect, Vector2.one * (crosshairThickness * 2f));

        return rect;
    }

    void CreateCrosshairLine(RectTransform parent, Vector2 size)
    {
        var go = new GameObject("Line");
        go.transform.SetParent(parent, false);

        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = crosshairColor;
        img.raycastTarget = false;
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
                FireTether(ref leftTether, GetCursorDirection(true));
        }
        else if (ie == InputEvents.RightStickAction)
        {
            rightTether.triggerHeld = true;
            if (!rightTether.isAnchored)
                FireTether(ref rightTether, GetCursorDirection(false));
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

    /// <summary>
    /// Screen-space crosshair position for a given side.
    /// Neutral: halfway between screen center and horizontal edge.
    /// Inward (toward center): moves crosshair to vessel (screen center).
    /// Outward (toward edge): moves crosshair to the screen edge.
    /// </summary>
    Vector3 GetCrosshairScreenPosition(bool left)
    {
        float sw = Screen.width;
        float sh = Screen.height;

        if (left)
        {
            // Left stick: X in [-1, 1], -1 = outward (left edge), +1 = inward (center)
            float stickX = InputStatus?.EasedLeftJoystickPosition.x ?? 0f;
            // Map [-1,1] → [0, sw*0.5]: outward=-1→0, neutral=0→sw*0.25, inward=+1→sw*0.5
            float x = sw * 0.25f * (stickX + 1f);
            return new Vector3(x, sh * 0.5f, 0f);
        }
        else
        {
            // Right stick: X in [-1, 1], -1 = inward (center), +1 = outward (right edge)
            float stickX = InputStatus?.EasedRightJoystickPosition.x ?? 0f;
            // Map [-1,1] → [sw*0.5, sw]: inward=-1→sw*0.5, neutral=0→sw*0.75, outward=+1→sw
            float x = sw * (0.75f + 0.25f * stickX);
            return new Vector3(x, sh * 0.5f, 0f);
        }
    }

    /// <summary>
    /// Returns the active gameplay camera from CameraManager.
    /// Falls back to Camera.main if the manager isn't available.
    /// </summary>
    Camera GetGameplayCamera()
    {
        var controller = CameraManager.Instance?.GetActiveController();
        if (controller is CustomCameraController ccc)
            return ccc.Camera;
        return Camera.main;
    }

    /// <summary>
    /// World-space direction for tether firing based on crosshair screen position.
    /// Raycasts from the gameplay camera through the crosshair into the scene so
    /// the tether aims at whatever prism is under the crosshair.
    /// Falls back to a far point on the ray if nothing is hit.
    /// </summary>
    Vector3 GetCursorDirection(bool left)
    {
        var cam = GetGameplayCamera();
        if (cam == null) return transform.forward;

        Vector3 screenPos = GetCrosshairScreenPosition(left);
        Ray ray = cam.ScreenPointToRay(screenPos);

        // Raycast through the crosshair — TrailBlocks layer contains prisms
        Vector3 worldTarget;
        int layerMask = 1 << TrailBlocksLayer;
        if (Physics.Raycast(ray, out var hit, maxTetherLength * 2f, layerMask))
            worldTarget = hit.point;
        else
            worldTarget = ray.GetPoint(maxTetherLength);

        Vector3 dir = worldTarget - transform.position;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
    }

    void FireTether(ref TetherState tether, Vector3 direction)
    {
        tether.isFiring = true;
        tether.extension = 0f;
        tether.fireOrigin = transform.position;
        tether.fireDirection = direction.normalized;
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
        UpdateTetherVisual(ref leftTether);
        UpdateTetherVisual(ref rightTether);

        UpdateCursors();

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

    void UpdateTetherVisual(ref TetherState tether)
    {
        Vector3 start = transform.position;
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

    void UpdateCursors()
    {
        if (crosshairCanvas == null) return;

        bool showLeft = !leftTether.isFiring && !leftTether.isAnchored;
        bool showRight = !rightTether.isFiring && !rightTether.isAnchored;

        if (leftCrosshair) leftCrosshair.gameObject.SetActive(showLeft);
        if (rightCrosshair) rightCrosshair.gameObject.SetActive(showRight);

        if (showLeft && leftCrosshair)
            leftCrosshair.position = GetCrosshairScreenPosition(true);

        if (showRight && rightCrosshair)
            rightCrosshair.position = GetCrosshairScreenPosition(false);
    }

    // ==================================================================
    //  ROTATION OVERRIDE
    // ==================================================================

    protected override void RotateShip()
    {
        // Always use standard two-thumb rotation regardless of swing state.
        // Pitch, yaw, and roll control vessel orientation in all modes.
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

    // ---- Free flight: course persists from swing momentum ----

    void FreeFlightMove()
    {
        if (VesselStatus == null || InputStatus == null) return;

        float boostAmount = 1f;
        if (VesselStatus.IsBoosting)
            boostAmount = VesselStatus.BoostMultiplier;
        if (VesselStatus.IsChargedBoostDischarging)
            boostAmount *= VesselStatus.ChargedBoostCharge;

        speed = Mathf.Lerp(speed, cruiseSpeed * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);
        speed *= throttleMultiplier;

        VesselStatus.Speed = speed;
        VesselStatus.Course = freeFlightCourse;

        transform.position += (speed * freeFlightCourse + velocityShift) * Time.deltaTime;
        lastVelocity = speed * freeFlightCourse;
    }

    // ---- Single anchor: sphere-projected flight ----
    //
    // The vessel is constrained to the sphere around the anchor.
    // Rotation controls work normally (pitch/yaw/roll via base.RotateShip).
    // The vessel's forward direction is projected onto the sphere's tangent
    // plane. Course snaps to the projected course on tether attach, then
    // lerps toward the projected forward each frame (drift feel).

    void SingleAnchorMove()
    {
        if (InputStatus == null || VesselStatus == null) return;

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

        // Project vessel forward onto tangent plane of sphere at current position
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

        // Speed calculation (same as free flight)
        float boostAmount = 1f;
        if (VesselStatus.IsBoosting) boostAmount = VesselStatus.BoostMultiplier;
        if (VesselStatus.IsChargedBoostDischarging) boostAmount *= VesselStatus.ChargedBoostCharge;
        speed = Mathf.Lerp(speed, cruiseSpeed * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);
        speed *= throttleMultiplier;

        // Move along course on the sphere surface
        Vector3 prevPos = transform.position;
        Vector3 newPos = transform.position + sphereCourse * speed * Time.deltaTime;

        // Re-project onto sphere to maintain constraint
        Vector3 newRadial = newPos - anchorPos;
        if (newRadial.sqrMagnitude > 0.001f)
            newRadial.Normalize();
        else
            newRadial = radial;
        transform.position = anchorPos + newRadial * radius;

        // Velocity tracking for momentum on release
        Vector3 displacement = transform.position - prevPos;
        lastVelocity = Time.deltaTime > 0.0001f ? displacement / Time.deltaTime : Vector3.zero;

        VesselStatus.Speed = lastVelocity.magnitude;
        VesselStatus.Course = sphereCourse;
        speed = VesselStatus.Speed;
    }

    // ---- Dual anchor: intersection-circle projected flight ----
    //
    // The vessel is constrained to the intersection circle of both tether
    // spheres. Rotation controls work normally. Forward is projected onto
    // the circle tangent. XDiff adjusts the circle radius by changing both
    // tether lengths while keeping the circle center fixed:
    //   Inward (XDiff→0)  = radius up to ~2× home
    //   Outward (XDiff→1) = radius toward zero

    void DualAnchorMove()
    {
        if (InputStatus == null || VesselStatus == null) return;

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

        // XDiff adjusts circle radius: inward(0)→2× home, neutral(0.5)→1× home, outward(1)→~0
        float xDiff = InputStatus.XDiff;
        float radiusMult = Mathf.Clamp(2f * (1f - xDiff), 0.05f, 2f);
        float h = dualAnchorHomeH * radiusMult;

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

        // Speed calculation
        float boostAmount = 1f;
        if (VesselStatus.IsBoosting) boostAmount = VesselStatus.BoostMultiplier;
        if (VesselStatus.IsChargedBoostDischarging) boostAmount *= VesselStatus.ChargedBoostCharge;
        speed = Mathf.Lerp(speed, cruiseSpeed * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);
        speed *= throttleMultiplier;

        float desiredAngVel = desiredDir * speed / Mathf.Max(h, 0.01f);

        // Lerp angular velocity toward desired (drift feel)
        circleAngularVelocity = Mathf.Lerp(circleAngularVelocity, desiredAngVel, courseLerp * Time.deltaTime);

        // Advance angle
        circleAngle += circleAngularVelocity * Time.deltaTime;

        // Position on circle
        Vector3 prevPos = transform.position;
        Vector3 targetPos = center + h * (Mathf.Cos(circleAngle) * u + Mathf.Sin(circleAngle) * v);
        transform.position = Vector3.Lerp(prevPos, targetPos, LERP_AMOUNT * Time.deltaTime);

        // Velocity tracking
        Vector3 displacement = transform.position - prevPos;
        lastVelocity = Time.deltaTime > 0.0001f ? displacement / Time.deltaTime : Vector3.zero;

        // Course tangent after movement
        Vector3 moveTangent = (-Mathf.Sin(circleAngle) * u + Mathf.Cos(circleAngle) * v).normalized;
        if (circleAngularVelocity < 0f) moveTangent = -moveTangent;

        VesselStatus.Speed = lastVelocity.magnitude;
        VesselStatus.Course = moveTangent;
        speed = VesselStatus.Speed;
    }

    // ==================================================================
    //  HELPERS
    // ==================================================================

    bool LeftIsActiveAnchor() => leftTether.isAnchored && leftTether.triggerHeld;

    /// <summary>
    /// On transition to SingleAnchor: snap course to the projection of the
    /// current course onto the sphere's tangent plane at the vessel position.
    /// </summary>
    void InitSphereCourseFromCurrentState()
    {
        Transform anchor = LeftIsActiveAnchor() ? leftTether.anchor
            : (rightTether.isAnchored && rightTether.triggerHeld ? rightTether.anchor : null);

        if (anchor == null) return;

        Vector3 toVessel = transform.position - anchor.position;
        float dist = toVessel.magnitude;
        Vector3 radial = dist > 0.01f ? toVessel / dist : Vector3.forward;

        // Project current course onto tangent plane — immediate snap
        Vector3 currentCourse = VesselStatus != null ? VesselStatus.Course : transform.forward;
        sphereCourse = currentCourse - Vector3.Dot(currentCourse, radial) * radial;

        if (sphereCourse.sqrMagnitude > 0.001f)
        {
            sphereCourse.Normalize();
            return;
        }

        // Fallback: project forward
        sphereCourse = transform.forward - Vector3.Dot(transform.forward, radial) * radial;
        if (sphereCourse.sqrMagnitude > 0.001f)
            sphereCourse.Normalize();
        else
            sphereCourse = Vector3.Cross(radial, Vector3.up).normalized;
    }

    /// <summary>
    /// On transition to DualAnchor: compute home geometry and initialize
    /// circle angle and angular velocity from current state.
    /// </summary>
    void InitDualAnchorFromCurrentState()
    {
        if (leftTether.anchor == null || rightTether.anchor == null) return;

        Vector3 a1 = leftTether.anchor.position;
        Vector3 a2 = rightTether.anchor.position;
        float d = Vector3.Distance(a1, a2);
        if (d < 0.01f) return;

        float r1 = leftTether.ropeLength;
        float r2 = rightTether.ropeLength;

        // Store home geometry so XDiff can adjust radius while keeping center fixed
        dualAnchorA = (r1 * r1 - r2 * r2 + d * d) / (2f * d);
        float hSq = Mathf.Max(r1 * r1 - dualAnchorA * dualAnchorA, 0f);
        dualAnchorHomeH = Mathf.Sqrt(hSq);

        // Initialize circle angle from current position
        Vector3 axis = (a2 - a1).normalized;
        Vector3 center = a1 + axis * dualAnchorA;

        Vector3 u = Vector3.Cross(axis, Vector3.up).normalized;
        if (u.sqrMagnitude < 0.01f)
            u = Vector3.Cross(axis, Vector3.forward).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;

        Vector3 offset = transform.position - center;
        circleAngle = Mathf.Atan2(Vector3.Dot(offset, v), Vector3.Dot(offset, u));

        // Initialize angular velocity from current velocity projected onto tangent
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
