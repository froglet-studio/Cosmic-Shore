// Extracted verbatim from Assets/_Scripts/System/Audio/AudioSystem.cs (the category
// enums only). The AudioSystem class ports in the presentation phase with the
// first-party audio backend.
using System;

namespace CosmicShore.Core
{
    [Serializable]
    public enum MenuAudioCategory
    {
        OptionClick = 1,
        OpenView = 2,
        SwitchView = 3,
        CloseView = 4,
        SmallReward = 5,
        BigReward = 6,
        Upgrade = 7,
        Denied = 8,
        Confirmed = 9,
        LetsGo = 10,
        SwitchScreen = 11,
        RedeemTicket = 12,
    }

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
    }
}
