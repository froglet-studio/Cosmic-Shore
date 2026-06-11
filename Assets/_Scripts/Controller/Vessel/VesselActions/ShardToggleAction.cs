using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Toggle ability:
    /// - First press: find the nearest Cell → get the densest region for the configured Team → redirect shards there.
    /// - Second press: restore shards to their normal (crystal) behavior.
    /// </summary>
    public class ShardToggleAction : ShipAction
    {
        [Header("Bus (required)")]
        [SerializeField] private ShardFieldBus shardFieldBus;

        [FormerlySerializedAs("team")]
        [Header("Mass Centroids Settings")]
        [Tooltip("Team whose density grid is queried for the explosion target.")]
        [SerializeField] private Domains domain = Domains.Jade;

        [SerializeField] private CellRuntimeDataSO cellData;
        
        // Runtime toggle state
        bool _redirectActive = false;

        public override void StartAction()
        {
            if (!cellData)
            {
                CSDebug.LogError("No cell data found!");
                return;
            }
            
            if (!shardFieldBus)
            {
                CSDebug.LogWarning("[ShardToggleAction] No ShardFieldBus assigned!");
                return;
            }
            
            if (!_redirectActive)
            {
                var cell = cellData.Cell;
                Vector3 highDensityPosition = cell.GetExplosionTarget(domain);
                // CSDebug.Log($"[ShardToggleAction] MassCentroids → Cell='{cell.name}' Team={domain} Target={highDensityPosition}");
                shardFieldBus.BroadcastPointAtPosition(highDensityPosition);
                _redirectActive = true;
            }
            else
            {
                // CSDebug.Log("[ShardToggleAction] Toggled OFF → restoring shards to crystal.");
                shardFieldBus.BroadcastRestoreToCrystal();
                _redirectActive = false;
            }
        }

        public override void StopAction()
        {
 
        }
    }
}
