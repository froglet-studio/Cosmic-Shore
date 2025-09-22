using CosmicShore.Game;
using UnityEngine;

public sealed class ShardToggleActionExecutor : ShipActionExecutorBase
{
    [Header("Bus (required)")]
    [SerializeField] private ShardFieldBus shardFieldBus;

    bool _redirectActive;

    public override void Initialize(IVesselStatus shipStatus)
    {
        // no special init needed; bus is scene ref
    }

    public void Toggle(ShardToggleActionSO so, IVessel ship, IVesselStatus status)
    {
        if (shardFieldBus == null)
        {
            Debug.LogWarning("[ShardToggleAction] No ShardFieldBus assigned!");
            return;
        }

        if (!_redirectActive)
        {
            var shipPos = (ship != null) ? ship.Transform.position : transform.position;
            var cell = CellControlManager.Instance.GetNearestCell(shipPos);
            if (cell == null)
            {
                Debug.LogWarning("[ShardToggleAction] No cell found near ship.");
                return;
            }

            Vector3 highDensityPosition = cell.GetExplosionTarget(so.Domain);
            Debug.Log($"[ShardToggleAction] MassCentroids → Cell='{cell.name}' Team={so.Domain} Target={highDensityPosition}");
            shardFieldBus.BroadcastPointAtPosition(highDensityPosition);
            _redirectActive = true;
        }
        else
        {
            Debug.Log("[ShardToggleAction] Toggled OFF → restoring shards to crystal.");
            shardFieldBus.BroadcastRestoreToCrystal();
            _redirectActive = false;
        }
    }
}