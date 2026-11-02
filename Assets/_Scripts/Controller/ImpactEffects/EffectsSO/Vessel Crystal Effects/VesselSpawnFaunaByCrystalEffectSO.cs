using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A crystal the vessel collects PRODUCES a creature in the vessel's own domain (Garrett, 2026-10-24: "lets have the
    /// omnicrystal produce an omnivorous worm fauna in the player's domain"; the Jacks wire it to the worm colony, which
    /// eats both prisms and creatures). The creature is born through the platform's one fauna spawn seam
    /// (<see cref="CellLifeSpawnerBase.SpawnFaunaWithDomain"/>), so it blooms in, registers with its cell and lives and dies
    /// by the ordinary rules; only its colour is the collector's, the third sign-off on the one-colour law
    /// (<c>Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md</c>).
    ///
    /// <para>The vessel's crystal effects run on every peer. A replicated species is born only where the ecology is
    /// simulated (the server spawns it to the others); an unreplicated one is born on each peer, as every local species is.
    /// Stateless; per-impact live reads.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "VesselSpawnFaunaByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselSpawnFaunaByCrystalEffectSO")]
    public class VesselSpawnFaunaByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("The species the crystal produces (the Jacks: the worm colony). Empty produces nothing.")]
        [SerializeField] FaunaConfigurationSO fauna;

        [Tooltip("How far behind the vessel the creature is born (u), so it does not start inside the hull.")]
        [SerializeField, Min(0f)] float spawnBehind = 30f;

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor ? vesselImpactor.Vessel?.VesselStatus : null;
            if (status == null || !status.Transform || !fauna || !fauna.FaunaPrefab) return;
            if (fauna.NetworkSynced && !FaunaNetworkSync.IsSimAuthority)
            {
                CrystalFaunaBirth.Decline(status, $"'{fauna.name}' is replicated and born on the ecology's peer");
                return;
            }

            var t = status.Transform;
            Vector3 at = t.position - t.forward * spawnBehind;
            var cell = Cell.FindNearestActiveCell(at);
            if (!cell) return;
            if (cell.FaunaExclusionRadius > 0f || cell.FaunaContainmentRadius > 0f) at = cell.ClampToFaunaContainment(at, at);
            var born = CellLifeSpawnerBase.SpawnFaunaWithDomain(cell, fauna.FaunaPrefab, at, status.Domain, at, fauna);
            // The omni crystal folds into the newborn's heart when the vessel wears that retirement
            // (R_VesselActions/FAUNA_BIRTH_CRYSTAL_MORPH.md); nothing listens otherwise.
            if (born) CrystalFaunaBirth.Announce(status, born);
        }
    }
}
