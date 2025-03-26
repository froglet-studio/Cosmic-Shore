using CosmicShore.App.Systems.Audio;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class ExplodableProjectile : Projectile
    {
        [SerializeField] List<AOEExplosion> AOEPrefabs;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        [SerializeField] AudioClip DetonateSound;
        public float Charge = 0;

        public void Detonate()
        {
            GetComponentInParent<PoolManager>().ReturnToPool(gameObject, gameObject.tag);
            if (DetonateSound != null)
                AudioSystem.Instance.PlaySFXClip(DetonateSound);

            foreach (var AOE in AOEPrefabs)
            {
                if (Ship == null)
                    return;

                var AOEExplosion = Instantiate(AOE).GetComponent<AOEExplosion>();
                AOEExplosion.Initialize(Ship);
                AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                AOEExplosion.MaxScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, Charge);
            }
        }
    }
}