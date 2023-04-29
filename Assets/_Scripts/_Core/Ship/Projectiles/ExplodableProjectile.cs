using UnityEngine;

namespace StarWriter.Core
{
    public class ExplodableProjectile : Projectile
    {
        [SerializeField] AOEExplosion AOEPrefab;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;
        public float Charge = 0;

        protected override void OnTriggerEnter(Collider other)
        {
            Destroy(gameObject);
        }

        public void OnDestroy()
        {
            var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
            AOEExplosion.Ship = Ship;
            AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
            AOEExplosion.MaxScale = (Charge * (maxExplosionScale - minExplosionScale)) + minExplosionScale; 
        }
    }
}