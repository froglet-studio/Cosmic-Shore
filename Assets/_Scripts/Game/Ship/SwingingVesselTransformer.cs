using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;
using CosmicShore.Utilities;

/// <summary>
/// Spider vessel transformer: dual-tether swinging through the hypersea.
///
/// Controls overview:
///   Free flight (no triggers held):
///     Standard two-thumb flying. XDiff spreads cursors instead of throttle.
///     Speed is a fixed cruise speed. Course (movement direction) is fully
///     decoupled from forward — joystick rotation only changes the vessel's
///     facing direction, not its trajectory. Only tethers change course
///     (via swing momentum on release).
///
///   Pull a trigger:
///     Fires a visible tether from that side's cursor. If it hits a prism the tether
///     anchors there. If it reaches max range a new prism is spawned as the anchor.
///
///   Hold one trigger (single anchor):
///     That stick's two axes control position on the sphere surrounding the anchor
///     (azimuth + elevation). The other stick controls the cursor for a second tether.
///
///   Hold both triggers (dual anchor):
///     Vessel is constrained to the intersection circle of the two anchor spheres.
///     YSum controls position along the circle. Vessel reorients so its up-forward
///     plane aligns with the circle plane.
///
///   Release a trigger:
///     Detach tether, carry swing momentum into the next state.
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
    [SerializeField] float cursorSpreadMax = 30f;

    [Header("Free Flight")]
    [SerializeField] float cruiseSpeed = 25f;

    [Header("Crosshair")]
    [SerializeField] float cursorDistance = 8f;
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
    public void StartSwing()
    {
        // Swing is input-driven; nothing extra needed on start.
    }

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

    // Sphere navigation (single anchor)
    float swingTheta;
    float swingPhi;

    // Circle navigation (dual anchor)
    float circleAngle;

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

        // Initialize course to forward so there is a sane default.
        // freeFlightCourse is the authoritative direction during free flight —
        // fully decoupled from transform.forward so rotation doesn't steer.
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

        // Remove collider — tether is visual only
        var col = go.GetComponent<Collider>();
        if (col) Destroy(col);

        var mr = go.GetComponent<MeshRenderer>();
        if (tetherMaterial != null)
            mr.sharedMaterial = tetherMaterial;
        else
        {
            // Bright unlit default so tethers are clearly visible without a material assigned
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

        // Horizontal bar
        CreateCrosshairLine(rect, new Vector2(crosshairSize, crosshairThickness));
        // Vertical bar
        CreateCrosshairLine(rect, new Vector2(crosshairThickness, crosshairSize));
        // Centre dot
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
    /// Cursor direction for a given side, based on vessel forward + XDiff spread.
    /// </summary>
    Vector3 GetCursorDirection(bool left)
    {
        float xDiff = InputStatus?.XDiff ?? 0.5f;
        float spreadAngle = (xDiff - 0.5f) * 2f * cursorSpreadMax;
        float angle = left ? -spreadAngle : spreadAngle;
        return Quaternion.AngleAxis(angle, transform.up) * transform.forward;
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

        // Process deferred anchor spawns from previous frame
        ProcessDeferredSpawn(ref leftTether, ref pendingLeftSpawnPos);
        ProcessDeferredSpawn(ref rightTether, ref pendingRightSpawnPos);

        // Pre-processing: advance firing tethers, validate anchors, update visuals
        UpdateFiringTether(ref leftTether, true);
        UpdateFiringTether(ref rightTether, false);
        ValidateAnchor(ref leftTether);
        ValidateAnchor(ref rightTether);
        UpdateTetherVisual(ref leftTether);
        UpdateTetherVisual(ref rightTether);

        // Position cursor indicators
        UpdateCursors();

        DetermineState();

        // Base handles: isActive guard, blockRotation, DecayBoost,
        // RotateShip (overridden), modifiers, MoveShip (overridden).
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
                // Carry swing momentum into free-flight course.
                if (lastVelocity.sqrMagnitude > 0.01f)
                {
                    freeFlightCourse = lastVelocity.normalized;
                    speed = lastVelocity.magnitude;
                }
                // Sync VesselStatus.Course for external consumers.
                VesselStatus.Course = freeFlightCourse;
                break;

            case SwingState.SingleAnchor:
                InitSphereAnglesFromCurrentPosition();
                break;

            case SwingState.DualAnchor:
                InitCircleAngleFromCurrentPosition();
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

        // Raycast the newly covered segment
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

        // Reached max range — defer prism spawn to next frame to avoid lag spike
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

        // Position at midpoint, orient Y-axis along tether direction
        tether.capsule.position = (start + end) * 0.5f;
        tether.capsule.rotation = Quaternion.FromToRotation(Vector3.up, (end - start) / distance);

        // Capsule primitive is 2 units tall, 1 unit diameter at scale 1
        tether.capsule.localScale = new Vector3(tetherRadius * 2f, distance * 0.5f, tetherRadius * 2f);
    }

    void UpdateCursors()
    {
        if (crosshairCanvas == null) return;

        // Show each crosshair only while that side has no active tether
        bool showLeft = !leftTether.isFiring && !leftTether.isAnchored;
        bool showRight = !rightTether.isFiring && !rightTether.isAnchored;

        if (leftCrosshair) leftCrosshair.gameObject.SetActive(showLeft);
        if (rightCrosshair) rightCrosshair.gameObject.SetActive(showRight);

        var cam = Camera.main;
        if (cam == null) return;

        if (showLeft && leftCrosshair)
        {
            Vector3 worldPos = transform.position + GetCursorDirection(true) * cursorDistance;
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z > 0f) leftCrosshair.position = new Vector3(sp.x, sp.y, 0f);
        }

        if (showRight && rightCrosshair)
        {
            Vector3 worldPos = transform.position + GetCursorDirection(false) * cursorDistance;
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z > 0f) rightCrosshair.position = new Vector3(sp.x, sp.y, 0f);
        }
    }

    // ==================================================================
    //  ROTATION OVERRIDE
    // ==================================================================

    protected override void RotateShip()
    {
        // During free flight use the standard two-thumb rotation.
        // During swing states rotation is driven by movement direction in MoveShip.
        if (currentState == SwingState.FreeFlight)
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

        // Fixed cruise speed (XDiff drives cursor spread, not throttle)
        speed = Mathf.Lerp(speed, cruiseSpeed * boostAmount + MinimumSpeed, LERP_AMOUNT * Time.deltaTime);
        speed *= throttleMultiplier;

        VesselStatus.Speed = speed;

        // Use freeFlightCourse — fully independent from transform.forward.
        // Only tethers (swing momentum) change this direction.
        // Forward rotation is cosmetic only (controlled by RotateShip).
        VesselStatus.Course = freeFlightCourse;

        transform.position += (speed * freeFlightCourse + velocityShift) * Time.deltaTime;
        lastVelocity = speed * freeFlightCourse;
    }

    // ---- Single anchor: sphere navigation ----

    void SingleAnchorMove()
    {
        if (InputStatus == null) return;

        bool isLeft = LeftIsActiveAnchor();
        TetherState anchored = isLeft ? leftTether : rightTether;

        if (anchored.anchor == null)
        {
            FreeFlightMove();
            return;
        }

        // The anchored side's stick drives sphere angles
        Vector2 stickInput = isLeft
            ? InputStatus.EasedLeftJoystickPosition
            : InputStatus.EasedRightJoystickPosition;

        swingTheta += stickInput.x * swingAngularSpeed * Time.deltaTime;
        swingPhi   += stickInput.y * swingAngularSpeed * Time.deltaTime;
        swingPhi    = Mathf.Clamp(swingPhi, -Mathf.PI * 0.45f, Mathf.PI * 0.45f);

        // Position on sphere
        Vector3 anchorPos = anchored.anchor.position;
        float r = anchored.ropeLength;
        Vector3 offset = new Vector3(
            r * Mathf.Cos(swingPhi) * Mathf.Sin(swingTheta),
            r * Mathf.Sin(swingPhi),
            r * Mathf.Cos(swingPhi) * Mathf.Cos(swingTheta));

        Vector3 prevPos = transform.position;
        transform.position = Vector3.Lerp(prevPos, anchorPos + offset, LERP_AMOUNT * Time.deltaTime);

        // Velocity tracking
        Vector3 displacement = transform.position - prevPos;
        lastVelocity = Time.deltaTime > 0.0001f ? displacement / Time.deltaTime : Vector3.zero;

        // Orient toward movement direction
        Vector3 moveDir = displacement.normalized;
        if (moveDir.sqrMagnitude > 0.001f &&
            SafeLookRotation.TryGet(moveDir, out var rot, this, logError: false))
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, LERP_AMOUNT * Time.deltaTime);
            accumulatedRotation = transform.rotation;
        }

        VesselStatus.Speed = lastVelocity.magnitude;
        VesselStatus.Course = moveDir.sqrMagnitude > 0.001f ? moveDir : transform.forward;
        speed = VesselStatus.Speed;
    }

    // ---- Dual anchor: intersection-circle navigation ----

    void DualAnchorMove()
    {
        if (leftTether.anchor == null || rightTether.anchor == null)
        {
            FreeFlightMove();
            return;
        }

        Vector3 a1 = leftTether.anchor.position;
        Vector3 a2 = rightTether.anchor.position;
        float r1 = leftTether.ropeLength;
        float r2 = rightTether.ropeLength;
        float d  = Vector3.Distance(a1, a2);

        if (d < 0.01f || d > r1 + r2)
        {
            FreeFlightMove();
            return;
        }

        // Circle centre and radius from sphere-sphere intersection
        float a  = (r1 * r1 - r2 * r2 + d * d) / (2f * d);
        float hSq = Mathf.Max(r1 * r1 - a * a, 0f);
        float h  = Mathf.Sqrt(hSq);

        Vector3 axis   = (a2 - a1).normalized;
        Vector3 center = a1 + axis * a;

        // Orthonormal basis for the circle plane
        Vector3 u = Vector3.Cross(axis, Vector3.up).normalized;
        if (u.sqrMagnitude < 0.01f)
            u = Vector3.Cross(axis, Vector3.forward).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;

        // YSum drives position along the circle
        float yInput = InputStatus?.YSum ?? 0f;
        circleAngle += yInput * swingAngularSpeed * Time.deltaTime;

        Vector3 prevPos = transform.position;
        Vector3 targetPos = center + h * (Mathf.Cos(circleAngle) * u + Mathf.Sin(circleAngle) * v);
        transform.position = Vector3.Lerp(prevPos, targetPos, LERP_AMOUNT * Time.deltaTime);

        // Velocity tracking
        Vector3 displacement = transform.position - prevPos;
        lastVelocity = Time.deltaTime > 0.0001f ? displacement / Time.deltaTime : Vector3.zero;

        // Orient: forward tangent to circle, up radial from centre.
        // This puts the up-forward plane in the circle's plane.
        Vector3 tangent = (-Mathf.Sin(circleAngle) * u + Mathf.Cos(circleAngle) * v).normalized;
        if (yInput < 0f) tangent = -tangent;
        Vector3 radial = (transform.position - center).normalized;

        if (tangent.sqrMagnitude > 0.001f &&
            SafeLookRotation.TryGet(tangent, radial, out var rot, this, logError: false))
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, LERP_AMOUNT * Time.deltaTime);
            accumulatedRotation = transform.rotation;
        }

        VesselStatus.Speed = lastVelocity.magnitude;
        VesselStatus.Course = tangent;
        speed = VesselStatus.Speed;
    }

    // ==================================================================
    //  HELPERS
    // ==================================================================

    bool LeftIsActiveAnchor()
    {
        return leftTether.isAnchored && leftTether.triggerHeld;
    }

    void InitSphereAnglesFromCurrentPosition()
    {
        Transform anchor = LeftIsActiveAnchor() ? leftTether.anchor
            : (rightTether.isAnchored && rightTether.triggerHeld ? rightTether.anchor : null);

        if (anchor == null) return;

        Vector3 offset = transform.position - anchor.position;
        float dist = offset.magnitude;
        if (dist < 0.01f) offset = Vector3.forward;

        swingTheta = Mathf.Atan2(offset.x, offset.z);
        swingPhi   = Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(dist, 0.01f), -1f, 1f));
    }

    void InitCircleAngleFromCurrentPosition()
    {
        if (leftTether.anchor == null || rightTether.anchor == null) return;

        Vector3 a1 = leftTether.anchor.position;
        Vector3 a2 = rightTether.anchor.position;
        float d = Vector3.Distance(a1, a2);
        if (d < 0.01f) return;

        float r1 = leftTether.ropeLength;
        float a  = (r1 * r1 - rightTether.ropeLength * rightTether.ropeLength + d * d) / (2f * d);
        Vector3 axis   = (a2 - a1).normalized;
        Vector3 center = a1 + axis * a;

        Vector3 u = Vector3.Cross(axis, Vector3.up).normalized;
        if (u.sqrMagnitude < 0.01f)
            u = Vector3.Cross(axis, Vector3.forward).normalized;
        Vector3 v = Vector3.Cross(axis, u).normalized;

        Vector3 offset = transform.position - center;
        circleAngle = Mathf.Atan2(Vector3.Dot(offset, v), Vector3.Dot(offset, u));
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
