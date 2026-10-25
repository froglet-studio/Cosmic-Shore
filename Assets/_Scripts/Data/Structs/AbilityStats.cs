using System;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    // Lives in the CosmicShore.Data ASSEMBLY (a dependency-free payload) but keeps its
    // CosmicShore.Gameplay namespace, so no consumer changes. Docs/ASSEMBLY_SPLIT.md, Phase 2 step 1.
    [Serializable]
    public struct AbilityStats
    {
        public string PlayerName;
        public InputEvents ControlType;
        public float Duration;
    }
}
