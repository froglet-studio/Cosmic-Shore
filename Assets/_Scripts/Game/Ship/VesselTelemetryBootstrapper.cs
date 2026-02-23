using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Ensures every vessel has a VesselTelemetry component at runtime.
    /// Attach to vessel prefabs in the Unity Editor (requires no serialized fields).
    /// On Awake, if no telemetry component exists, adds the correct type
    /// and injects gameData from the VesselController on the same GameObject.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Run before VesselTelemetry.Awake
    public class VesselTelemetryBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameDataSO gameData;

        void Awake()
        {
            if (GetComponent<VesselTelemetry>() != null)
            {
                Destroy(this);
                return;
            }

            var vesselStatus = GetComponent<IVesselStatus>();
            if (vesselStatus == null)
            {
                Debug.LogWarning("[TelemetryBootstrap] No IVesselStatus found. Skipping.");
                Destroy(this);
                return;
            }

            VesselTelemetry telemetry = vesselStatus.VesselType switch
            {
                VesselClassType.Sparrow => gameObject.AddComponent<SparrowVesselTelemetry>(),
                _ => gameObject.AddComponent<DefaultVesselTelemetry>()
            };

            // Inject gameData so the base class can subscribe to turn events
            telemetry.InjectGameData(gameData);

            Debug.Log($"[TelemetryBootstrap] Added {telemetry.GetType().Name} to {vesselStatus.VesselType}");
            Destroy(this);
        }
    }
}
