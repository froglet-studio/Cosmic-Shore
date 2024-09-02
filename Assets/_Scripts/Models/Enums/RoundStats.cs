namespace CosmicShore.Core
{
    [System.Serializable]
    public struct RoundStats
    {
        public int BlocksCreated;
        public int BlocksDestroyed;
        public int BlocksRestored;
        public int BlocksStolen;
        public int BlocksRemaining;
        public int FriendlyBlocksDestroyed;
        public int HostileBlocksDestroyed;
        public float VolumeCreated;
        public float VolumeDestroyed;
        public float VolumeRestored;
        public float VolumeStolen;
        public float VolumeRemaining;
        public float FriendlyVolumeDestroyed;
        public float HostileVolumeDestroyed;
        public int CrystalsCollected;
        public int OmniCrystalsCollected;
        public int ElementalCrystalsCollected;
        public float ChargeCrystalValue;
        public float MassCrystalValue;
        public float SpaceCrystalValue;
        public float TimeCrystalValue;
        public int SkimmerShipCollisions;
        public float FullSpeedStraightAbilityActiveTime;
        public float RightStickAbilityActiveTime;
        public float LeftStickAbilityActiveTime;
        public float FlipAbilityActiveTime;
        public float Button1AbilityActiveTime;
        public float Button2AbilityActiveTime;
        public float Button3AbilityActiveTime;
        

        public RoundStats(bool dummy = false)
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