using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore
{
    public abstract class Assembler : MonoBehaviour
    {
        public abstract Prism Prism { get; set; }
        public abstract Spindle Spindle { get; set; }

        public abstract bool IsFullyBonded();
        public abstract GrowthInfo GetGrowthInfo();
        public virtual void SeedBonding() { StartBonding(); }
        public abstract void StartBonding();
        public virtual void StopBonding() { }
        public abstract int Depth { get; set; }
    }
}
