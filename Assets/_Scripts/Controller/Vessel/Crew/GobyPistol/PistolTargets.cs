namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the Pistol may act on. The Goby's OWN trail is the ground it rides, never a target: not seen, not hit, not
    /// primed, not carried (the lab keeps its trail blocks out of the prism arrays for the same reason). Identity of the
    /// Trail, never the player's name: the hull's other lay sites (the Squirrel's tube) are friendly prisms like any other.
    /// Pure, so <c>PistolTargetsTests</c> can hold it without a Prism.
    /// </summary>
    public static class PistolTargets
    {
        public static bool IsOwnTrail(Trail prismTrail, Trail hullTrail, Trail hullSecondary)
        {
            if (prismTrail == null) return false;
            return (hullTrail != null && ReferenceEquals(prismTrail, hullTrail)) || (hullSecondary != null && ReferenceEquals(prismTrail, hullSecondary));
        }
    }
}
