using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ExplosionImpactor : R_ImpactorBase
    {
        [SerializeField] R_IImpactEffect[] explosionShipEffects;
        [SerializeField] R_IImpactEffect[] explosionPrismEffects;
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {    
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, explosionShipEffects);
                    break;
                case R_PrismImpactor prismImpactor:
                    ExecuteEffect(impactee, explosionPrismEffects);
                    break;
            }
        }
    }
}