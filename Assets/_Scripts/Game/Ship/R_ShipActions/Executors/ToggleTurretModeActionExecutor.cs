using CosmicShore;
using CosmicShore.Game;
using Obvious.Soap;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Plants the vessel as a stationary turret: translation is restricted, prism
/// spawning stops, and the configured resource regenerates faster while planted.
/// </summary>
public sealed class ToggleTurretModeActionExecutor : ShipActionExecutorBase
{
    [Header("Scene Refs")]
    [SerializeField] VesselPrismController vesselPrismController;

    [Header("Events")]
    [SerializeField] ScriptableEventBool stationaryModeChanged;

    [Header("MiniGame")]
    [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

    IVesselStatus _status;
    ToggleTurretModeActionSO _activeConfig;
    int _lastToggleFrame = -1;

    void OnEnable()
    {
        if (OnMiniGameTurnEnd)
            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        if (OnMiniGameTurnEnd)
            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;

        if (vesselPrismController == null)
            vesselPrismController = shipStatus?.VesselPrismController;
    }

    public void Toggle(ToggleTurretModeActionSO so, IVesselStatus status)
    {
        if (!so || status == null) return;

        if (Time.frameCount == _lastToggleFrame) return;
        _lastToggleFrame = Time.frameCount;

        var controller = status.Vessel as VesselController;
        if (!controller) return;

        bool netActive = NetworkManager.Singleton && NetworkManager.Singleton.IsListening &&
                         (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer);
        bool hasAuthority = !netActive || NetworkManager.Singleton.IsServer || controller.IsOwner;
        if (!hasAuthority) return;

        bool isOn = !status.IsTranslationRestricted;

        controller.SetTranslationRestricted(isOn);

        if (isOn) vesselPrismController?.StopSpawn();
        else vesselPrismController?.StartSpawn();

        _activeConfig = isOn ? so : null;
        ApplyGainRate(so, isOn);

        stationaryModeChanged?.Raise(isOn);
    }

    void ApplyGainRate(ToggleTurretModeActionSO so, bool boosted)
    {
        var resources = _status?.ResourceSystem?.Resources;
        if (resources == null || so.ResourceIndex < 0 || so.ResourceIndex >= resources.Count)
            return;

        var resource = resources[so.ResourceIndex];
        resource.resourceGainRate = boosted
            ? resource.initialResourceGainRate * so.StationaryGainMultiplier
            : resource.initialResourceGainRate;
    }

    void OnTurnEndOfMiniGame()
    {
        if (_status == null) return;
        if (!_status.IsTranslationRestricted) return;

        _status.IsTranslationRestricted = false;
        vesselPrismController?.StartSpawn();

        if (_activeConfig != null)
        {
            ApplyGainRate(_activeConfig, false);
            _activeConfig = null;
        }

        stationaryModeChanged?.Raise(false);
    }
}
