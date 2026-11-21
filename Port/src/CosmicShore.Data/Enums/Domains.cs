namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.
    //
    // Blue is the sentinel for "no team / not yet picked / neutral entity" and is NEVER
    // present in GameDataSO.ActiveDomains. The playable set is {Jade, Ruby, Gold} (indices
    // 0..2 in ActiveDomains, enum values 1, 2, 4).
    public enum Domains
    {
        Jade = 1,
        Ruby = 2,
        Blue = 3,
        Gold = 4,
    }
}
