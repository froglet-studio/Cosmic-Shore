using System;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SkimmerStealPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerStealPrismEffectSO")]
    public class SkimmerStealPrismEffectSO : SkimmerPrismEffectSO
    {
        public static event Action<string> OnSkimmerStolenPrism;

        [Tooltip("Level-5 'Iron Grip': OFF by default, so a vessel whose steal has no shield " +
                 "clause behaves exactly as before. On, a shielded prism is stolen OUTRIGHT and " +
                 "keeps its armour once the superStealElement upgrade is live, instead of merely " +
                 "being stripped of the shield. Super-shielded mass is refused either way.")]
        [SerializeField] private bool superStealEnabled = false;

        [Tooltip("Which element's level-5 upgrade unlocks the shield-piercing steal. Read only " +
                 "when superStealEnabled is on.")]
        [SerializeField] private Element superStealElement = Element.Space;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Skimmer.VesselStatus;

            // Routed through IsUpgradeActive (the replicated NetElementUnlocks bit) rather than a
            // raw level read: this changes who OWNS a prism, which is an outcome every peer has
            // to agree on. Per-hit snapshot; the SO stays stateless.
            bool superSteal = superStealEnabled
                              && status?.ElementalAbilityHandler?.IsUpgradeActive(superStealElement) == true;

            PrismEffectHelper.Steal(prismImpactee, status, superSteal);

            OnSkimmerStolenPrism?.Invoke(status.PlayerName);
        }
    }
}
