using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction's hunter roster is a fixed, intensity-scaled adversary count (2/3/4/5
    /// Rhino hunters per the design doc) rather than the usual team-balancing AI
    /// backfill (which fills out human player counts). Hunters always join
    /// <see cref="Domains.Blue"/> — the "no specific team" sentinel — so they never
    /// count as an ally of any human domain, and AIPilot's same-domain skip in
    /// UpdatePlayerTarget naturally keeps them from targeting each other while still
    /// targeting every human player.
    ///
    /// Hunters are also placed away from the humans: the scene's four player spawn
    /// points sit on top of each other, so drawing hunter poses from that shared pool
    /// (as <see cref="GameDataSO.AddPlayer"/> does) parked the whole pack directly
    /// behind the player at the countdown. <see cref="OnAIPlayerInitialized"/> re-poses
    /// each hunter onto its own slot around the arena instead.
    /// </summary>
    public class FrictionHunterVesselInitializer : ServerPlayerVesselInitializerWithAI
    {
        [Header("Friction Hunter Scaling")]
        [SerializeField]
        int[] hunterCountByIntensity = { 2, 3, 4, 5 };

        [SerializeField]
        float[] hunterSkillByIntensity = { 0f, 0.34f, 0.67f, 1f };

        [Header("Friction Hunter Placement")]
        [Tooltip("Optional scene-placed hunter spawn transforms. When set, hunters are " +
                 "assigned these in order and every field below is ignored. Leave empty " +
                 "to place hunters procedurally on a ring around the arena.")]
        [SerializeField]
        Transform[] hunterSpawnPoints;

        [Tooltip("Center the procedural hunter ring is built around. Leave empty to use " +
                 "the world origin, which is where the Friction crystal ring is centered.")]
        [SerializeField]
        Transform arenaCenter;

        [Tooltip("Radius of the procedural hunter ring, measured from the arena center.")]
        [SerializeField]
        float hunterRingRadius = 700f;

        [Tooltip("Hunters are fanned across this vertical band so they don't all sit in " +
                 "the same plane. Hunter 0 spawns at -spread, the last at +spread.")]
        [SerializeField]
        float hunterRingHeightSpread = 200f;

        [Tooltip("No hunter spawns closer to the human spawn cluster than this. The ring " +
                 "arc that would violate it is excluded before hunters are distributed, " +
                 "so raising this pushes the whole pack further around the arena.")]
        [SerializeField]
        float minDistanceFromPlayerSpawn = 900f;

        protected override int ResolveAICount()
        {
            int intensity = Mathf.Clamp(gameData.SelectedIntensity.Value, 1, hunterCountByIntensity.Length);
            return hunterCountByIntensity[intensity - 1];
        }

        protected override Domains ResolveAIDomain(Dictionary<Domains, int> counts) => Domains.Blue;

        protected override float ResolveAISkill()
        {
            int intensity = Mathf.Clamp(gameData.SelectedIntensity.Value, 1, hunterSkillByIntensity.Length);
            return Mathf.Clamp01(hunterSkillByIntensity[intensity - 1]);
        }

        // Tag the spawned instance only — the shared Rhino vessel prefab asset stays
        // untouched, so human Rhino players in every other mode are unaffected by
        // hunter-only impact effects like VesselLifeLossByHunterSkimmerEffectSO.
        protected override void OnAIVesselSpawned(NetworkObject aiVesselNO, Player aiPlayer)
        {
            if (!aiVesselNO.gameObject.TryGetComponent<FrictionHunterTag>(out _))
                aiVesselNO.gameObject.AddComponent<FrictionHunterTag>();
        }

        /// <summary>
        /// Overwrites the pose <see cref="GameDataSO.AddPlayer"/> just drew from the
        /// human spawn pool. Runs on the server only (the whole AI spawn path is
        /// server-side); <see cref="IVessel.SetPose"/> replicates the result to every
        /// client via VesselController.SetPose_ClientRpc.
        /// </summary>
        protected override void OnAIPlayerInitialized(Player aiPlayer, int aiIndex, int aiCount)
        {
            if (aiPlayer.Vessel == null)
            {
                CSDebug.LogWarning($"[FrictionHunterVesselInitializer] Hunter {aiIndex} has no vessel to place.");
                return;
            }

            aiPlayer.Vessel.SetPose(ResolveHunterPose(aiIndex, aiCount));
        }

        Pose ResolveHunterPose(int hunterIndex, int hunterCount)
        {
            if (hunterSpawnPoints is { Length: > 0 })
            {
                var point = hunterSpawnPoints[hunterIndex % hunterSpawnPoints.Length];
                return new Pose(point.position, point.rotation);
            }

            Vector3 center = arenaCenter ? arenaCenter.position : Vector3.zero;
            Vector3 position = ResolveRingPosition(center, hunterIndex, hunterCount);

            // Face the arena center so hunters converge on the play space once they wake,
            // rather than starting nose-out and having to turn around.
            Vector3 inward = center - position;
            Quaternion rotation = inward.sqrMagnitude > float.Epsilon
                ? Quaternion.LookRotation(inward.normalized, Vector3.up)
                : Quaternion.identity;

            return new Pose(position, rotation);
        }

        /// <summary>
        /// Spreads hunters evenly around the ring, but only across the arc that clears
        /// <see cref="minDistanceFromPlayerSpawn"/> from the human spawn cluster. The
        /// excluded wedge is derived from the chord length, so the guarantee holds for
        /// any radius / hunter count combination rather than depending on hand-tuned
        /// angles. Distribution is deterministic — same inputs, same layout on every
        /// machine, no shared RNG seed needed.
        /// </summary>
        Vector3 ResolveRingPosition(Vector3 center, int hunterIndex, int hunterCount)
        {
            float radius = Mathf.Max(1f, hunterRingRadius);
            Vector3 playerAnchor = ResolvePlayerSpawnAnchor(center);

            Vector3 anchorOffset = playerAnchor - center;
            Vector2 anchorFlat = new(anchorOffset.x, anchorOffset.z);
            float anchorAngle = anchorFlat.sqrMagnitude > float.Epsilon
                ? Mathf.Atan2(anchorFlat.y, anchorFlat.x)
                : 0f;

            // Half-angle of the wedge whose chord equals the requested clearance.
            // Clamped so an unreachable clearance degrades to "put them opposite the
            // players" instead of collapsing the usable arc to nothing.
            float halfWedge = 2f * Mathf.Asin(Mathf.Clamp(minDistanceFromPlayerSpawn / (2f * radius), 0f, 0.95f));
            float usableArc = 2f * Mathf.PI - 2f * halfWedge;

            int count = Mathf.Max(1, hunterCount);
            float angle = anchorAngle + halfWedge + (hunterIndex + 0.5f) * (usableArc / count);

            float height = count == 1
                ? 0f
                : Mathf.Lerp(-hunterRingHeightSpread, hunterRingHeightSpread, hunterIndex / (float)(count - 1));

            return center
                   + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius
                   + Vector3.up * height;
        }

        /// <summary>
        /// The point hunters keep their distance from: the centroid of the scene's
        /// player spawn poses. Read from <see cref="GameDataSO.SpawnPoses"/> rather than
        /// live human vessels because hunters spawn before the base class processes any
        /// human player, so no human vessel exists yet.
        /// </summary>
        Vector3 ResolvePlayerSpawnAnchor(Vector3 fallbackCenter)
        {
            var poses = gameData.SpawnPoses;
            if (poses == null || poses.Length == 0)
                return fallbackCenter + Vector3.forward;

            Vector3 sum = Vector3.zero;
            foreach (var pose in poses)
                sum += pose.position;

            return sum / poses.Length;
        }
    }
}
