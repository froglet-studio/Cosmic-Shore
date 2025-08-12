using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ElementalCrystalImpactor : R_ImpactorBase
    {
        [SerializeField] R_IImpactEffect[] elementalCrystalShipEffects;
        [SerializeField] R_IImpactEffect[] elementalCrystalSkimmerEffects;
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, elementalCrystalShipEffects);
                    break;
                case R_SkimmerImpactor skimmerImpactor:
                    ExecuteEffect(impactee, elementalCrystalSkimmerEffects);
                    break;
            }
        }
    }
}