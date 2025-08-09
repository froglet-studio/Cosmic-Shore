using UnityEngine;

namespace CosmicShore.Game
{
    public class R_ShipImpactor : R_ImpactorBase
    {
        [SerializeField] R_IImpactEffect[] shipPrismEffects;
        [SerializeField] R_IImpactEffect[] shipOmniCrystalEffects;
        [SerializeField] R_IImpactEffect[] shipElementalCrystalEffects;
        [SerializeField] R_IImpactEffect[] shipFakeCrystalEffects;
        [SerializeField] R_IImpactEffect[] shipProjectileEffects;
        [SerializeField] R_IImpactEffect[] shipSkimmerEffects;
        [SerializeField] R_IImpactEffect[] shipExplosionEffects;

        protected override void AcceptImpactee(R_IImpactor impactee)
        {
            switch (impactee)
            {
                case R_PrismImpactor prismImpactor:
                    ExecuteEffect(impactee, shipPrismEffects);
                    break;
                case R_OmniCrystalImpactor omniCrystalImpactor:
                    ExecuteEffect(impactee, shipOmniCrystalEffects);
                    break;
                case R_ElementalCrystalImpactor elementalCrystalImpactor:
                    ExecuteEffect(impactee, shipElementalCrystalEffects);
                    break;
                case R_FakeCrystalImpactor fakeCrystalImpactor:
                    ExecuteEffect(impactee, shipFakeCrystalEffects);
                    break;
                case R_ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, shipProjectileEffects);
                    break;
                case R_SkimmerImpactor shimmerImpactor:
                    ExecuteEffect(impactee, shipSkimmerEffects);
                    break;
                case R_ExplosionImpactor explosionImpactor:
                    ExecuteEffect(impactee, shipExplosionEffects);
                    break;
            }
        }
    }
}