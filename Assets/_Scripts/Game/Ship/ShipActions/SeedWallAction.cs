using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using CosmicShore.Core;

namespace CosmicShore
{
    public class SeedWallAction : ShipAction
    {
        [SerializeField] float enhancementsPerFullAmmo = 3;
        TrailSpawner spawner;

        [SerializeField] int resourceIndex = 0;
        float resourceCost;

        protected override void Start()
        {
            base.Start();
            spawner = ship.GetComponent<TrailSpawner>();
            resourceCost = resourceSystem.Resources[resourceIndex].MaxAmount / enhancementsPerFullAmmo;
        }

        public override void StartAction()
        {
            if (resourceSystem.Resources[resourceIndex].CurrentAmount > resourceCost)
            {
                resourceSystem.ChangeResourceAmount(resourceIndex, -resourceCost);
                var BlockObject = spawner.Trail.TrailList.Last().gameObject;
                BlockObject.GetComponent<TrailBlock>().ActivateSuperShield();
                var assembler = BlockObject.AddComponent<GyroidAssembler>();
                assembler.Depth = 50;
                assembler.StartBonding();
            }
        }

        public override void StopAction() {}
    }
}
