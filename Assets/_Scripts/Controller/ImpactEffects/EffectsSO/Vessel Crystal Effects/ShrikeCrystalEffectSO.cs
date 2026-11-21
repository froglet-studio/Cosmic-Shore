using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike's crystal pickup (design: <c>R_VesselActions/SHRIKE.md</c> §3.5): the crystal becomes a NEST FLORA where
    /// it was, which visibly grows large missiles (Garrett, 2026-10-15), and the Void Sling resets (the Shrike Studio's
    /// <c>pickUp</c>: "resetting your void sling") — <see cref="ShrikeExecutor.CollectCrystal"/>.
    ///
    /// <para>Wired into EVERY crystal list of <c>ShrikeImpactorDataContainer</c> — the document says "a crystal", not a
    /// kind of crystal. The dispatch runs on every peer (the owner relays it through <c>NetworkVesselImpactor</c>), so
    /// every machine's copy of the cooldown and the flora agree. The crystal's own collect pose
    /// (<see cref="CrystalImpactData.Origin"/>) places the flora on every peer, even after the crystal has respawned.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ShrikeCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/ShrikeCrystalEffectSO")]
    public sealed class ShrikeCrystalEffectSO : VesselCrystalEffectSO
    {
        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;
            var shrike = vesselImpactor.GetComponentInChildren<ShrikeExecutor>(true);
            if (!shrike)
            {
                CSDebug.LogWarning($"[Shrike] '{vesselImpactor.name}' collected a crystal but has no ShrikeExecutor - " +
                                   "is this effect wired into a non-Shrike container?", vesselImpactor);
                return;
            }
            shrike.CollectCrystal(data.Origin.Valid ? data.Origin.Position : vesselImpactor.transform.position);
        }
    }
}
