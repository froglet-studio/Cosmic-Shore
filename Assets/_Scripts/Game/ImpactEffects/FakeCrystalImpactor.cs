using System;
using UnityEngine;

namespace CosmicShore.Game
{
    public class FakeCrystalImpactor : CrystalImpactor
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] fakeCrystalShipEffectsSO;
        
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] fakeCrystalProjectileEffectsSO;
        
        IImpactEffect[] fakeCrystalShipEffects;
        IImpactEffect[] fakeCrystalProjectileEffects;
        
        protected virtual void Awake()
        {
            base.Awake();
            fakeCrystalShipEffects = Array.ConvertAll(fakeCrystalShipEffectsSO, so => so as IImpactEffect);
            fakeCrystalProjectileEffects = Array.ConvertAll(fakeCrystalProjectileEffectsSO, so => so as IImpactEffect);
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, fakeCrystalShipEffects);
                    break;
                case ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, fakeCrystalProjectileEffects);
                    break;
            }
        }
    }
}