using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Executes <see cref="PlaceSwitchActionSO"/>: gates on switch charges, claims occupancy
    /// brick-by-brick, lays the ring through the standard pooled prism channel with the
    /// standard bloom stamps, and spends the charge. Per-vessel state lives here (the SO is
    /// shared and stateless). Rides the normal R_VesselActionHandler ServerRpc→ClientRpc
    /// re-execution, so every peer lays the same ring from its replicated transform.
    ///
    /// The ring's plane is ⊥ to the vessel's COURSE; each brick's prism-forward (+z, the long
    /// axis) runs along the ring tangent with its up pointing radially outward, so the ring
    /// reads as a hoop rather than a fan (Docs conventions: prism z is forward — a chained
    /// ribbon is LookRotation(tangent, radial) with a large z).
    /// </summary>
    public class PlaceSwitchActionExecutor : ShipActionExecutorBase
    {
        [Tooltip("The standard pooled prism spawn channel " +
                 "(Assets/_SO_Assets/Event Channels/Prisms/EventOnSpawnPrismAndReturn.asset).")]
        [SerializeField] PrismEventChannelWithReturnSO prismSpawnEvent;

        // The float-ulp epsilon from SCARAB.md §3.3: a full 1.0 meter minus two exact 1/3
        // spends lands one ulp BELOW 1/3f, so the gate must sit a hair under the cost.
        const float CostEpsilon = 0.001f;

        IVesselStatus _status;
        int _placedCounter;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
        }

        public void PlaceSwitch(PlaceSwitchActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;
            _status = status;

            var resources = status.ResourceSystem;
            if (!resources) return;

            if (!prismSpawnEvent)
            {
                CSDebug.LogError("[PlaceSwitch] Prism spawn event channel is not assigned on the executor.");
                return;
            }

            float cost = so.ComputeCost(resources);
            if (cost <= 0f) return;
            var meter = resources.Resources[so.ResourceIndex];
            if (meter.CurrentAmount < cost - CostEpsilon)
            {
                // Refusal: no charge, nothing spawns. (HUD pips already show the count;
                // a refusal SFX is a follow-up alongside the Scarab HUD pass.)
                CSDebug.Log("[PlaceSwitch] Refused — no switch charge banked.");
                return;
            }

            var ship = status.ShipTransform ? status.ShipTransform : transform;
            Vector3 course = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : ship.forward;

            // Ring basis: any stable up ⊥ course. Prefer the ship's up so the ring's
            // orientation reads as "thrown from the vessel".
            Vector3 up = Vector3.ProjectOnPlane(ship.up, course);
            if (up.sqrMagnitude < 1e-4f)
                up = Vector3.ProjectOnPlane(Vector3.up, course);
            if (up.sqrMagnitude < 1e-4f)
                up = Vector3.Cross(course, Vector3.right);
            up.Normalize();
            Vector3 right = Vector3.Cross(up, course).normalized;

            float distance = so.placementDistance.EvaluateLive(status); // SPACE, live at use time
            float radius = so.RingRadius * so.switchScale.EvaluateLive(status); // MASS, live
            Vector3 center = ship.position + course * distance;

            // Claim-before-spawn: physics queries are blind to fresh prisms (0.6s collider
            // delay), so occupancy rides the spatial index. Clear radius ≈ half the brick's
            // largest dimension.
            var index = PrismSpatialIndex.EnsureInstance();
            float clearRadius = Mathf.Max(2f, 0.5f * Mathf.Max(so.BrickScale.x, Mathf.Max(so.BrickScale.y, so.BrickScale.z)));

            var trail = new Trail();
            int placed = 0;
            int ringId = ++_placedCounter;

            for (int i = 0; i < so.BrickCount; i++)
            {
                float angle = (i / (float)so.BrickCount) * Mathf.PI * 2f;
                Vector3 radial = (Mathf.Cos(angle) * up + Mathf.Sin(angle) * right).normalized;
                Vector3 pos = center + radial * radius;

                if (index != null && !index.TryReserve(pos, clearRadius))
                    continue; // occupied — partial rings are legal, overlap-spawns are not

                Vector3 tangent = Vector3.Cross(course, radial).normalized;
                if (!SafeLookRotation.TryGet(tangent, radial, out var rotation, this))
                    continue;

                var data = new PrismEventData
                {
                    ownDomain = status.Domain,
                    Rotation = rotation,
                    SpawnPosition = pos,
                    Scale = so.BrickScale,
                    Velocity = Vector3.zero,
                    PrismType = PrismType.Interactive,
                    TargetTransform = null,
                    OnGrowCompleted = null
                };

                var ret = prismSpawnEvent.RaiseEvent(data);
                if (!ret.SpawnedObject) continue;
                if (!ret.SpawnedObject.TryGetComponent(out Prism prism)) continue;

                prism.ownerID = $"{status.PlayerName}::Switch::{ringId}::{i}";
                prism.Domain = status.Domain;

                // The one growth engine (Docs/PRISM_ANIMATION.md): TargetScale is the initial
                // condition, SetGrowthRate pushes the rate to the animator, Initialize stamps
                // the bloom. Colliders and gameplay state go final at spawn; only photons grow.
                prism.TargetScale = so.BrickScale;
                prism.SetGrowthRate(so.GrowthRate);
                prism.Initialize(status.PlayerName);

                prism.Trail = trail;
                trail.Add(prism);
                placed++;
            }

            if (placed == 0)
            {
                CSDebug.Log("[PlaceSwitch] No bricks placed (occupancy fully blocked) — charge not spent.");
                return;
            }

            resources.ChangeResourceAmount(so.ResourceIndex, -cost);
            CSDebug.Log($"[PlaceSwitch] Placed switch ring {ringId}: {placed}/{so.BrickCount} bricks, " +
                        $"r={radius:F0} at {distance:F0}u ahead.");
        }
    }
}
