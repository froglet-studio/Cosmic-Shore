using CosmicShore;
using CosmicShore.Game;
using UnityEngine;
using UnityEngine.Serialization;
using PrismSpawner = CosmicShore.Game.PrismSpawner;

public sealed class ToggleStationaryModeActionExecutor : ShipActionExecutorBase
{
    [FormerlySerializedAs("trailSpawner")]
    [Header("Scene Refs")]
    [SerializeField] private PrismSpawner prismSpawner;

    [Header("Seeding")]
    [SerializeField] private SeedAssemblerActionExecutor seedAssemblerExecutor;
    [SerializeField] private SeedWallActionSO stationarySeedConfig;
    
    IVessel _ship;
    IVesselStatus _status;
    ActionExecutorRegistry _registry;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status   = shipStatus;
        _ship     = shipStatus?.Vessel;
        _registry = GetComponent<ActionExecutorRegistry>();

        // Auto-resolve missing refs
        if (prismSpawner == null)
            prismSpawner = shipStatus?.PrismSpawner;

        if (seedAssemblerExecutor == null && _registry != null)
            seedAssemblerExecutor = _registry.Get<SeedAssemblerActionExecutor>();
    }

    public void Toggle(ToggleStationaryModeActionSO so, IVessel ship, IVesselStatus status)
    {
        if (so == null || status == null) return;

        status.IsStationary = !status.IsStationary;
        bool isOn = status.IsStationary;

        if (so.StationaryMode == ToggleStationaryModeActionSO.Mode.Serpent && seedAssemblerExecutor != null)
        {
            if (isOn)
            {
                bool seeded;

                if (stationarySeedConfig)
                    seeded = seedAssemblerExecutor.StartSeed(stationarySeedConfig, status);
                else
                    seeded = seedAssemblerExecutor.StartSeed(stationarySeedConfig, status); 

                prismSpawner?.PauseTrailSpawner();

                if (seeded)
                    seedAssemblerExecutor.BeginBonding();
            }
            else
            {
                prismSpawner?.RestartTrailSpawnerAfterDelay(0);
                seedAssemblerExecutor.StopSeedCompletely();
            }
        }
        else
        {
            // Non-Serpent modes: just pause/resume trail spawner
            if (isOn) prismSpawner?.PauseTrailSpawner();
            else      prismSpawner?.RestartTrailSpawnerAfterDelay(0);
        }
    }
}
