using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class ExplodableProjectile : Projectile
    {
        [SerializeField] List<AOEExplosion> AOEPrefabs;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        public float Charge = 0;

        public void Detonate()
        {
            GetComponentInParent<PoolManager>().ReturnToPool(gameObject, gameObject.tag);
            foreach (var AOE in AOEPrefabs)
            {
                var AOEExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                AOEExplosion.Ship = Ship;
                AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                AOEExplosion.MaxScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, Charge);
            }
        }
    }
}