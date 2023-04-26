using UnityEngine;

namespace StarWriter.Core
{
    public class ExplodableProjectile : Projectile
    {
        [SerializeField] AOEExplosion AOEPrefab;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;

        protected override void OnTriggerEnter(Collider other)
        {
            Destroy(gameObject);
        }

        public void OnDestroy()
        {
            var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
            AOEExplosion.Ship = Ship;
            AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
            AOEExplosion.MaxScale = Mathf.Max(minExplosionScale, Ship.ResourceSystem.CurrentAmmo * maxExplosionScale);
        }
    }
}