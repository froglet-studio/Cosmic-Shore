using CosmicShore;
using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

public sealed class ToggleTranslationModeActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] private VesselPrismController vesselPrismController;

    [Header("Seeding")]
    [SerializeField] private SeedAssemblerActionExecutor seedAssemblerExecutor;
    [SerializeField] private SeedWallActionSO stationarySeedConfig;

    [Header("Events")]
    [SerializeField] private ScriptableEventBool stationaryModeChanged;

    IVessel _ship;
    IVesselStatus _status;
    ActionExecutorRegistry _registry;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status   = shipStatus;
        _ship     = shipStatus?.Vessel;
        _registry = GetComponent<ActionExecutorRegistry>();

        if (vesselPrismController == null)
            vesselPrismController = shipStatus?.VesselPrismController;

        if (seedAssemblerExecutor == null && _registry != null)
            seedAssemblerExecutor = _registry.Get<SeedAssemblerActionExecutor>();
    }

    public void Toggle(ToggleTranslationModeActionSO so, IVessel ship, IVesselStatus status)
    {
        if (!so || status == null) return;

        status.IsTranslationRestricted = !status.IsTranslationRestricted;
        var isOn = status.IsTranslationRestricted;

        if (so.StationaryMode == ToggleTranslationModeActionSO.Mode.Serpent && seedAssemblerExecutor)
        {
            if (isOn)
            {
                var seeded = seedAssemblerExecutor.StartSeed(stationarySeedConfig, status);
                vesselPrismController?.PauseTrailSpawner();
                if (seeded) seedAssemblerExecutor.BeginBonding();
            }
            else
            {
                vesselPrismController?.RestartTrailSpawnerAfterDelay(0);
                seedAssemblerExecutor.StopSeedCompletely();
            }
        }
        else
        {
            if (isOn)
            {
                CosmicShore.Game.UI.NotificationAPI.Notify("", "Sparrow Prism Guns Activated");
                vesselPrismController?.PauseTrailSpawner();
            }
            else
            {
                CosmicShore.Game.UI.NotificationAPI.Notify("", "Sparrow Auto Guns Activated");
                vesselPrismController?.RestartTrailSpawnerAfterDelay(0);
            }
        }

        stationaryModeChanged?.Raise(isOn);
    }
}
