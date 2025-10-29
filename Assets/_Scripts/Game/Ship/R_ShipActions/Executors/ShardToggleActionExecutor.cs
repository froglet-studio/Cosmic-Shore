using CosmicShore.Game;
using UnityEngine;

public sealed class ShardToggleActionExecutor : ShipActionExecutorBase
{
    [Header("Bus (required)")]
    [SerializeField] private ShardFieldBus shardFieldBus;

    bool _redirectActive;

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
}