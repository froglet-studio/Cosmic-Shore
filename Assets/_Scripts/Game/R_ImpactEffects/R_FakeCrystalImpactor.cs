using UnityEngine;

namespace CosmicShore.Game
{
    public class R_FakeCrystalImpactor : R_ImpactorBase
    {
        [SerializeField] R_IImpactEffect[] fakeCrystalShipEffects;
        [SerializeField] R_IImpactEffect[] fakeCrystalProjectileEffects;
        
        protected override void AcceptImpactee(R_IImpactor impactee)
        {
            switch (impactee)
            {
                case R_ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, fakeCrystalShipEffects);
                    break;
                case R_ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, fakeCrystalProjectileEffects);
                    break;
            }
        }
    }
}