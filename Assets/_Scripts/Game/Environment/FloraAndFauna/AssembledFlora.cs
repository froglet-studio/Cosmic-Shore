using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// A flora that uses an <c cref="Assembler">Assembler</c> to define its growth pattern
    /// </summary>
    public class AssembledFlora : Flora
    {
        // TODO: we want to serialize a congifurable assembler, but currently only use the type
        /// <summary>
        /// The assembler that defines the growth pattern of this flora's health blocks and spindles
        /// </summary>
        [SerializeField] Assembler assemblerTemplate = new GyroidAssembler();
        /// <summary>
        /// The max recursion depth of the assembler
        /// </summary>
        [SerializeField] int depth = 50;
        /// <summary>
        /// does this flora either feed on other blocks or grow on its own?
        /// </summary>
        [SerializeField] bool feeds = false;
        /// <summary>
        /// the assembler that is actually used to grow this flora
        /// </summary>
        Assembler assembler;

        public override void Grow()
        {
            if (feeds) return; // if it feeds, it doesn't grow
            assembler.Grow();
        }

        public override void Plant()
        {
            assembler = healthBlock.gameObject.AddComponent(assemblerTemplate.GetType()) as Assembler;
            assembler.Depth = depth;
            if (feeds) assembler.StartBonding();
        }
    }
}
