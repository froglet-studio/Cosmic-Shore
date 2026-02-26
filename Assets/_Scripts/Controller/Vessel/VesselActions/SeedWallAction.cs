using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;


namespace CosmicShore.Gameplay
{
    public class SeedWallAction : ShipAction
    {
        [SerializeField] float enhancementsPerFullAmmo = 3;
        VesselPrismController controller;

        [SerializeField] int resourceIndex = 0;
        float resourceCost;

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            controller = Vessel.VesselStatus.VesselPrismController;
            resourceCost = ResourceSystem.Resources[resourceIndex].MaxAmount / enhancementsPerFullAmmo;
        }

        public override void StartAction()
        {
            if (ResourceSystem.Resources[resourceIndex].CurrentAmount > resourceCost)
            {
                ResourceSystem.ChangeResourceAmount(resourceIndex, -resourceCost);
                var BlockObject = controller.Trail.TrailList.Last().gameObject;
                BlockObject.GetComponent<Prism>().ActivateSuperShield();
                var assembler = BlockObject.AddComponent<GyroidAssembler>();
                assembler.Depth = 50;
                assembler.StartBonding();
            }
        }

        public override void StopAction() {}
    }
}