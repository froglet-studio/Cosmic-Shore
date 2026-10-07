namespace CosmicShore.Data
{
    // Always assign static numeric values; Unity serialization drift on enum reordering
    // breaks scene-wired references and SOAP asset references silently.
    //
    // HOW a lifeform came apart. Not a designer switch and not a per-prefab option: the
    // style is decided by the force that killed the creature, and the death animation
    // reads it back (Docs/ECOSYSTEM.md §26). Every style still runs the one sealed death
    // path - the elemental crystal always drops (mass conserved) and nothing ever pops
    // out of existence (continuity) - they differ only in WHERE the body comes apart and
    // WHEN the heart becomes collectable.
    //
    //   Withered - the ordinary death (starvation, and any death with no other cause on
    //              record). The body is spent from the OUTSIDE IN: the extremity spindles
    //              evaporate first and the heart is the last thing standing, so it only
    //              becomes collectable - by any vessel - once the wither reaches it. The
    //              body prisms are left behind as a skeleton.
    //   Jousted  - a vessel out-paced the creature and took its heart (the Squirrel's
    //              Crystal Joust). The mirror image of Withered: the heart leaves FIRST,
    //              awarded straight to the jouster, and the body unravels FROM THE HEART
    //              OUTWARD around the hole it left. The body prisms are left behind as a
    //              skeleton.
    //   Consumed - a predator caught and ate the creature. No skeleton and no ordering:
    //              the body breaks apart and suctions into the mouth, because here the
    //              mass is genuinely transferred to the eater rather than left in place.
    public enum LifeformDeathStyle
    {
        Withered = 0,
        Jousted = 1,
        Consumed = 2,
    }
}
