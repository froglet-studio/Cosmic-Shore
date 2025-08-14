// Assets/Scripts/World/SnowChanger.cs
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class SnowChanger : MonoBehaviour
    {
        [Header("Bus")]
        [SerializeField] ShardFieldBus shardFieldBus;

        [SerializeField] GameObject snow;
        [SerializeField] Vector3 crystalSize = new Vector3(500, 500, 500);
        [SerializeField] int shardDistance = 100;

        [Header("Optional Fields")]
        [SerializeField] bool lookAt;
        [SerializeField] Vector3 targetAxis;
        [SerializeField] Vector3 newOrigin;

        [Header("Nudge")]
        [SerializeField] Transform nudgeRoot;
        
        [SerializeField] ScriptableEventNoParam OnCellItemsUpdated;

        Crystal _crystal;

        GameObject[,,] crystalLattice;
        readonly float nodeScaler = 10;
        readonly float nodeSize = .25f;
        readonly float sphereScaler = 2;
        int shardsX, shardsY, shardsZ;
        float sphereDiameter;
        Vector3 origin = Vector3.zero;
        Vector3[,,] _originalPositions;

        // === New: control mode ===
        enum ControlMode { Crystal, Axis, Position, Transform }
        ControlMode _mode = ControlMode.Crystal;
        Vector3 _overridePosition;
        Transform _overrideTransform;
        Quaternion _nudgeOriginalRotation;
        
        void OnEnable()
        {
            OnCellItemsUpdated.OnRaised += ChangeSnowSize;
            shardFieldBus?.Register(this);     // register to bus
            if (nudgeRoot != null)
            {
                _nudgeOriginalRotation = nudgeRoot.rotation;
                Debug.Log($"[SnowChanger] Registered with bus, nudgeRoot='{nudgeRoot.name}'");
            }
            else
            {
                Debug.LogWarning("[SnowChanger] nudgeRoot not assigned. Parent rotation will not reflect!");
            }
        }

        void OnDisable()
        {
            OnCellItemsUpdated.OnRaised -= ChangeSnowSize;
            shardFieldBus?.Unregister(this);   // unregister
        }

        public void Initialize(Crystal crystal)
        {
            _crystal = crystal;
            origin = newOrigin;

            shardsX = (int)(crystalSize.x / shardDistance);
            shardsY = (int)(crystalSize.y / shardDistance);
            shardsZ = (int)(crystalSize.z / shardDistance);

            if (_crystal != null) 
                sphereDiameter = sphereScaler * _crystal.GetComponent<Crystal>().sphereRadius;

            crystalLattice = new GameObject[shardsX * 2 + 1, shardsY * 2 + 1, shardsZ * 2 + 1];
            _originalPositions = new Vector3[shardsX * 2 + 1, shardsY * 2 + 1, shardsZ * 2 + 1]; // new

            for (int x = -shardsX; x <= shardsX; x++)
            for (int y = -shardsY; y <= shardsY; y++)
            for (int z = -shardsZ; z <= shardsZ; z++)
            {
                GameObject tempSnow = Instantiate(snow, transform, true);
                tempSnow.transform.localScale = Vector3.one * nodeScaler;
                Vector3 pos = origin + new Vector3(
                    x * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2),
                    y * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2),
                    z * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2));

                tempSnow.transform.position = pos;
                crystalLattice[x + shardsX, y + shardsY, z + shardsZ] = tempSnow;
                _originalPositions[x + shardsX, y + shardsY, z + shardsZ] = pos; // store
            }

            ChangeSnowSize();
        }
        
        void ChangeSnowSize()
        {
             float nodeScalerOverThree = nodeScaler / 3;

            for (int x = 0; x < shardsX * 2 + 1; x++)
            for (int y = 0; y < shardsY * 2 + 1; y++)
            for (int z = 0; z < shardsZ * 2 + 1; z++)
            {
                var shard = crystalLattice[x, y, z];
                float normalizedDistance;

                if (_mode == ControlMode.Crystal && _crystal != null)
                {
                    float clampedDistance = Mathf.Clamp(
                        (shard.transform.position - _crystal.transform.position).magnitude, 0, sphereDiameter);
                    normalizedDistance = clampedDistance / sphereDiameter;
                    shard.transform.LookAt(_crystal.transform);
                }
                else if (_mode == ControlMode.Transform && _overrideTransform != null)
                {
                    Vector3 dir = (_overrideTransform.position - shard.transform.position);
                    float m = dir.magnitude;
                    normalizedDistance = (m <= 0f ? 0f : 1f);
                    if (m > 0.0001f) shard.transform.rotation = Quaternion.LookRotation(dir);
                }
                else if (_mode == ControlMode.Position)
                {
                    Vector3 dir = (_overridePosition - shard.transform.position);
                    float m = dir.magnitude;
                    normalizedDistance = (m <= 0f ? 0f : 1f);
                    if (m > 0.0001f) shard.transform.rotation = Quaternion.LookRotation(dir);
                }
                else // Axis (existing behavior)
                {
                    var reject = shard.transform.position - (Vector3.Dot(shard.transform.position, targetAxis.normalized) * targetAxis.normalized);
                    var maxDistance = Mathf.Max(shardsX, shardsY) * shardDistance;
                    float clampedDistance = Mathf.Clamp(reject.magnitude, 0, maxDistance);
                    normalizedDistance = clampedDistance / maxDistance;

                    if (lookAt) shard.transform.rotation = Quaternion.LookRotation(-reject.normalized);
                    else shard.transform.rotation = Quaternion.LookRotation(targetAxis);
                }

                shard.transform.localScale =
                    Vector3.forward * (normalizedDistance * nodeScaler + nodeSize) +
                    Vector3.one     * (normalizedDistance * nodeScalerOverThree + nodeSize);
            }

            UpdateNudgeRootRotation();
        }

        public void SetOrigin(Vector3 origin) => this.origin = origin;

        void UpdateNudgeRootRotation()
        {
            if (nudgeRoot == null) return;

            Vector3 targetPos;
            switch (_mode)
            {
                case ControlMode.Transform:
                    if (_overrideTransform == null) return;
                    targetPos = _overrideTransform.position;
                    break;
                case ControlMode.Position:
                    targetPos = _overridePosition;
                    break;
                case ControlMode.Axis:
                    // Point the nudge root “down the axis” from its current position
                    targetPos = nudgeRoot.position + targetAxis.normalized * 10f;
                    break;
                case ControlMode.Crystal:
                default:
                    if (_crystal == null) return;
                    targetPos = _crystal.transform.position;
                    break;
            }

            var dir = targetPos - nudgeRoot.position;
            if (dir.sqrMagnitude > 0.0001f)
                nudgeRoot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        
        // ======= Public control surface called by the bus =======
        public void PointAtTransform(Transform t)
        {
            _overrideTransform = t;
            _mode = ControlMode.Transform;
            Debug.Log($"[SnowChanger] PointAtTransform -> {t?.name}");
            ChangeSnowSize();
        }

        public void PointAtPosition(Vector3 worldPos)
        {
            _overridePosition = worldPos;
            _mode = ControlMode.Position;
            Debug.Log($"[SnowChanger] PointAtPosition -> {worldPos}");
            ChangeSnowSize();
        }

        public void AlignToAxis(Vector3 axis, bool shouldLookAtReject = true)
        {
            targetAxis = axis;
            lookAt = shouldLookAtReject;
            _mode = ControlMode.Axis;
            Debug.Log($"[SnowChanger] AlignToAxis -> axis {axis} lookAt={lookAt}");
            ChangeSnowSize();
        }

        public void RestoreToCrystal()
        {
            _overrideTransform = null;
            _mode = ControlMode.Crystal;

            // OPTIONAL: if you also want positions to snap back (kept from previous patch),
            // keep your cached originalPositions logic here. Rotation goes back to crystal:
            Debug.Log("[SnowChanger] RestoreToCrystal");
            ChangeSnowSize();

            // Also put the NudgeShards parent back to original orientation for a perfect reset
            if (nudgeRoot != null) nudgeRoot.rotation = _nudgeOriginalRotation;
        }
    }
}
