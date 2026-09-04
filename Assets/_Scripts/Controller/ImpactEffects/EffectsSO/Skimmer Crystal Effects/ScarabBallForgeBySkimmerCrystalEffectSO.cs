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
    /// OMNI CRYSTALS ONLY (see the guard in Execute): an elemental crystal is the element economy
    /// every vessel shares, so it collects normally through the hull instead of being spent on a
    /// ball. The blast forge was already omni-only by construction.
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

            // OMNI CRYSTALS ONLY. An ELEMENTAL crystal is the platform's element economy - it is
            // how every vessel levels Charge/Mass/Space/Time - and turning one into a ball spent
            // it for something else entirely, so a Scarab could never level an element it flew
            // past. Skipping it here hands the crystal back to the HULL, whose four elemental
            // branches then collect it normally (the skimmer sphere strictly contains the hull, so
            // whatever the skimmer consumes, the hull never sees). TeamCrystalImpactor derives
            // from OmniCrystalImpactor and is deliberately included: a team crystal is a
            // domain-locked omni, the same family, and forges the same way.
            //
            // This also makes the two forge paths agree. The BLAST path was omni-only already, not
            // by choice but by construction - ExplosionImpactor.SweepCrystals only picks up
            // OmniCrystalImpactor - so the skimmer was the odd one out.
            if (impactee is not OmniCrystalImpactor) return;

            var status = impactor.Skimmer.VesselStatus;
            if (status == null) return;

            // Only the server mints. This runs on every peer whose skimmer overlaps the crystal.
            if (!ScarabBallForge.CanSpawnLocally) return;

            var crystal = impactee.Crystal;
            if (crystal == null || crystal.IsEmbedded || crystal.IsExploding) return;

            // AT REST, AT THE CRYSTAL. The ball inherits nothing from the vessel — that is the
            // entire point of moving the forge onto the skimmer. Through Request, so a mode's
            // ForgeGate (a mode policy hook, unused today) and the OnForged adoption apply
            // here exactly as they do to the blast forge.
            Vector3 at = crystal.transform.position;
            var ball = ScarabBallForge.Request(status, _ballPrefab, at, Vector3.zero);
            if (ball == null) return;   // a mode's ForgeGate refused — the crystal is left alone

            // THE CRYSTAL BECOMES THE BALL, and the animation says so. Stamped before the consume
            // below so the origin is read while the crystal is still standing where it was spent —
            // Crystal.CollectPose covers the same-frame respawn either way, but reading it first is
            // what makes that guarantee unnecessary rather than load-bearing.
            //
            // This is the Scarab's bespoke omni-crystal retirement, and it replaces the shared husk
            // spray rather than joining it: two retirements would claim the same body and draw over
            // each other. The suppression travels on the consume payload below, because the husk is
            // spawned on every peer and so the suppression has to reach every peer.
            ball.MarkForgedFromCrystal(crystal);

            ConsumeCrystal(crystal, status);

            CSDebug.LogVerbose(CSLogChannel.ScarabNucleus,
                $"[ScarabBallForge] {status.PlayerName}'s skimmer turned a crystal into a " +
                $"{status.Domain} ball at {at}.");
        }

        /// <summary>
        /// The crystal leaves the world the same way a collected one does — retired, then respawned
        /// through its manager — so it neither pops out of existence nor stops the supply. The
        /// OmniCrystalImpactor consume shape, including its manager-less local-mint fallback (the
        /// freestyle conveyor toy mints crystals with no manager to respawn through).
        ///
        /// The HUSK is suppressed and nothing else is: the pickup sound still plays and the impact
        /// latch still closes on every peer, because those belong to the pickup rather than to the
        /// spray. What replaces the spray is <see cref="ScarabCrystalMorph"/>, which carries this
        /// crystal's own body onto the ball instead of shattering it.
        /// </summary>
        static void ConsumeCrystal(Crystal crystal, IVesselStatus status)
        {
            var explode = new Crystal.ExplodeParams
            {
                Course = status.Course.sqrMagnitude > 1e-4f ? status.Course.normalized : Vector3.forward,
                Speed = 0f,
                PlayerName = status.PlayerName,
                SuppressHusk = true,
            };

            if (crystal.CrystalManager != null)
                crystal.NotifyManagerToExplodeCrystal(explode);
            else
                crystal.Explode(explode);

            crystal.Respawn();
        }
    }
}
