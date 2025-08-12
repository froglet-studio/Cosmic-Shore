using System.Reflection;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// While active, redirects SnowChanger shards to an alternate direction.
    /// Resolved via the ship's nearest Cell (no scene-wide searches).
    /// </summary>
    public class ShardToggleAction : ShipAction
    {
        [Header("Toggle Settings")]
        [SerializeField] private Vector3 alternateTargetAxis = Vector3.up;
        [SerializeField] private bool lookAtAlternate = true;

        private SnowChanger _snowChanger;
        private Vector3 _originalTargetAxis;
        private bool _originalLookAt;
        private bool _hasBackup;

        // reflection
        FieldInfo _fiTargetAxis;
        FieldInfo _fiLookAt;
        MethodInfo _miChangeSnowSize;

        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);

            // 1) Find the nearest Cell to the ship (preferred: CellControlManager)
            var shipPos = ShipStatus.Transform.position;
            Cell cell = null;

            if (CellControlManager.Instance != null)
            {
                // Crystal.ActivateCrystal() uses the same call: GetNearestCell(position)
                cell = CellControlManager.Instance.GetNearestCell(shipPos);
            }

            // Fallback: pick nearest Cell by distance if manager missing
            if (cell == null)
            {
                var allCells = Object.FindObjectsOfType<Cell>(includeInactive: false);
                float best = float.PositiveInfinity;
                foreach (var c in allCells)
                {
                    float d = (c.transform.position - shipPos).sqrMagnitude;
                    if (d < best) { best = d; cell = c; }
                }
            }

            if (cell == null)
            {
                Debug.LogWarning("[ShardToggleAction] No Cell found near ship; cannot toggle shards.", this);
                return;
            }

            // 2) Get SnowChanger from that Cell’s hierarchy
            _snowChanger = cell.GetComponentInChildren<SnowChanger>(true);
            if (_snowChanger == null)
            {
                Debug.LogWarning("[ShardToggleAction] Cell has no SnowChanger.", cell);
                return;
            }

            // 3) Cache reflection handles once
            var t = typeof(SnowChanger);
            _fiTargetAxis     = t.GetField("targetAxis", BindingFlags.Instance | BindingFlags.NonPublic);
            _fiLookAt         = t.GetField("lookAt", BindingFlags.Instance | BindingFlags.NonPublic);
            _miChangeSnowSize = t.GetMethod("ChangeSnowSize", BindingFlags.Instance | BindingFlags.NonPublic);

            if (_fiTargetAxis == null || _fiLookAt == null || _miChangeSnowSize == null)
            {
                Debug.LogError("[ShardToggleAction] Expected private fields 'targetAxis','lookAt' and private method 'ChangeSnowSize()' on SnowChanger.", _snowChanger);
            }
        }

        public override void StartAction()
        {
            if (!EnsureReady()) return;

            if (!_hasBackup)
            {
                _originalTargetAxis = (Vector3)_fiTargetAxis.GetValue(_snowChanger);
                _originalLookAt     = (bool)_fiLookAt.GetValue(_snowChanger);
                _hasBackup = true;
            }

            _fiTargetAxis.SetValue(_snowChanger, alternateTargetAxis);
            _fiLookAt.SetValue(_snowChanger, lookAtAlternate);

            _miChangeSnowSize.Invoke(_snowChanger, null);
            Debug.Log($"[ShardToggleAction] Activated → axis={alternateTargetAxis}, lookAt={lookAtAlternate}", this);
        }

        public override void StopAction()
        {
            if (!EnsureReady() || !_hasBackup) return;

            _fiTargetAxis.SetValue(_snowChanger, _originalTargetAxis);
            _fiLookAt.SetValue(_snowChanger, _originalLookAt);

            _miChangeSnowSize.Invoke(_snowChanger, null);
            Debug.Log("[ShardToggleAction] Deactivated → restored original shard targeting.", this);
        }

        private bool EnsureReady()
        {
            if (_snowChanger == null) { Debug.LogWarning("[ShardToggleAction] SnowChanger not set.", this); return false; }
            if (_fiTargetAxis == null || _fiLookAt == null || _miChangeSnowSize == null)
            {
                Debug.LogError("[ShardToggleAction] Reflection members missing.", this); return false;
            }
            return true;
        }
    }
}
