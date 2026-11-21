namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Something that knows a hull's TOP SPEED - the ceiling its own boost mechanic climbs to - so a
    /// rule that means "travelling fast" can be written as a FRACTION of it rather than as a number
    /// that is wrong the first time the boost is retuned. The Rhino's ramp
    /// (<see cref="RampBoostActionExecutor.TopSpeed"/>) is the one implementation today; the Time
    /// level-5 ward reads it (<see cref="VesselElementalImmunity.Condition.WhileAboveTopSpeedFraction"/>).
    /// </summary>
    public interface ITopSpeedSource
    {
        /// <summary>Top speed in u/s, or 0 while it is not known yet.</summary>
        float TopSpeed { get; }
    }
}
