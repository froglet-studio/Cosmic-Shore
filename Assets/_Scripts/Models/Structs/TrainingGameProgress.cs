﻿using System;

namespace CosmicShore.Models
{
    [Serializable]
    public struct TrainingGameProgress
    {
        public int CurrentIntensity { get; private set; }
        public TrainingGameTier[] Progress { get; private set; }

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
    }

    [Serializable]
    public struct TrainingGameTier
    { 
        public bool Satisfied;
        public bool Claimed;
    }
}