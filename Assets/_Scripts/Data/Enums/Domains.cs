namespace CosmicShore.Data
{
    // Remember folks, only you can prevent Unity from arbitrarily swapping enum values in files.
    // Always assign a static numeric value to your enum types.
    //
    // Blue is the sentinel for "no team / not yet picked / neutral entity" and is NEVER
    // present in GameDataSO.ActiveDomains. The playable set is
    // {Jade, Ruby, Gold, Amethyst} (indices 0..3 in ActiveDomains, enum values 1, 2, 4, 5).
    //
    // Amethyst is the fourth playable domain. Blue was deliberately NOT promoted to a
    // playable team: neutral mass (GyroidAssembler prisms, uncommitted crystals, the
    // wildcard density-grid bucket) is tagged Blue and renders with the Blue material
    // set, so a playable Blue would be visually indistinguishable from neutral mass.
    public enum Domains
    {
        Jade = 1,
        Ruby = 2,
        Blue = 3,
        Gold = 4,
        Amethyst = 5,
    }
}
