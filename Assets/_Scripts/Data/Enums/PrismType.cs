namespace CosmicShore.Gameplay
{
    public enum PrismType
    {
        Dolphin = 0,
        Serpent = 1,
        Sparrow = 2,
        Manta = 3,
        Squirrel = 4,
        Rhino = 5,
        Interactive = 6,
        Explosion = 7,
        Implosion = 8,
        Grow = 9,
        // Fast-growing, collider-live-on-spawn prisms drawn from a dedicated pool: a
        // surface a skimmer can boost off, but usually flown past without a body hit
        // (Squirrel tube + joust danger-block formations). See PrismFactory.SpawnBoostPrism.
        Boost = 10,
    }
}
