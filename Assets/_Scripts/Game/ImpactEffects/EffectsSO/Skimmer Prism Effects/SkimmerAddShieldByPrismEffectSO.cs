using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "SkimmerAddShieldByPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Prism/SkimmerAddShieldByPrismEffectSO")]
    public class SkimmerAddShieldByPrismEffectSO : SkimmerPrismEffectSO
    {
        [SerializeField] private int shieldIndex = 0;

        [Tooltip("How many WORLD scale units to add per prism hit")]
        [SerializeField] private float addScaleUnits = 5f;

        const float BASE_SCALE     = 30f;
        const float MAX_SCALE      = 120f;
        const float PRISM_MAX_SCALE= 100f;

        public override void Execute(SkimmerImpactor impactor, PrismImpactor prismImpactee)
        {
            var vs = impactor?.Skimmer?.VesselStatus;
            var rs = vs?.ResourceSystem;
            if (!rs) return;

            Domains skimmerDomain = vs.Domain;
            Domains prismDomain   = prismImpactee.Prism.Domain;
            if (prismDomain == skimmerDomain) return;

            float range   = MAX_SCALE - BASE_SCALE;
            float step01  = addScaleUnits / range;
            float cap01   = (PRISM_MAX_SCALE - BASE_SCALE) / range;

            var r = rs.Resources[shieldIndex];
            float current01 = Mathf.Clamp01(r.CurrentAmount);

            if (current01 >= 0.999f)
                current01 = cap01;

            float next01 = Mathf.Min(current01 + step01, cap01);
            rs.SetResourceAmount(shieldIndex, next01);
        }
    }
}