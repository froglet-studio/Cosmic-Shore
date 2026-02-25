using CosmicShore.Models.Enums;

namespace CosmicShore.Models.Structs
{
    ﻿using System;

    [Serializable]
    public struct GameplayReward
    {
        public int ScoreRequirement;
        public int Value;
        public Element Element;
        public GameModes GameMode;
    }
}
