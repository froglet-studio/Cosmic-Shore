using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game
{
    [System.Serializable]
    public struct ResourceChangeSpec
    {
        [SerializeField] int _resourceIndex;
        [SerializeField, Range(0, 1)] float _resourceAmount;
        [SerializeField, Tooltip("If true, set to value; if false, add value.")]
        bool _overrideAmount;

        public void ApplyTo(ResourceSystem rs, Object context = null)
        {
            if (rs == null) return;

            var list = rs.Resources;
            if (_resourceIndex < 0 || _resourceIndex >= list.Count)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"Resource index {_resourceIndex} out of range ({list.Count}).", context);
#endif
                return;
            }
            Debug.Log($"<color=green> Resource amount changed to {_resourceAmount}");
            
            if (_overrideAmount)
                rs.SetResourceAmount(_resourceIndex, _resourceAmount);
            else
                rs.ChangeResourceAmount(_resourceIndex, _resourceAmount);
        }
    }
}