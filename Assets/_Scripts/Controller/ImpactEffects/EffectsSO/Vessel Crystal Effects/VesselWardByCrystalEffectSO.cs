using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A crystal pickup that hands the collecting pilot a TEMPORARY elemental-debuff ward.
    ///
    /// <para>This is the Sparrow's new answer to "what is an omni crystal for?". Its missiles
    /// used to be crystal-stocked; they are now stocked by destroying mass
    /// (<see cref="VesselRearmOnPrismDestruction"/>), which freed the crystal to say something
    /// else — a few seconds during which danger prisms, blasts and overtakes leave your element
    /// levels alone. Buffs still land; the ward only ever drops NEGATIVE
    /// <see cref="ResourceSystem.ApplyElementalEffect"/> calls, and it PREVENTS new debuffs
    /// rather than cleansing live ones.</para>
    ///
    /// <para>The SO stays stateless, as every action/effect asset must: the timer lives on the
    /// vessel's own <see cref="VesselTimedElementalWard"/>, which owns the grant, the countdown
    /// and the revoke-on-disable. This effect only decides HOW LONG; WHAT is warded is that
    /// component's authored promise.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselWardByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/VesselWardByCrystalEffectSO")]
    public class VesselWardByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Tooltip("Seconds of elemental-debuff immunity this pickup grants. Re-collecting " +
                 "REFRESHES the window rather than stacking it (VesselTimedElementalWard.Grant " +
                 "takes the longer of the two), so a crystal run cannot be banked into a " +
                 "permanent state.")]
        [SerializeField, Min(0f)] float wardSeconds = 8f;

        // Vessels already told about a missing ward component, so one unwired prefab names
        // itself once rather than once per pickup.
        static readonly System.Collections.Generic.HashSet<int> s_reported = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetReported() => s_reported.Clear();

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (!vesselImpactor) return;

            // The VesselImpactor lives on the vessel ROOT (the impact-effects contract), which is
            // also where the ward belongs — but fall back to a search so a differently-nested
            // hull still works rather than silently granting nothing.
            if (!vesselImpactor.TryGetComponent(out VesselTimedElementalWard ward))
                // includeInactive:false deliberately - a ward on an inactive object can never
                // count down or be revoked, so finding one would only ever produce a grant
                // that Grant() now declines. Not searching for it says the same thing sooner.
                ward = vesselImpactor.GetComponentInChildren<VesselTimedElementalWard>();

            if (!ward)
            {
                if (s_reported.Add(vesselImpactor.GetInstanceID()))
                    CSDebug.LogWarning($"[{nameof(VesselWardByCrystalEffectSO)}] '{name}' ran on " +
                        $"'{vesselImpactor.name}', which carries no {nameof(VesselTimedElementalWard)} " +
                        "— the pickup grants nothing. Add the component to that vessel's root.",
                        vesselImpactor);
                return;
            }

            ward.Grant(wardSeconds);
        }
    }
}
