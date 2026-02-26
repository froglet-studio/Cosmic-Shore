using CosmicShore.Models.Enums;

namespace CosmicShore.Models.Structs
{
    ﻿using System;

    [Serializable]
    public struct DailyChallenge
    {
        public int Intensity;
        public GameModes GameMode;
    }
}
