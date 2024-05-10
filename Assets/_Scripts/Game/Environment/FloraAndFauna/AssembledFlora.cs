using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class AssembledFlora : Flora
    {
        [SerializeField] Assembler assembler = new GyroidAssembler();
        [SerializeField] int depth = 50;

        public override void Grow()
        {
            throw new System.NotImplementedException();
        }

        public override void Plant()
        {
            var newAssembler = healthBlock.gameObject.AddComponent(assembler.GetType()) as Assembler;
            newAssembler.Depth = depth;
            newAssembler.StartBonding();
        }

    }
}
