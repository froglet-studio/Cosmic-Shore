using System;

namespace CosmicShore.Game
{
    public interface IRoundStats
    {
        //──────────────────────────────────────────
        // EVENTS
        //──────────────────────────────────────────

        event Action<IRoundStats> OnAnyStatChanged;
        event Action OnScoreChanged;

        // Prism count events
        event Action<IRoundStats> OnBlocksCreatedChanged;
        event Action<IRoundStats> OnBlocksDestroyedChanged;
        event Action<IRoundStats> OnBlocksRestoredChanged;
        event Action<IRoundStats> OnPrismsStolenChanged;
        event Action<IRoundStats> OnPrismsRemainingChanged;
        event Action<IRoundStats> OnFriendlyPrismsDestroyedChanged;
        event Action<IRoundStats> OnHostilePrismsDestroyedChanged;

        // Volume events
        event Action<IRoundStats> OnVolumeCreatedChanged;
        event Action<IRoundStats> OnTotalVolumeDestroyedChanged;
        event Action<IRoundStats> OnFriendlyVolumeDestroyedChanged;
        event Action<IRoundStats> OnHostileVolumeDestroyedChanged;
        event Action<IRoundStats> OnVolumeRestoredChanged;
        event Action<IRoundStats> OnVolumeStolenChanged;
        event Action<IRoundStats> OnVolumeRemainingChanged;

        // Crystal events
        event Action<IRoundStats> OnCrystalsCollectedChanged;
        event Action<IRoundStats> OnOmniCrystalsCollectedChanged;
        event Action<IRoundStats> OnElementalCrystalsCollectedChanged;

        event Action<IRoundStats> OnChargeCrystalValueChanged;
        event Action<IRoundStats> OnMassCrystalValueChanged;
        event Action<IRoundStats> OnSpaceCrystalValueChanged;
        event Action<IRoundStats> OnTimeCrystalValueChanged;

        // Misc events
        event Action<IRoundStats> OnSkimmerShipCollisionsChanged;

        // Ability time events
        event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;

        //──────────────────────────────────────────
        // PROPERTIES
        //──────────────────────────────────────────

        string Name { get; set; }
        Domains Domain { get; set; }

        float Score { get; set; }

        // Prism counts
        int BlocksCreated { get; set; }
        int BlocksDestroyed { get; set; }
        int BlocksRestored { get; set; }
        int PrismStolen { get; set; }
        int PrismsRemaining { get; set; }
        int FriendlyPrismsDestroyed { get; set; }
        int HostilePrismsDestroyed { get; set; }

        // Volumes
        float VolumeCreated { get; set; }
        float TotalVolumeDestroyed { get; set; }
        float VolumeRestored { get; set; }
        float VolumeStolen { get; set; }
        float VolumeRemaining { get; set; }
        float FriendlyVolumeDestroyed { get; set; }
        float HostileVolumeDestroyed { get; set; }

        // Crystals
        int CrystalsCollected { get; set; }
        int OmniCrystalsCollected { get; set; }
        int ElementalCrystalsCollected { get; set; }

        float ChargeCrystalValue { get; set; }
        float MassCrystalValue { get; set; }
        float SpaceCrystalValue { get; set; }
        float TimeCrystalValue { get; set; }

        // Other stats
        int SkimmerShipCollisions { get; set; }

        // Ability active times
        float FullSpeedStraightAbilityActiveTime { get; set; }
        float RightStickAbilityActiveTime { get; set; }
        float LeftStickAbilityActiveTime { get; set; }
        float FlipAbilityActiveTime { get; set; }
        float Button1AbilityActiveTime { get; set; }
        float Button2AbilityActiveTime { get; set; }
        float Button3AbilityActiveTime { get; set; }

        //──────────────────────────────────────────
        // RESET
        //──────────────────────────────────────────

        public void Cleanup()
        {
            Score = 0f;

            BlocksCreated = 0;
            BlocksDestroyed = 0;
            BlocksRestored = 0;
            PrismStolen = 0;
            PrismsRemaining = 0;
            FriendlyPrismsDestroyed = 0;
            HostilePrismsDestroyed = 0;

            VolumeCreated = 0f;
            TotalVolumeDestroyed = 0f;
            VolumeRestored = 0f;
            VolumeStolen = 0f;
            VolumeRemaining = 0f;
            FriendlyVolumeDestroyed = 0f;
            HostileVolumeDestroyed = 0f;

            CrystalsCollected = 0;
            OmniCrystalsCollected = 0;
            ElementalCrystalsCollected = 0;

            ChargeCrystalValue = 0f;
            MassCrystalValue = 0f;
            SpaceCrystalValue = 0f;
            TimeCrystalValue = 0f;

            SkimmerShipCollisions = 0;

            FullSpeedStraightAbilityActiveTime = 0f;
            RightStickAbilityActiveTime = 0f;
            LeftStickAbilityActiveTime = 0f;
            FlipAbilityActiveTime = 0f;
            Button1AbilityActiveTime = 0f;
            Button2AbilityActiveTime = 0f;
            Button3AbilityActiveTime = 0f;
        }
    }

    public struct DomainStats
    {
        public Domains Domain;
        public float Score;
    }
}
