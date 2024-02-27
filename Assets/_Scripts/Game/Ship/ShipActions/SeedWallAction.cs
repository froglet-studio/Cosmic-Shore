using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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
                var assembler = trailBlock.AddComponent<GyroidAssembler>();
                assembler.Depth = 50;
                assembler.StartBonding();
            }
        }

        public override void StopAction() {}
    }
}
