using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CosmicShore
{
    public class SeedAssemblerAction : ShipAction
    {
        [SerializeField] float enhancementsPerFullAmmo = 3;
        TrailSpawner spawner;
        [SerializeField] Assembler assembler;
        System.Type type;

        protected override void Start()
        {
            base.Start();
            spawner = ship.GetComponent<TrailSpawner>();
            type = assembler.GetType();
        }

        public override void StartAction()
        {
            if (resourceSystem.CurrentAmmo > resourceSystem.MaxAmmo / enhancementsPerFullAmmo)
            {
                resourceSystem.ChangeAmmoAmount(-resourceSystem.MaxAmmo / enhancementsPerFullAmmo);
                var trailBlock = spawner.Trail.TrailList.Last().gameObject;
                //var assembler = trailBlock.AddComponent<type>();
                //assembler.Depth = 50;
                //assembler.StartBonding();
            }
        }

        public override void StopAction() { }
    }
}
