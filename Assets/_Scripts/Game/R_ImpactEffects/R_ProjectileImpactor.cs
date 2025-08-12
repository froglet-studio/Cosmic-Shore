using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ProjectileImpactor : R_ImpactorBase
    {
        [SerializeField] R_IImpactEffect[] projectileShipEffects;
        [SerializeField] R_IImpactEffect[] projectilePrismEffects;
        [SerializeField] R_IImpactEffect[] projectileFakeCrystalEffects;
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {    
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, projectileShipEffects);
                    break;
                case R_PrismImpactor prismImpactor:
                    ExecuteEffect(impactee, projectilePrismEffects);
                    break;
                case R_FakeCrystalImpactor fakeCrystalImpactor:
                    ExecuteEffect(impactee, projectileFakeCrystalEffects);
                    break;
            }
        }
    }
}