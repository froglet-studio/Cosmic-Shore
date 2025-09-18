using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerChangeResourceByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerChangeResourceByPrismEffectSO")]
    public class SkimmerChangeResourceByPrismEffectSO : SkimmerPrismEffectSO 
    {
        [SerializeField] ResourceChangeSpec _change;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var rs = impactor.Skimmer.VesselStatus.ResourceSystem;
            _change.ApplyTo(rs, this);
        }
    }
}