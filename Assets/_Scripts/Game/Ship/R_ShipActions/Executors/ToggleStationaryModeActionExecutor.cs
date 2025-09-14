using CosmicShore;
using CosmicShore.Game;
using UnityEngine;

public sealed class ToggleStationaryModeActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] private SeedAssemblerConfigurator seedAssembler;
    [SerializeField] private TrailSpawner trailSpawner;

    IVessel _ship;
    IVesselStatus _status;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
        _ship = shipStatus.Vessel;

        if (seedAssembler != null)
            seedAssembler.Initialize(_ship);
    }

    public void Toggle(ToggleStationaryModeActionSO so, IVessel ship, IVesselStatus status)
    {
        status.IsStationary = !status.IsStationary;
        bool isOn = status.IsStationary;

        if (so.StationaryMode == ToggleStationaryModeActionSO.Mode.Serpent && seedAssembler != null)
        {
            if (isOn)
            {
                if (seedAssembler.StartSeed())
                {
                    trailSpawner?.PauseTrailSpawner();
                    seedAssembler.BeginBonding();
                }
                else
                {
                    trailSpawner?.PauseTrailSpawner();
                }
            }
            else
            {
                trailSpawner?.RestartTrailSpawnerAfterDelay(0);
                seedAssembler.StopSeedCompletely();
            }
        }
        else
        {
            if (isOn) trailSpawner?.PauseTrailSpawner();
            else      trailSpawner?.RestartTrailSpawnerAfterDelay(0);
        }
    }
}