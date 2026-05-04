namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.
    //
    // Blue (4) is the sentinel for "no team / not yet picked / neutral entity" and is NEVER
    // present in GameDataSO.ActiveDomains. The playable set is {Jade, Ruby, Gold} keyed by
    // index 0..2, with enum values 1..3 (one-off from the index).
    public enum Domains
    {
        Jade = 1,
        Ruby = 2,
        Gold = 3,
        Blue = 4,
    }
}
