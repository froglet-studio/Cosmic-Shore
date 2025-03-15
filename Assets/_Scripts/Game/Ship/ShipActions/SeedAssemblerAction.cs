using UnityEngine;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;


namespace CosmicShore
{
    public class SeedAssemblerAction : ShipAction
    {
        float enhancementsPerFullAmmo = 4;
        TrailSpawner spawner;
        [SerializeField] Assembler assembler;
        [SerializeField] int depth = 50;

        [SerializeField] int resourceIndex = 0;
        Assembler currentAssembler;

        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);
            spawner = Ship.ShipStatus.TrailSpawner;
        }

        public override void StartAction()
        {
            float ammoRequiredPerUse = ResourceSystem.Resources[resourceIndex].MaxAmount / enhancementsPerFullAmmo;

            if (ResourceSystem.Resources[resourceIndex].CurrentAmount >= ammoRequiredPerUse)
            {
                ResourceSystem.ChangeResourceAmount(resourceIndex, -ammoRequiredPerUse);
                var trailBlock = spawner.Trail.TrailList.Last().gameObject;
                
                var newAssembler = trailBlock.AddComponent(assembler.GetType()) as Assembler;
                newAssembler.Depth = depth;
                currentAssembler = newAssembler;
            }
        }

        public override void StopAction() 
        {
            if (currentAssembler == null) return;
            var seed = currentAssembler.GetComponent<TrailBlock>();
            seed.ActivateSuperShield();
            seed.transform.localScale *= 2f;
            currentAssembler.SeedBonding();
            currentAssembler = null;
        }

        //void CopyComponentValues(Assembler sourceComp, Assembler targetComp)
        //{
        //    FieldInfo[] sourceFields = sourceComp.GetType().GetFields(BindingFlags.Public |
        //                                                  BindingFlags.NonPublic |
        //                                                  BindingFlags.Instance);
        //    for (var i = 0; i < sourceFields.Length; i++)
        //    {
        //        var value = sourceFields.GetValue(i);
        //        sourceFields.SetValue(value, i);
        //    }
        //}
    }
}
