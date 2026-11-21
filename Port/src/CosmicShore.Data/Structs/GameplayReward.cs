using CosmicShore.Data;
using System;

namespace CosmicShore.Data
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
