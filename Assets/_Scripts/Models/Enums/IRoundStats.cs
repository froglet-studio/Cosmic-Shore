namespace CosmicShore.Core
{
    public interface IRoundStats
    {
        string Name { get; set; }
        Teams Team { get; set; }
        float Score { get; set; }
        int BlocksCreated { get; set; }
        int BlocksDestroyed { get; set; }
        int BlocksRestored { get; set; }
        int BlocksStolen { get; set; }
        int PrismsRemaining { get; set; }
        int FriendlyPrismsDestroyed { get; set; }
        int HostilePrismsDestroyed { get; set; }
        float VolumeCreated { get; set; }
        float VolumeDestroyed { get; set; }
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
    }

}