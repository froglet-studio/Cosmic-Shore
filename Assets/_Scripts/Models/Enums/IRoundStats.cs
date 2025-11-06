using System;

namespace CosmicShore.Game
{
    public interface IRoundStats
    {
        public event Action OnScoreChanged;
        public event Action<IRoundStats> OnVolumeCreatedChanged;
        public event Action<IRoundStats> OnTotalVolumeDestroyedChanged;
        public event Action<IRoundStats> OnFriendlyVolumeDestroyedChanged;
        public event Action<IRoundStats> OnHostileVolumeDestroyedChanged;
        
        string Name { get; set; }
        Domains Domain { get; set; }
        float Score { get; set; }
        int BlocksCreated { get; set; }
        int BlocksDestroyed { get; set; }
        int BlocksRestored { get; set; }
        int PrismStolen { get; set; }
        int PrismsRemaining { get; set; }
        int FriendlyPrismsDestroyed { get; set; }
        int HostilePrismsDestroyed { get; set; }
        float VolumeCreated { get; set; }
        float TotalVolumeDestroyed { get; set; }
        float VolumeRestored { get; set; }
        float VolumeStolen { get; set; }
        float VolumeRemaining { get; set; }
        float FriendlyVolumeDestroyed { get; set; }
        float HostileVolumeDestroyed { get; set; }
        int CrystalsCollected { get; set; }
        int OmniCrystalsCollected { get; set; }
        int ElementalCrystalsCollected { get; set; }
        float ChargeCrystalValue { get; set; }
        float MassCrystalValue { get; set; }
        float SpaceCrystalValue { get; set; }
        float TimeCrystalValue { get; set; }
        int SkimmerShipCollisions { get; set; }
        float FullSpeedStraightAbilityActiveTime { get; set; }
        float RightStickAbilityActiveTime { get; set; }
        float LeftStickAbilityActiveTime { get; set; }
        float FlipAbilityActiveTime { get; set; }
        float Button1AbilityActiveTime { get; set; }
        float Button2AbilityActiveTime { get; set; }
        float Button3AbilityActiveTime { get; set; }

        public void Cleanup()
        {
            // ints default to 0, floats to 0f
            Score = 0;

            BlocksCreated = BlocksDestroyed = BlocksRestored =
                PrismStolen = PrismsRemaining =
                    FriendlyPrismsDestroyed = HostilePrismsDestroyed =
                        CrystalsCollected = OmniCrystalsCollected = ElementalCrystalsCollected =
                            SkimmerShipCollisions = 0;

            VolumeCreated = TotalVolumeDestroyed = VolumeRestored =
                VolumeStolen = VolumeRemaining =
                    FriendlyVolumeDestroyed = HostileVolumeDestroyed =
                        ChargeCrystalValue = MassCrystalValue = SpaceCrystalValue = TimeCrystalValue =
                            FullSpeedStraightAbilityActiveTime = RightStickAbilityActiveTime =
                                LeftStickAbilityActiveTime = FlipAbilityActiveTime =
                                    Button1AbilityActiveTime = Button2AbilityActiveTime = Button3AbilityActiveTime = 0f;
        }
    }

}