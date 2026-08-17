using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's ball forge (design: R_VesselActions/SCARAB.md §4.1). Crystals fill an energy
    /// meter; once it is FULL, the next crystal contact materialises a ball at the crystal,
    /// carrying the vessel's velocity, and spends the meter.
    ///
    /// Ordering inside the container is the mechanic: this effect must sit BEFORE the
    /// energy-adding effect in `vesselCrystalEffects`, so it tests the meter as it stood on
    /// arrival. That is what makes it "the NEXT crystal after you fill up" rather than "the
    /// crystal that fills you up" — the beat the design notes ask for.
    ///
    /// Why the ball is aimed rather than spawned by a button: you have to fly THROUGH a crystal at
    /// the speed and heading you want the ball to leave with, so making a ball is a piece of
    /// driving. Velocity comes from the vessel's own Course × Speed (its transform-driven motion),
    /// scaled by <see cref="_inheritedVelocityFraction"/>.
    ///
    /// SERVER-ONLY, and that is not incidental: the omni-crystal chain reaches this effect through
    /// NetworkVesselImpactor's ServerRpc → **ClientRpc**, so `Execute` runs on EVERY peer. An
    /// ungated spawn would mint one ball per connected client. The ball is a NetworkObject: the
    /// server spawns it once and Netcode replicates it to everyone.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScarabBallForgeByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/ScarabBallForgeByCrystalEffectSO")]
    public class ScarabBallForgeByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Header("Energy")]
        [Tooltip("Gate ball creation on the energy meter — the design's economy (SCARAB.md §4.1: " +
                 "crystals fill a meter, and only once it is FULL does the next crystal forge). " +
                 "OFF by request for now: EVERY omni crystal forges a ball outright and no energy " +
                 "is spent. The meter still fills from its sibling effect, so while this is off " +
                 "the HUD's energy ring is a readout with nothing gated behind it. Turning this " +
                 "back on restores the economy with no other change.")]
        [SerializeField] bool _requireEnergy = false;

        [Tooltip("Which resource meter holds ball energy (the Scarab authors index 0). " +
                 "Only read when _requireEnergy is on.")]
        [SerializeField] int _energyResourceIndex = 0;

        [Tooltip("Fraction of the meter's max that counts as 'full'. At or above this, the next " +
                 "crystal contact forges a ball instead of just topping up.")]
        [SerializeField, Range(0.1f, 1f)] float _forgeThreshold = 1f;

        [Tooltip("Energy spent per ball, as a fraction of the meter's max. CHARGE lowers this " +
                 "once the elemental hook lands (SCARAB.md §7).")]
        [SerializeField, Range(0.1f, 1f)] float _energyCostPerBall = 1f;

        [Header("Ball")]
        [Tooltip("The networked ball prefab (AstroLeagueBall). Must also be registered in " +
                 "DefaultNetworkPrefabs or Spawn() throws.")]
        [SerializeField] AstroLeagueBall _ballPrefab;

        [Tooltip("How much of the vessel's velocity the fresh ball inherits. 1 = the ball leaves " +
                 "exactly as fast as you were flying.")]
        [SerializeField, Min(0f)] float _inheritedVelocityFraction = 1f;

        [Tooltip("Minimum launch speed, so forging while nearly stopped still produces a ball that " +
                 "moves instead of one that sits on your nose.")]
        [SerializeField, Min(0f)] float _minimumLaunchSpeed = 40f;

        [Tooltip("Spawn offset ahead of the vessel along the launch direction, so the new ball " +
                 "does not materialise inside the hull that made it. The vessel is touching the " +
                 "crystal at this instant, so this is the crystal's position plus clearance — " +
                 "CrystalImpactData carries no position, and widening that networked struct for " +
                 "one vessel would change the wire format for the whole fleet.")]
        [SerializeField, Min(0f)] float _forwardClearance = 25f;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor != null ? vesselImpactor.Vessel?.VesselStatus : null;
            if (status == null) return;

            // Runs on every peer (ClientRpc fan-out) — only the server may spawn.
            if (!ScarabBallForge.CanSpawnLocally) return;

            var resources = status.ResourceSystem;
            Resource meter = null;

            if (_requireEnergy)
            {
                if (resources == null) return;
                if (_energyResourceIndex < 0 || _energyResourceIndex >= resources.Resources.Count) return;

                meter = resources.Resources[_energyResourceIndex];
                if (meter == null || meter.MaxAmount <= 0f) return;

                // Tested BEFORE the sibling energy-add effect runs (container order) — the meter
                // must already have been full when this crystal was touched.
                if (meter.CurrentAmount < meter.MaxAmount * _forgeThreshold) return;
            }

            var ship = status.ShipTransform ? status.ShipTransform : vesselImpactor.transform;

            // TRUE velocity, not Speed × Course. A juke/dash rides the ModifyVelocity channel,
            // which displaces the vessel WITHOUT changing Speed or Course — so reading those two
            // alone throws the ball down the throttle line and silently discards the dash. The
            // ball's own strike path samples real per-tick motion, so using anything less here
            // would make "dash into a crystal" and "dash into a ball" produce different
            // trajectories from the same input.
            Vector3 velocity = status.Speed * status.Course;
            var transformer = status.VesselTransformer;
            if (transformer) velocity += transformer.VelocityShift;

            Vector3 heading = velocity.sqrMagnitude > 1e-4f
                ? velocity.normalized
                : (status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : ship.forward);
            float launchSpeed = Mathf.Max(velocity.magnitude * _inheritedVelocityFraction, _minimumLaunchSpeed);
            Vector3 course = heading;
            Vector3 spawnAt = ship.position + course * _forwardClearance;

            var ball = ScarabBallForge.Spawn(_ballPrefab, spawnAt, course * launchSpeed,
                                             status.Domain, ScarabBallForge.SizeScaleFor(status));
            if (ball == null) return;

            if (_requireEnergy && meter != null)
                resources.ChangeResourceAmount(_energyResourceIndex, -meter.MaxAmount * _energyCostPerBall);

            CSDebug.Log($"[ScarabBallForge] {status.PlayerName} forged a {status.Domain} ball at " +
                        $"{spawnAt} @ {launchSpeed:F0} u/s.");
        }
    }
}
