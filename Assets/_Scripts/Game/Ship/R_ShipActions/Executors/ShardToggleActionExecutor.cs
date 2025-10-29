using CosmicShore.Game;
using Obvious.Soap;
using UnityEngine;

public sealed class ShardToggleActionExecutor : ShipActionExecutorBase
{
    [Header("Bus (required)")]
    [SerializeField] private ShardFieldBus shardFieldBus;

    [Header("Events")]
    [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

    bool _redirectActive;

    void OnEnable()
    {
        OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
    }

    void OnDisable()
    {
        OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
    }

    void OnTurnEndOfMiniGame() => End();

    public override void Initialize(IVesselStatus shipStatus) { }

    public void Toggle(ShardToggleActionSO so, IVessel ship, IVesselStatus status)
    {
        if (!_redirectActive)
        {
            var shipPos = (ship != null) ? ship.Transform.position : transform.position;
            var cell = CellControlManager.Instance.GetNearestCell(shipPos);

            Vector3 highDensityPosition = cell.GetExplosionTarget(so.Domain);
            shardFieldBus.BroadcastPointAtPosition(highDensityPosition);
            _redirectActive = true;
        }
        else
        {
            shardFieldBus.BroadcastRestoreToCrystal();
            _redirectActive = false;
        }
    }

     void End()
     {
         if (!_redirectActive) return;
         shardFieldBus.BroadcastRestoreToCrystal();
         _redirectActive = false;
     }
}