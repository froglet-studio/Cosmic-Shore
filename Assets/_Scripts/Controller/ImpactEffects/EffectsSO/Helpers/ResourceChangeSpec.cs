using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public struct ResourceChangeSpec
    {
        [SerializeField] int _resourceIndex;
        [SerializeField, Range(0, 1)] float _resourceAmount;
        [SerializeField, Tooltip("If true, set to value; if false, add value.")]
        bool _overrideAmount;

        /// <summary>The resource this spec writes — exposed so callers can read the value BEFORE
        /// the change (e.g. an effect that spends a meter and needs to know what it spent).</summary>
        public int ResourceIndex => _resourceIndex;

        /// <summary>
        /// Applies the authored change. <paramref name="scale"/> multiplies the authored amount for
        /// per-hit modifiers (an elemental bonus on the gain); it defaults to 1, so existing callers
        /// and assets behave exactly as before.
        /// </summary>
        public void ApplyTo(ResourceSystem rs, Object context = null, float scale = 1f)
        {
            if (rs == null) return;

            var list = rs.Resources;
            if (_resourceIndex < 0 || _resourceIndex >= list.Count)
            {
#if UNITY_EDITOR
                CSDebug.LogWarning($"Resource index {_resourceIndex} out of range ({list.Count}).", context);
#endif
                return;
            }

            float amount = _resourceAmount * scale;

            if (_overrideAmount)
                rs.SetResourceAmount(_resourceIndex, amount);
            else
                rs.ChangeResourceAmount(_resourceIndex, amount);
        }
    }
}