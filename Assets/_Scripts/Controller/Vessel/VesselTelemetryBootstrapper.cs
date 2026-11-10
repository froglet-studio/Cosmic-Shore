using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Ensures every vessel has a VesselTelemetry component at runtime.
    /// Attach to vessel prefabs in the Unity Editor (requires no serialized fields).
    /// On Awake, if no telemetry component exists, adds the correct type
    /// and injects gameData from the VesselController on the same GameObject.
    ///
    /// <see cref="VesselController.Initialize"/> calls <see cref="Ensure"/> as well, so a hull whose prefab carries
    /// neither a telemetry nor this component still counts (Docs/ScoringSystem/BUGS.md B21). A telemetry added here
    /// takes its three base stats from <c>Resources/GameModeStatsProfile</c> (<see cref="VesselTelemetry"/>).
    /// </summary>
    [DefaultExecutionOrder(-100)] // Run before VesselTelemetry.Awake
    public class VesselTelemetryBootstrapper : MonoBehaviour
    {
        [Inject] private GameDataSO gameData;

        void Awake()
        {
            if (GetComponent<VesselTelemetry>() == null && !TryGetComponent(out IVesselStatus _))
                Debug.LogWarning("[TelemetryBootstrap] No IVesselStatus found. Skipping.");
            else
                Ensure(gameObject, gameData);
            Destroy(this);
        }

        /// <summary>The vessel's telemetry, added (the hull's own type) when it has none. Null without a vessel status.</summary>
        public static VesselTelemetry Ensure(GameObject vessel, GameDataSO gameData)
        {
            if (vessel.TryGetComponent(out VesselTelemetry existing)) return existing;
            if (!vessel.TryGetComponent(out IVesselStatus vesselStatus)) return null;

            VesselTelemetry telemetry = vesselStatus.VesselType switch
            {
                VesselClassType.Sparrow  => vessel.AddComponent<SparrowVesselTelemetry>(),
                VesselClassType.Squirrel => vessel.AddComponent<SquirrelVesselTelemetry>(),
                _ => vessel.AddComponent<DefaultVesselTelemetry>()
            };

            // Inject gameData so the base class can subscribe to turn events
            telemetry.InjectGameData(gameData);

            CSDebug.LogVerbose(CSLogChannel.VesselTelemetry, $"[TelemetryBootstrap] Added {telemetry.GetType().Name} to " +
                $"{vesselStatus.VesselType} at runtime; the base stats come from the GameModeStatsProfile.");
            return telemetry;
        }
    }
}
