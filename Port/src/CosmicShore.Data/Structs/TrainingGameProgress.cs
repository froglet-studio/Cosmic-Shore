using System;
using System.Linq;

namespace CosmicShore.Data
{
    [Serializable]
    public struct TrainingGameProgress
    {
        public int CurrentIntensity { get; set; }
        public TrainingGameTier[] Progress { get; set; }

        // Port fix (documented in PORT_PLAN.md Deviations): in the Unity original,
        // `new TrainingGameProgress()` zero-initialized the struct (null Progress,
        // intensity 0) because C# 9 forbade parameterless struct ctors — the dummy-arg
        // ctor below was the workaround and TrainingGameProgressTests' documented
        // "fresh progress" contract was silently violated. C# 10+ allows the real fix.
        public TrainingGameProgress() : this(1, null) { }

        public TrainingGameProgress(int dummy1=1, TrainingGameTier[] dummy2=null)
        {
            CurrentIntensity = 1;
            Progress = new TrainingGameTier[4]
            { 
                new(),
                new(),
                new(),
                new(),
            };
        }

        public void SatisfyTier(int tier)
        {
            Progress[tier-1].Satisfied = true;
            CurrentIntensity = Math.Max(CurrentIntensity, tier);
        }

        public void ClaimTier(int tier)
        {
            Progress[tier - 1].Claimed = true;
        }

        public bool IsTierClaimed(int tier)
        {
            return Progress[tier - 1].Claimed;
        }

        public bool IsTierSatisfied(int tier)
        {
            return Progress[tier - 1].Satisfied;
        }
    }

    [Serializable]
    public struct TrainingGameTier
    { 
        public bool Satisfied;
        public bool Claimed;
    }
}