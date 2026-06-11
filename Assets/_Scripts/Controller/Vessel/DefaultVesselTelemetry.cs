namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Default vessel telemetry for vessels that only track the common base stats
    /// (longest drift, max boost time, prisms damaged).
    /// Attach to any vessel prefab that doesn't need custom stats yet.
    /// </summary>
    public class DefaultVesselTelemetry : VesselTelemetry
    {
    }
}
