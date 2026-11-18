using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An omni crystal the Goby and Pistol collect - by the Goby's hull or by the Pistol's body through the undertow -
    /// restocks the pair (<see cref="GobyPistolDuo.OnOmniCrystal"/>): the Pistol's energy fills and every move's cooldown
    /// resets (2026-11-17; design brief <c>.claude/skills/duo-vessel/references/designs/goby-and-pistol.md</c> sections 6
    /// and 7). Wired into the omni list of <c>GobyImpactorDataContainer</c> only. Omni effects run where the collect
    /// resolves and on a remote pilot's own machine (<c>OmniCrystalImpactor.ReplayOnRemoteOwner</c>); the restock acts on
    /// the machine that decides the hull (<see cref="ElementalTransfer.IsDecidedHere"/>), so it happens once.
    /// </summary>
    [CreateAssetMenu(fileName = "GobyOmniCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/GobyOmniCrystalEffectSO")]
    public sealed class GobyOmniCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor && vesselImpactor.Vessel != null ? vesselImpactor.Vessel.VesselStatus : null;
            if (status == null || !ElementalTransfer.IsDecidedHere(status)) return;
            var hull = vesselImpactor.Vessel.Transform;
            var duo = hull ? hull.GetComponentInChildren<GobyPistolDuo>(true) : null;
            if (duo) duo.OnOmniCrystal();
        }
    }
}
