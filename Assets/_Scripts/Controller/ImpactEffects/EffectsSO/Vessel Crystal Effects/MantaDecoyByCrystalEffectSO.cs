using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's MASS ability (MANTA_STING_KABLOOM.md §4a): a crystal the Manta collects leaves a decoy where it was.
    /// Authored in all four of <c>MantaImpactorDataContainer</c>'s elemental crystal lists (the base ability: every
    /// elemental crystal leaves an elemental decoy) and in its omni list (the Mass 5 upgrade: the omni leaves an omni
    /// decoy too - the bay checks the upgrade). One of each at a time; the bay pops the older one.
    ///
    /// Runs on every peer (the vessel-side crystal dispatch is broadcast), so every screen gets the same decoy; the same
    /// static per-impactor cooldown the Kabloom carries dedupes the broadcast + local double dispatch.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MantaDecoyByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/MantaDecoyByCrystalEffectSO")]
    public class MantaDecoyByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("Minimum seconds between decoys from the same vessel AND crystal - dedupes the broadcast+local " +
                 "double dispatch and multi-collider hulls alike.")]
        [SerializeField] float cooldownSeconds = 0.15f;

        static readonly Dictionary<(int, int), float> LastByImpactorAndCrystal = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => LastByImpactorAndCrystal.Clear();

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            var status = vesselImpactor?.Vessel?.VesselStatus;
            if (status == null || !data.Origin.Valid) return;
            if (!MantaStingActionExecutor.TryGetFor(status, out var bay)) return;

            var key = (vesselImpactor.GetInstanceID(), data.Origin.CrystalId);
            float now = Time.time;
            if (LastByImpactorAndCrystal.TryGetValue(key, out float last) && now - last < cooldownSeconds) return;
            LastByImpactorAndCrystal[key] = now;

            bay.LeaveDecoy(data);
        }
    }
}
