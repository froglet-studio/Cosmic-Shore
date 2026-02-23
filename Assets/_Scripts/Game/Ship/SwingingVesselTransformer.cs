using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Utility;
using CosmicShore.Utilities;

/// <summary>
/// Vessel transformer that adds Spider-Man-style swinging mechanics.
/// When swinging is activated, the vessel latches onto the nearest prism
/// (or spawns one if none exist) and swings around it like a pendulum.
/// Releasing the swing flings the vessel forward with accumulated momentum.
/// </summary>
public class SwingingVesselTransformer : VesselTransformer
{
    [Header("Swing Settings")]
    [SerializeField] float swingSearchRadius = 150f;
    [SerializeField] float ropeLength = 40f;
    [SerializeField] float swingGravity = 30f;
    [SerializeField] float swingDamping = 0.98f;
    [SerializeField] float releaseBoostMultiplier = 2f;
    [SerializeField] float minFlingSpeed = 20f;
    [SerializeField] float steerTorque = 15f;

    [Header("Anchor Prism Spawning")]
    [SerializeField] float anchorSpawnDistance = 60f;
    [SerializeField] Vector3 anchorPrismScale = new(6f, 6f, 6f);
    [SerializeField] PrismEventChannelWithReturnSO prismSpawnChannel;

    // Swing state
    bool isSwinging;
    Transform anchorTransform;
    Vector3 swingVelocity;
    float currentRopeLength;

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

    public bool IsSwinging => isSwinging;

    protected override void Update()
    {
        if (VesselStatus == null || VesselStatus.IsStationary)
            return;

        VesselStatus.blockRotation = transform.rotation;

        if (isSwinging && anchorTransform != null)
        {
            SwingUpdate();
        }
        else
        {
            base.Update();
        }
    }

    /// <summary>
    /// Begin a swing. Finds the nearest prism or spawns one as the anchor.
    /// </summary>
    public void StartSwing()
    {
        if (isSwinging) return;

        var anchor = FindNearestPrism();
        if (anchor == null)
            anchor = SpawnAnchorPrism();

        if (anchor == null) return;

        anchorTransform = anchor;
        currentRopeLength = Vector3.Distance(transform.position, anchorTransform.position);

        // Clamp rope length so swings aren't absurdly long or short
        currentRopeLength = Mathf.Clamp(currentRopeLength, ropeLength * 0.3f, ropeLength * 2f);

        // Initialize swing velocity from current movement direction
        swingVelocity = VesselStatus.Course * VesselStatus.Speed;
        isSwinging = true;
    }

    /// <summary>
    /// Release the swing, flinging the vessel forward with momentum.
    /// </summary>
    public void ReleaseSwing()
    {
        if (!isSwinging) return;

        isSwinging = false;

        // Fling: transfer swing velocity into forward momentum
        float flingSpeed = Mathf.Max(swingVelocity.magnitude * releaseBoostMultiplier, minFlingSpeed);
        Vector3 flingDir = swingVelocity.normalized;

        if (flingDir.sqrMagnitude < 0.01f)
            flingDir = transform.forward;

        VesselStatus.Course = flingDir;
        VesselStatus.Speed = flingSpeed;
        speed = flingSpeed;

        // Orient vessel to face fling direction
        if (SafeLookRotation.TryGet(flingDir, out var rot, this, logError: false))
            accumulatedRotation = rot;

        anchorTransform = null;
    }

    void SwingUpdate()
    {
        if (anchorTransform == null)
        {
            isSwinging = false;
            return;
        }

        Vector3 anchorPos = anchorTransform.position;
        Vector3 ropeDir = transform.position - anchorPos;
        float dist = ropeDir.magnitude;

        if (dist < 0.01f)
        {
            // Edge case: vessel is at anchor — nudge it
            transform.position += Vector3.down * 0.5f;
            return;
        }

        Vector3 ropeNorm = ropeDir / dist;

        // Gravity pulls down
        swingVelocity += Vector3.down * (swingGravity * Time.deltaTime);

        // Player steering: use input to add tangential force
        if (InputStatus != null)
        {
            // Tangent plane perpendicular to rope
            Vector3 tangentRight = Vector3.Cross(ropeNorm, Vector3.up).normalized;
            Vector3 tangentUp = Vector3.Cross(tangentRight, ropeNorm).normalized;

            // If tangentRight collapsed (rope is vertical), pick a different reference
            if (tangentRight.sqrMagnitude < 0.01f)
            {
                tangentRight = Vector3.Cross(ropeNorm, Vector3.forward).normalized;
                tangentUp = Vector3.Cross(tangentRight, ropeNorm).normalized;
            }

            float steerH = InputStatus.XSum;
            float steerV = InputStatus.YSum;
            swingVelocity += (tangentRight * steerH + tangentUp * steerV) * (steerTorque * Time.deltaTime);
        }

        // Apply damping
        swingVelocity *= Mathf.Pow(swingDamping, Time.deltaTime);

        // Project velocity onto tangent plane (remove radial component to enforce rope constraint)
        float radialComponent = Vector3.Dot(swingVelocity, ropeNorm);
        swingVelocity -= ropeNorm * radialComponent;

        // Move vessel
        transform.position += swingVelocity * Time.deltaTime;

        // Enforce rope length constraint (snap back to rope radius)
        Vector3 newRopeDir = transform.position - anchorPos;
        float newDist = newRopeDir.magnitude;
        if (newDist > currentRopeLength)
        {
            transform.position = anchorPos + (newRopeDir / newDist) * currentRopeLength;
        }

        // Orient vessel to face swing direction
        if (swingVelocity.sqrMagnitude > 0.1f)
        {
            if (SafeLookRotation.TryGet(swingVelocity.normalized, out var lookRot, this, logError: false))
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, LERP_AMOUNT * Time.deltaTime);
                accumulatedRotation = transform.rotation;
            }
        }

        // Update vessel status
        VesselStatus.Speed = swingVelocity.magnitude;
        VesselStatus.Course = swingVelocity.normalized;
    }

    Transform FindNearestPrism()
    {
        int layerMask = 1 << TrailBlocksLayer;
        var colliders = Physics.OverlapSphere(transform.position, swingSearchRadius, layerMask);

        Transform closest = null;
        float closestDist = float.MaxValue;

        foreach (var col in colliders)
        {
            if (!col.TryGetComponent<Prism>(out var prism)) continue;
            if (prism.destroyed) continue;

            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = col.transform;
            }
        }

        return closest;
    }

    Transform SpawnAnchorPrism()
    {
        if (prismSpawnChannel == null)
        {
            Debug.LogWarning("[SwingingVesselTransformer] No prism spawn channel assigned — cannot create anchor prism.");
            return null;
        }

        // Spawn the anchor prism ahead and above the vessel
        Vector3 spawnPos = transform.position
                         + transform.forward * anchorSpawnDistance
                         + Vector3.up * (anchorSpawnDistance * 0.5f);

        var ret = prismSpawnChannel.RaiseEvent(new PrismEventData
        {
            ownDomain = VesselStatus.Domain,
            Rotation = Quaternion.identity,
            SpawnPosition = spawnPos,
            Scale = anchorPrismScale,
            PrismType = PrismType.Interactive
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
