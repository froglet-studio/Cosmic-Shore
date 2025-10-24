using System;

namespace CosmicShore.Game
{
    public class RoundStats : IRoundStats
    {
        public event Action OnScoreChanged;
        public event Action<IRoundStats> OnVolumeCreatedChanged;
        
        public string Name { get; set; }
        public Domains Domain { get; set; }
        float score;
        public float Score
        {
            get => score;
            set
            {
                score = value;
                OnScoreChanged?.Invoke();
            }
        }
        public int BlocksCreated { get; set; }
        public int BlocksDestroyed { get; set; }
        public int BlocksRestored { get; set; }
        public int PrismStolen { get; set; }
        public int PrismsRemaining { get; set; }
        public int FriendlyPrismsDestroyed { get; set; }
        public int HostilePrismsDestroyed { get; set; }
        float volumeCreated;
        public float VolumeCreated
        {
            get => volumeCreated;
            set
            {
                volumeCreated = value;
                OnVolumeCreatedChanged?.Invoke(this);
            }
        }
        public float VolumeDestroyed { get; set; }
        public float VolumeRestored { get; set; }
        public float VolumeStolen { get; set; }
        public float VolumeRemaining { get; set; }
        public float FriendlyVolumeDestroyed { get; set; }
        public float HostileVolumeDestroyed { get; set; }
        public int CrystalsCollected { get; set; }
        public int OmniCrystalsCollected { get; set; }
        public int ElementalCrystalsCollected { get; set; }
        public float ChargeCrystalValue { get; set; }
        public float MassCrystalValue { get; set; }
        public float SpaceCrystalValue { get; set; }
        public float TimeCrystalValue { get; set; }
        public int SkimmerShipCollisions { get; set; }
        public float FullSpeedStraightAbilityActiveTime { get; set; }
        public float RightStickAbilityActiveTime { get; set; }
        public float LeftStickAbilityActiveTime { get; set; }
        public float FlipAbilityActiveTime { get; set; }
        public float Button1AbilityActiveTime { get; set; }
        public float Button2AbilityActiveTime { get; set; }
        public float Button3AbilityActiveTime { get; set; }

        public RoundStats()
        {
            BlocksCreated = 0;
            BlocksDestroyed = 0;
            BlocksRestored = 0;
            PrismStolen = 0;
            PrismsRemaining = 0;
            FriendlyPrismsDestroyed = 0;
            HostilePrismsDestroyed = 0;
            VolumeCreated = 0;
            VolumeDestroyed = 0;
            VolumeRestored = 0;
            VolumeStolen = 0;
            VolumeRemaining = 0;
            FriendlyVolumeDestroyed = 0;
            HostileVolumeDestroyed = 0;
            CrystalsCollected = 0;
            OmniCrystalsCollected = 0;
            ElementalCrystalsCollected = 0;
            ChargeCrystalValue = 0;
            MassCrystalValue = 0;
            SpaceCrystalValue = 0;
            TimeCrystalValue = 0;
            SkimmerShipCollisions = 0;
            FullSpeedStraightAbilityActiveTime = 0;
            RightStickAbilityActiveTime = 0;
            LeftStickAbilityActiveTime = 0;
            FlipAbilityActiveTime = 0;
            Button1AbilityActiveTime = 0;
            Button2AbilityActiveTime = 0;
            Button3AbilityActiveTime = 0;
        }
    }
}