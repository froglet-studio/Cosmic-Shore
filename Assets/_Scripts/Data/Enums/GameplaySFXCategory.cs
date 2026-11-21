using System;

namespace CosmicShore.Core
{
    // Lives in the CosmicShore.Data ASSEMBLY (a dependency-free payload) but keeps its
    // CosmicShore.Core namespace, so no consumer changes. Docs/ASSEMBLY_SPLIT.md, Phase 2 step 1.
    [Serializable]
    public enum GameplaySFXCategory
    {
        BlockDestroy = 1,
        ShieldActivate = 2,
        ShieldDeactivate = 3,
        MineExplode = 4,
        ProjectileLaunch = 5,
        CrystalCollect = 6,
        VesselImpact = 7,
        GameEnd = 8,
        ScoreReveal = 9,
        PauseOpen = 10,
        PauseClose = 11,
        GunFire = 12,
        BoostActivate = 13,
        Explosion = 14,
        CreatureDeath = 15,
        DriftStart = 16,
        DriftEnd = 17,
        EnergyGain = 18,
        SpeedBurst = 19,
        CrystalSkim = 20,
        JoustScored = 21,
        JoustReceived = 22,
        ElementChargeReceived = 23,
        ElementMassReceived = 24,
        ElementSpaceReceived = 25,
        ElementTimeReceived = 26,
        ComebackCharge = 27,
        ComebackMass = 28,
        ComebackSpace = 29,
        ComebackTime = 30,
        JoustBuffCharge = 31,
        JoustBuffMass = 32,
        JoustBuffSpace = 33,
        JoustBuffTime = 34,
        TrackImpact = 35,
        FloraCollision = 36,
        CreatureBlockHit = 37,
    }
}
