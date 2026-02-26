using CosmicShore.Models.Enums;
using System;

namespace CosmicShore.Models.Structs
{
    [Serializable]
    public struct GameplayReward
    {
        public int ScoreRequirement;
        public int Value;
        public Element Element;
        public GameModes GameMode;
    }
}
