using System;
using CosmicShore.Models.Enums;

namespace CosmicShore.Models.Structs
{
    [Serializable]
    public struct DailyChallenge
    {
        public int Intensity;
        public GameModes GameMode;
    }
}
