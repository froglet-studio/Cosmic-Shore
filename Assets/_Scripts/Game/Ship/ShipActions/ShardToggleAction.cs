using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
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

        [Tooltip("Max distance to search for a cell (used by CellControlManager on its side, if applicable).")]
        [SerializeField] private float searchRadiusHint = 0f; // optional / unused here, kept for future

        // Runtime toggle state
        bool _redirectActive = false;

        public override void StartAction()
        {
            if (shardFieldBus == null)
            {
                Debug.LogWarning("[ShardToggleAction] No ShardFieldBus assigned!");
                return;
            }
            
            if (!_redirectActive)
            {
                var shipPos = Vessel != null ? Vessel.Transform.position : transform.position;
                var cell = CellControlManager.Instance.GetNearestCell(shipPos);

                Vector3 highDensityPosition = cell.GetExplosionTarget(domain);

                Debug.Log($"[ShardToggleAction] MassCentroids → Cell='{cell.name}' Team={domain} Target={highDensityPosition}");
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

        public override void StopAction()
        {
 
        }
    }
}
