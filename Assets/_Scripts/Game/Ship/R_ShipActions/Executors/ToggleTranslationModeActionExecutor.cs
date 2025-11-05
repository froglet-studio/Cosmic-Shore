using CosmicShore;
using CosmicShore.Game;
using Obvious.Soap;
using Unity.Netcode;
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

    [Header("MiniGame")]
    [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

    IVessel _ship;
    IVesselStatus _status;
    ActionExecutorRegistry _registry;
    int _lastToggleFrame = -1;
    
    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnTurnEndOfMiniGame() => End();

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
        if (Time.frameCount == _lastToggleFrame) return; 
        _lastToggleFrame = Time.frameCount;

        var controller = status.Vessel as VesselController;
        if (!controller) return;

        bool isMp = controller.IsSpawned &&
                    NetworkManager.Singleton &&
                    NetworkManager.Singleton.IsListening &&
                    (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);

        bool hasAuthority = !isMp || controller.IsNetworkOwner;
        if (!hasAuthority) return;

        bool isOn = !status.IsTranslationRestricted;
        controller.SetTranslationRestricted(isOn); 

        if (so.StationaryMode == ToggleTranslationModeActionSO.Mode.Serpent && seedAssemblerExecutor)
        {
            if (isOn)
            {
                var seeded = seedAssemblerExecutor.StartSeed(stationarySeedConfig, status);
                vesselPrismController?.StopSpawn();
                if (seeded) seedAssemblerExecutor.BeginBonding();
            }
            else
            {
                vesselPrismController?.StartSpawn();
                seedAssemblerExecutor.StopSeedCompletely();
            }
        }
        else
        {
            if (isOn)
            {
                CosmicShore.Game.UI.NotificationAPI.Notify("", "Sparrow Prism Guns Activated");
                vesselPrismController?.StopSpawn();
            }
            else
            {
                CosmicShore.Game.UI.NotificationAPI.Notify("", "Sparrow Auto Guns Deactivated");
                vesselPrismController?.StartSpawn();
            }
        }

        stationaryModeChanged?.Raise(isOn);
    }
    void End()
    {
        if (_status == null) return;

        if (!_status.IsTranslationRestricted) return;
        _status.IsTranslationRestricted = false;

        vesselPrismController?.StartSpawn();
        if (seedAssemblerExecutor) seedAssemblerExecutor.StopSeedCompletely();

        stationaryModeChanged?.Raise(false);
    }
}
