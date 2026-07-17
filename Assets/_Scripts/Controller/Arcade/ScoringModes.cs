using System;

namespace CosmicShore.Gameplay
{
    public enum ScoringModes
    {
        HostileVolumeDestroyed = 0,
        VolumeCreated = 1,
        TimePlayed = 2,
        // 3-6 retired (TurnsPlayed, VolumeStolen, BlocksStolen, TeamVolumeDifference):
        // strategies threw NotImplementedException and no content ever authored them. Do not reuse the IDs.
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