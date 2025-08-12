using UnityEngine;

namespace CosmicShore.Game
{
    public class R_CrystalImpactor : R_ImpactorBase
    {
        [SerializeField]
        Crystal crystal;
        
        [SerializeField] R_IImpactEffect[] crystalShipEffects;
        [SerializeField] R_IImpactEffect[] crystalProjectileEffects;
        [SerializeField] R_IImpactEffect[] crystalSkimmerEffects;
        
        public Crystal Crystal => crystal;
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, crystalShipEffects);
                    crystal.ExecuteCommonVesselImpact(shipImpactor.Ship);
                    break;
                case R_ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, crystalProjectileEffects);
                    break;
                case R_SkimmerImpactor skimmerImpactor:
                    ExecuteEffect(impactee, crystalSkimmerEffects);
                    break;
            }
        }
    }
}