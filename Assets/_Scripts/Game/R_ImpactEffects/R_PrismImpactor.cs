using UnityEngine;

namespace CosmicShore.Game
{
    public class R_PrismImpactor : R_ImpactorBase
    {
        [SerializeField] R_IImpactEffect[] prismShipEffects;
        [SerializeField] R_IImpactEffect[] prismProjectileEffects;
        [SerializeField] R_IImpactEffect[] prismSkimmerEffects;
        [SerializeField] R_IImpactEffect[] prismExplosionEffects;

        protected override void AcceptImpactee(R_IImpactor impactee)
        {    
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, prismShipEffects);
                    break;
                case R_ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, prismProjectileEffects);
                    break;
                case R_SkimmerImpactor skimmerImpactor:
                    ExecuteEffect(impactee, prismSkimmerEffects);
                    break;
                case R_ExplosionImpactor explosionImpactor:
                    ExecuteEffect(impactee, prismExplosionEffects);
                    break;
            }
        }
    }
}