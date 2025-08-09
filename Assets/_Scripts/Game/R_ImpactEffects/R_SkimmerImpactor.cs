using UnityEngine;

namespace CosmicShore.Game
{
    public class R_SkimmerImpactor : R_ImpactorBase
    {
        [SerializeField] R_IImpactEffect[] skimmerShipEffects;
        [SerializeField] R_IImpactEffect[] skimmerPrismEffects;
        [SerializeField] R_IImpactEffect[] skimmerElementalCrystalEffects;
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {    
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, skimmerShipEffects);
                    break;
                case R_PrismImpactor prismImpactor:
                    ExecuteEffect(impactee, skimmerPrismEffects);
                    break;
                case R_ElementalCrystalImpactor elementalCrystalImpactor:
                    ExecuteEffect(impactee, skimmerElementalCrystalEffects);
                    break;
            }
        }
    }
}