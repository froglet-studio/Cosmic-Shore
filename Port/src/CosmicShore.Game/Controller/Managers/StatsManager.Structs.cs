// Extracted verbatim from Assets/_Scripts/Controller/Managers/StatsManager.cs (the
// stat payload structs only). The StatsManager class itself ports in phase 2; when it
// does, it joins this folder and these structs stay here.
using System;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public struct CellStats
    {
        public int LifeFormsInCell;
        public int LiveBlockCount;
        public CellPhase Phase;
        public Domains DominantDomain;
    }

    [Serializable]
    public struct CrystalStats
    {
        public string PlayerName;
        public Element Element;
        public float Value;
    }

    [Serializable]
    public struct PrismStats
    {
        public string OwnName;
        public float Volume;
        public string AttackerName;
    }

    [Serializable]
    public struct AbilityStats
    {
        public string PlayerName;
        public InputEvents ControlType;
        public float Duration;
    }
}
