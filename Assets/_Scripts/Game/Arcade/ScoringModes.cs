using System;

namespace CosmicShore.Game.Arcade
{
    public enum ScoringModes
    {
        HostileVolumeDestroyed = 0,
        VolumeCreated = 1,
        TimePlayed = 2,
        TurnsPlayed = 3,
        VolumeStolen = 4,
        BlocksStolen = 5,
        TeamVolumeDifference = 6,
        CrystalsCollected = 7,
        OmniCrystalsCollected = 8,
        ElementalCrystalsCollected = 9,
        CrystalsCollectedScaleWithSize = 10,
        FriendlyVolumeDestroyed = 11,
        PrismsCreated = 12,
        HostilePrismsDestroyed = 13,
        FriendlyPrismsDestroyed = 14,
        LifeFormsKilled = 15,                   
        ElementalCrystalsCollectedBlitz = 16,
    }
}