using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Each skim hit forwards the impactee to OverchargeAction on the ship hierarchy.
    /// </summary>
    [CreateAssetMenu(fileName = "SkimmerOverchargeCollectPrismEffect", menuName = "ScriptableObjects/Impact Effects/SkimmerOverchargeCollectPrismEffectSO")]
    public class SkimmerOverchargeCollectPrismEffectSO : ImpactEffectSO<R_SkimmerImpactor, R_PrismImpactor>
    {
        [SerializeField] bool verbose = false;

        protected override void ExecuteTyped(R_SkimmerImpactor impactor, R_PrismImpactor prismImpactee)
        {
     
            if (verbose) Debug.Log(
                $"[OverchargeCollectEffect] Registered impactee {impactor.name}",
                prismImpactee);
        }
    }
}