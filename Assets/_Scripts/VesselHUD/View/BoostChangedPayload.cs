using CosmicShore.Models.Enums;

﻿using System;

namespace CosmicShore.Game
{
    [Serializable]
    public struct BoostChangedPayload
    {
        public float BoostMultiplier;
        public float MaxMultiplier;
        public Domains SourceDomain;
    }
}
