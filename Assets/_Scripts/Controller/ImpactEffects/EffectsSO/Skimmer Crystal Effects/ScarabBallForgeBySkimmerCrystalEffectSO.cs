using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's ball forge (design: R_VesselActions/SCARAB.md §4.1): **the skimmer touches a
    /// crystal and the crystal BECOMES a ball, in place, at rest.**
    ///
    /// This replaces an earlier hull-collision forge that tried to make collecting a crystal *feel*
    /// like striking a ball — it spawned the ball ahead of the vessel along its course, carrying a
    /// fraction of its velocity, with a forward clearance so the new ball did not materialise inside
    /// the hull. Every one of those numbers was an approximation of a collision that had not
    /// happened yet, and none of them could get the feel right, because the ball was leaving before
    /// the ship arrived.
    ///
    /// The skimmer removes the whole problem. Its sphere reaches well beyond the hull, so the
    /// conversion happens BEFORE the ship gets there, and the ball is sitting still when it does.
    /// The hull then hits a real ball, and the real strike path — the one every other contact in the
    /// game already uses — produces the trajectory. No inherited velocity, no minimum launch speed,
    /// no clearance offset: the physics that was being imitated now simply runs.
    ///
    /// MECHANICALLY INSTANT, VISUALLY GRADUAL. The ball is fully live the moment it is minted, so a
    /// pilot arriving one frame later strikes a finished ball. The crystal→ball morph is pure
    /// presentation and is free to still be playing while the ball is already struck and travelling:
    /// the crystal leaves through its own shipped collect burst (continuity of existence), and the
    /// ball blooms in over <c>AstroLeagueSettingsSO.spawnBloomSeconds</c>, a scale animation that
    /// simply keeps running wherever the ball has got to.
    ///
    /// SERVER-ONLY, and it needs no round-trip to be fair. The server simulates every vessel, so the
    /// server's copy of any pilot's skimmer overlaps the crystal and converts it — including a remote
    /// client's. Clients see the crystal go and the ball arrive by ordinary replication.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ScarabBallForgeBySkimmerCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/ScarabBallForgeBySkimmerCrystalEffectSO")]
    public sealed class ScarabBallForgeBySkimmerCrystalEffectSO : SkimmerCrystalEffectSO
    {
        [Header("Ball")]
        [Tooltip("The networked ball prefab (AstroLeagueBall). Must also be registered in " +
                 "DefaultNetworkPrefabs or Spawn() throws.")]
        [SerializeField] AstroLeagueBall _ballPrefab;

        public override void Execute(SkimmerImpactor impactor, CrystalImpactor impactee)
        {
            if (impactor == null || impactor.Skimmer == null || impactee == null) return;

            var status = impactor.Skimmer.VesselStatus;
            if (status == null) return;

            // Only the server mints. This runs on every peer whose skimmer overlaps the crystal.
            if (!ScarabBallForge.CanSpawnLocally) return;

            var crystal = impactee.Crystal;
            if (crystal == null || crystal.IsEmbedded || crystal.IsExploding) return;

            // AT REST, AT THE CRYSTAL. The ball inherits nothing from the vessel — that is the
            // entire point of moving the forge onto the skimmer. Through Request, so a mode's
            // ForgeGate (Scarab Scramble's per-domain live-ball cap) and the OnForged adoption
            // apply here exactly as they do to the blast forge.
            Vector3 at = crystal.transform.position;
            var ball = ScarabBallForge.Request(status, _ballPrefab, at, Vector3.zero);
            if (ball == null) return;   // gate refused (at the cap) — the crystal is left alone

            ConsumeCrystal(crystal, status);

            CSDebug.LogVerbose(CSLogChannel.ScarabNucleus,
                $"[ScarabBallForge] {status.PlayerName}'s skimmer turned a crystal into a " +
                $"{status.Domain} ball at {at}.");
        }

        /// <summary>
        /// The crystal leaves the world the same way a collected one does — burst, then respawn
        /// through its manager — so it neither pops out of existence nor stops the supply. The
        /// OmniCrystalImpactor consume shape, including its manager-less local-mint fallback (the
        /// freestyle conveyor toy mints crystals with no manager to respawn through).
        /// </summary>
        static void ConsumeCrystal(Crystal crystal, IVesselStatus status)
        {
            var explode = new Crystal.ExplodeParams
            {
                Course = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : Vector3.forward,
                Speed = 0f,
                PlayerName = status.PlayerName,
            };

            if (crystal.CrystalManager != null)
                crystal.NotifyManagerToExplodeCrystal(explode);
            else
                crystal.Explode(explode);

            crystal.Respawn();
        }
    }
}
