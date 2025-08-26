namespace CosmicShore.Core
{
    [System.Serializable]
    public class RoundStats : IRoundStats
    {
        public string Name { get; set; }
        public Teams Team { get; set; }
        public float Score { get; set; }
        public int BlocksCreated { get; set; }
        public int BlocksDestroyed { get; set; }
        public int BlocksRestored { get; set; }
        public int BlocksStolen { get; set; }
        public int BlocksRemaining { get; set; }
        public int FriendlyBlocksDestroyed { get; set; }
        public int HostileBlocksDestroyed { get; set; }
        public float VolumeCreated { get; set; }
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
            BlocksStolen = 0;
            BlocksRemaining = 0;
            FriendlyBlocksDestroyed = 0;
            HostileBlocksDestroyed = 0;
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