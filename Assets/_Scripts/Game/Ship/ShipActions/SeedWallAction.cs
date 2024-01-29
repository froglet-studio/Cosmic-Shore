using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game.Projectiles;
using System.Linq;
using CosmicShore.Utility.ClassExtensions;

namespace CosmicShore
{
    public class SeedWallAction : ShipAction
    {
        [SerializeField] float enhancementsPerFullAmmo = 3;
        TrailSpawner spawner;

        protected override void Start()
        {
            base.Start();
            spawner = ship.GetComponent<TrailSpawner>();
        }

        public override void StartAction()
        {
            if (resourceSystem.CurrentAmmo > resourceSystem.MaxAmmo / enhancementsPerFullAmmo)
            {
                resourceSystem.ChangeAmmoAmount(-resourceSystem.MaxAmmo / enhancementsPerFullAmmo);
                var trailBlock = spawner.Trail.TrailList.Last().gameObject;
                trailBlock.AddComponent<GyroidAssembler>();
                trailBlock.GetComponent<GyroidAssembler>().StartBonding();
            }
        }

        public override void StopAction()
        {

        }
    }
}
