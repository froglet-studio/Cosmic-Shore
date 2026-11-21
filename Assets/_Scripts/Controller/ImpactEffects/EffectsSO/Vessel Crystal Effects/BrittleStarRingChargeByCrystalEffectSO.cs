using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Brittle Star's crystal pickup (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §3.5): a crystal is the
    /// resource the cockpit builds a support ring from. With no ring it rebuilds one at once; with a ring and no charge
    /// it stores the one charge the hull can hold; with a charge already stored it throws the hull into OVERDRIVE — all
    /// three modes at once for a short time (<see cref="BrittleStarExecutor.CollectCrystal"/>).
    ///
    /// <para>Wired into EVERY crystal list of <c>BrittleStarImpactorDataContainer</c> — the omni list and the four
    /// elemental ones — because the class document says "a crystal", not a kind of crystal. Both dispatches run on every
    /// peer (the owner relays them through <c>NetworkVesselImpactor</c>), so every machine's copy of the ring and its
    /// charge agree, which matters: the ring's presence decides the modes, and the modes decide the trail every peer
    /// lays.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BrittleStarRingChargeByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/BrittleStarRingChargeByCrystalEffectSO")]
    public sealed class BrittleStarRingChargeByCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;
            // The executor lives on the hull's ShipActions child; this effect is on no other vessel's container, so a
            // miss here is a Brittle Star prefab without its executor.
            var star = vesselImpactor.GetComponentInChildren<BrittleStarExecutor>(true);
            if (!star)
            {
                CSDebug.LogWarning($"[BrittleStar] '{vesselImpactor.name}' collected a crystal but has no " +
                                   "BrittleStarExecutor - is this effect wired into a non-Brittle Star container?", vesselImpactor);
                return;
            }
            star.CollectCrystal();
        }
    }
}
