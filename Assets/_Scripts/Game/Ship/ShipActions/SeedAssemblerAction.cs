using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Reflection;

namespace CosmicShore
{
    public class SeedAssemblerAction : ShipAction
    {
        [SerializeField] float enhancementsPerFullAmmo = 3;
        TrailSpawner spawner;
        [SerializeField] Assembler assembler = new GyroidAssembler();
        [SerializeField] int depth = 50;

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
                var newAssembler = trailBlock.AddComponent(assembler.GetType()) as Assembler;

                newAssembler.Depth = depth;
                //CopyComponentValues(assembler, newAssembler);
                newAssembler.StartBonding();
            }
        }

        public override void StopAction() { }

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
