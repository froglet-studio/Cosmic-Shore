using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;
using CosmicShore.Utilities;

/// <summary>
/// Spider vessel transformer: dual-tether swinging through the hypersea.
///
/// Controls overview:
///   Free flight (no triggers held):
///     Standard two-thumb flying with drift. XDiff spreads cursors instead of throttle.
///     Speed is a fixed cruise speed.
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
    [SerializeField] float tetherWidth = 0.15f;

    [Header("Swing")]
    [SerializeField] float swingAngularSpeed = 2.5f;
    [SerializeField] float cursorSpreadMax = 30f;

    [Header("Free Flight")]
    [SerializeField] float cruiseSpeed = 25f;
    [SerializeField] float driftConvergeRate = 1.5f;

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
        public LineRenderer line;
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

        leftTether.line = CreateTetherLine("LeftTether");
        rightTether.line = CreateTetherLine("RightTether");

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

    LineRenderer CreateTetherLine(string childName)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = tetherWidth;
        lr.endWidth = tetherWidth * 0.5f;
        lr.useWorldSpace = true;
        lr.enabled = false;
        return lr;
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
        tether.line.enabled = true;
    }

    void ReleaseTether(ref TetherState tether)
    {
        tether.isAnchored = false;
        tether.isFiring = false;
        tether.anchor = null;
        tether.line.enabled = false;
    }

    // ==================================================================
    //  UPDATE LOOP
    // ==================================================================

    protected override void Update()
    {
        if (VesselStatus == null || VesselStatus.IsStationary)
            return;

        // Pre-processing: advance firing tethers, validate anchors, draw lines
        UpdateFiringTether(ref leftTether);
        UpdateFiringTether(ref rightTether);
        ValidateAnchor(ref leftTether);
        ValidateAnchor(ref rightTether);
        UpdateTetherVisual(ref leftTether);
        UpdateTetherVisual(ref rightTether);

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
                // Carry swing momentum
                if (lastVelocity.sqrMagnitude > 0.01f)
                {
                    VesselStatus.Course = lastVelocity.normalized;
                    speed = lastVelocity.magnitude;
                    if (SafeLookRotation.TryGet(lastVelocity.normalized, out var rot, this, logError: false))
                        accumulatedRotation = rot;
                }
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

    void UpdateFiringTether(ref TetherState tether)
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

        // Reached max range — spawn an anchor prism
        if (tether.extension >= maxTetherLength)
        {
            Vector3 spawnPos = tether.fireOrigin + tether.fireDirection * maxTetherLength;
            var anchor = SpawnAnchorPrism(spawnPos);
            if (anchor != null)
            {
                float rl = Vector3.Distance(transform.position, anchor.position);
                AnchorTether(ref tether, anchor, rl);
            }
            else
            {
                tether.isFiring = false;
                tether.line.enabled = false;
            }
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
        if (!tether.line.enabled) return;

        tether.line.SetPosition(0, transform.position);

        if (tether.isAnchored && tether.anchor != null)
            tether.line.SetPosition(1, tether.anchor.position);
        else if (tether.isFiring)
            tether.line.SetPosition(1, tether.fireOrigin + tether.fireDirection * tether.extension);
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

    // ---- Free flight: dolphin-like drift, fixed cruise speed ----

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

        // Drift: course slowly converges toward current forward
        VesselStatus.Course = Vector3.Slerp(
            VesselStatus.Course, transform.forward,
            driftConvergeRate * Time.deltaTime).normalized;

        transform.position += (speed * VesselStatus.Course + velocityShift) * Time.deltaTime;
        lastVelocity = speed * VesselStatus.Course;
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
