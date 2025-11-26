using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent((typeof(Mine)))]
    public class MineImpactor : ImpactorBase
    {
        public Mine Mine;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] mineShipEffectsSO;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] explosionEffectsSO;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] mineProjectileEffectsSO;

        IImpactEffect[] mineShipEffects;
        IImpactEffect[] mineProjectileEffects;
        IImpactEffect[] explosionEffects;

        protected virtual void Awake()
        {
            Mine ??= GetComponent<Mine>();
            mineShipEffects = Array.ConvertAll(mineShipEffectsSO, so => so as IImpactEffect);
            mineProjectileEffects = Array.ConvertAll(mineProjectileEffectsSO, so => so as IImpactEffect);
            explosionEffects = Array.ConvertAll(explosionEffectsSO, so => so as IImpactEffect);
        }

        private void Reset()
        {
            Mine ??= GetComponent<Mine>();
        }

        protected override void AcceptImpactee(IImpactor impactee)
        {
            switch (impactee)
            {
                case ShipImpactor shipImpactor:
                    ExecuteEffect(impactee, mineShipEffects);
                    break;
                case ProjectileImpactor projectileImpactor:
                    ExecuteEffect(impactee, mineProjectileEffects);
                    break;
                case ExplosionImpactor explosionImpactor:
                    ExecuteEffect(impactee, explosionEffects);
                    break;
            }
        }
    }
}