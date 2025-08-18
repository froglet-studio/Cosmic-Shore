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
        int shardsX, shardsY, shardsZ;
        float sphereDiameter;
        Vector3 origin = Vector3.zero;
        Vector3[,,] _originalPositions;
        
        private const float NODE_SCALER = 10f;
        private const float NODE_SIZE   = 0.25f;
        private const float SPHERE_SCALER = 2f;
        
        enum ControlMode { Crystal, Axis, Position, Transform }
        ControlMode _mode = ControlMode.Crystal;
        Vector3 _overridePosition;
        Transform _overrideTransform;
        
        bool _nudgeControlArmed = false;   
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
            _crystal = crystal; // TODO: this should be injected by the node, but that's not working at the moment :/
            origin = newOrigin; 
            shardsX = (int)(crystalSize.x / shardDistance); 
            shardsY = (int)(crystalSize.y / shardDistance); 
            shardsZ = (int)(crystalSize.z / shardDistance); 
                
            if (_crystal != null) 
                sphereDiameter = SPHERE_SCALER  * _crystal.GetComponent<Crystal>().sphereRadius; 
            
            crystalLattice = new GameObject[shardsX * 2 + 1, shardsY * 2 + 1, shardsZ * 2 + 1]; // both sides of each axis plus the midplane
            
            for(int x = -shardsX; x <= shardsX; x++)
            {
                for(int y = -shardsY; y <= shardsY; y++) 
                { 
                    for (int z = -shardsZ; z <= shardsZ; z++) 
                    { 
                        var tempSnow = Instantiate(snow, transform, true); 
                        tempSnow.transform.localScale = Vector3.one * NODE_SCALER; 
                        tempSnow.transform.position = origin + new Vector3(x * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2), y * shardDistance + 
                            Random.Range(-shardDistance / 2, shardDistance / 2), z * shardDistance + Random.Range(-shardDistance / 2, shardDistance / 2)); 
                            
                        crystalLattice[x + shardsX, y + shardsY, z + shardsZ] = tempSnow; } 
                }
            } 
            
            _mode = ControlMode.Crystal;
            _nudgeControlArmed = false;
            ChangeSnowSize(); 
        }
        
         void ChangeSnowSize()
        {
            float nodeScalerOverThree = NODE_SCALER / 3;

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
                    normalizedDistance = sphereDiameter > 0f ? clampedDistance / sphereDiameter : 0f;
                    shard.transform.LookAt(_crystal.transform);
                }
                else
                {
                    Vector3 dir = _overridePosition - shard.transform.position;
                    float m = dir.magnitude;
                    if (m > 0.0001f) shard.transform.rotation = Quaternion.LookRotation(dir);
                    normalizedDistance = 1f;
                }
                shard.transform.localScale =
                    Vector3.forward * (normalizedDistance * NODE_SCALER + NODE_SIZE) +
                    Vector3.one     * (normalizedDistance * nodeScalerOverThree + NODE_SIZE);
            }
            
            if (_nudgeControlArmed)
                UpdateNudgeRootRotation();
        }

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
        
        public void SetOrigin(Vector3 origin) => this.origin = origin;
  
        public void PointAtPosition(Vector3 worldPos)
        {
            _overridePosition = worldPos;
            _mode = ControlMode.Position;
            _nudgeControlArmed = true;                
            Debug.Log($"[SnowChanger] PointAtPosition -> {worldPos}");
            ChangeSnowSize();
        }
        
        public void RestoreToCrystal()
        {
            _overrideTransform = null;
            _mode = ControlMode.Crystal;
            _nudgeControlArmed = false;                  

            Debug.Log("[SnowChanger] RestoreToCrystal");
            ChangeSnowSize();

            if (nudgeRoot != null)
                nudgeRoot.rotation = _nudgeOriginalRotation;
        }
    }
}
