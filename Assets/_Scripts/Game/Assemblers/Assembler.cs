using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore
{
    public abstract class Assembler : MonoBehaviour
    {
        public abstract TrailBlock TrailBlock { get; set; }
        public abstract Spindle Spindle { get; set; }

        public abstract bool FullyBonded { get; set; }
        public abstract TrailBlock ProgramBlock(TrailBlock trailBlock);
        public abstract void StartBonding();
        public abstract int Depth { get; set; }
    }
}
