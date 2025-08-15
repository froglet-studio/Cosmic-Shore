using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class R_FakeCrystalImpactor : R_CrystalImpactor
    {
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] fakeCrystalShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(R_IImpactEffect))]
        ScriptableObject[] fakeCrystalProjectileEffectsSO;
        
        R_IImpactEffect[] fakeCrystalShipEffects;
        R_IImpactEffect[] fakeCrystalProjectileEffects;
        
        void Awake()
        {
            fakeCrystalShipEffects = Array.ConvertAll(fakeCrystalShipEffectsSO, so => so as R_IImpactEffect);
            fakeCrystalProjectileEffects = Array.ConvertAll(fakeCrystalProjectileEffectsSO, so => so as R_IImpactEffect);
        }
        
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