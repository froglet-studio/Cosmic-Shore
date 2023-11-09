using StarWriter.Core;
using UnityEngine;

namespace _Scripts._Core.Ship.Projectiles
{
    public class ExplodableProjectile : Projectile
    {
        [SerializeField] AOEExplosion AOEPrefab;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        public float Charge = 0;

        public void Detonate()
        {
            GetComponentInParent<PoolManager>().ReturnToPool(gameObject, gameObject.tag);
            var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
            AOEExplosion.Ship = Ship;
            AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
            AOEExplosion.MaxScale = Mathf.Lerp(minExplosionScale, maxExplosionScale, Charge);
        }
    }
}