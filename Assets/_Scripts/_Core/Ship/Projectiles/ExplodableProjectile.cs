using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class ExplodableProjectile : Projectile
    {
        [SerializeField] AOEExplosion AOEPrefab;
        [SerializeField] float minExplosionScale;
        [SerializeField] float maxExplosionScale;

        ResourceSystem resourceSystem;

        private void Start()
        {
            resourceSystem = Ship.ResourceSystem;
        }

        protected override void OnTriggerEnter(Collider other)
        {
            Destroy(gameObject);
        }

        public void OnDestroy()
        {
            var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
            AOEExplosion.Material = Ship.AOEExplosionMaterial;
            AOEExplosion.Team = Ship.Team;
            AOEExplosion.Ship = Ship;
            AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
            AOEExplosion.MaxScale = Mathf.Max(minExplosionScale, resourceSystem.CurrentAmmo * maxExplosionScale);

            if (AOEExplosion is AOEBlockCreation aoeBlockcreation)
                aoeBlockcreation.SetBlockMaterial(Ship.TrailSpawner.GetBlockMaterial());
        }

    }
}