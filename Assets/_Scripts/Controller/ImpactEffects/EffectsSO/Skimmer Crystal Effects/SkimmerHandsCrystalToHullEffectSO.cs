using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A crystal the skimmer reaches is handed to the HULL, exactly as if the hull had touched it:
    /// the two halves a physics contact dispatches, through the sweep entry point
    /// (<see cref="ImpactorBase.AcceptImpacteeFromSweep"/>), the same way
    /// <see cref="CrystalRaceRing"/> credits a threaded ring.
    ///
    /// <para><b>Why it exists.</b> A skimmer sphere strictly contains its hull, so whatever the
    /// skimmer collects the hull never touches (the elemental crystal's collider is disabled the
    /// moment <see cref="ElementalCrystalImpactor.CollectBy"/> takes it). On a hull whose OWN crystal
    /// chain is part of its kit - the Serpent's every crystal re-shields its seed walls
    /// (<see cref="SerpentWallShieldByCrystalEffectSO"/>, in all five lists of its vessel container) -
    /// giving it a skimmer would otherwise trade that chain for the element level. With this effect in
    /// the skimmer's container it keeps both: the skimmer collects the element, the hull runs its chain.</para>
    ///
    /// <para><b>Omni crystals</b> (this effect opts in): an omni crystal is collected by a VESSEL
    /// contact only (<see cref="OmniCrystalImpactor"/>), so the crystal's half is dispatched too. It is
    /// authority-gated inside the crystal (a network client returns), and the vessel half routes its
    /// effects by ownership (<see cref="VesselImpactor.RouteCrystalPickup()"/>), so this is the same
    /// network shape as a hull contact. Both halves latch per crystal, so a hull that does touch the
    /// crystal a moment later does not run either chain twice.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "SkimmerHandsCrystalToHullEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/SkimmerHandsCrystalToHullEffectSO")]
    public sealed class SkimmerHandsCrystalToHullEffectSO : SkimmerCrystalEffectSO
    {
        public override void Execute(SkimmerImpactor impactor, CrystalImpactor impactee)
        {
            if (impactor == null || impactee == null || impactee.Crystal == null) return;

            var status = impactor.Skimmer ? impactor.Skimmer.VesselStatus : null;
            var hull = status?.Transform;
            if (!hull || !hull.TryGetComponent(out VesselImpactor vessel)) return;

            vessel.AcceptImpacteeFromSweep(impactee);
            if (impactee is OmniCrystalImpactor)
                impactee.AcceptImpacteeFromSweep(vessel);
        }
    }
}
