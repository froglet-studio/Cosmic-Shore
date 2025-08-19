using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Simple effects router used by subscriptions and controller (IHUDEffects).
    /// - Drives meters via R_ResourceDisplay[] (supports SpriteFill / Swap / Sequence)
    /// - Toggles GameObjects by key
    /// - Triggers Animator by key (no separate HudAnimatorTrigger component)
    /// </summary>
    public class ShipHUDEffects : MonoBehaviour, IHUDEffects
    {
        [System.Serializable] public struct NamedToggle { public string key; public GameObject target; }
        [System.Serializable] public struct NamedAnimator { public string key; public Animator animator; }

        [Header("Meters (R_ResourceDisplay by index)")]
        [SerializeField] private R_ResourceDisplay[] meters;

        [Header("Toggles (by key)")]
        [SerializeField] private NamedToggle[] toggles;

        [Header("Animators (by key)")]
        [SerializeField] private NamedAnimator[] animators;

        private readonly Dictionary<string, GameObject> _toggleMap = new();

        void Awake()
        {
            _toggleMap.Clear();
            foreach (var t in toggles)
                if (!string.IsNullOrEmpty(t.key) && t.target)
                    _toggleMap[t.key] = t.target; ;
        }

        // -------- IHUDEffects --------

        public void SetMeter(int index, float normalized)
        {
            var m = GetMeter(index);
            if (m == null) return;
            m.SetImmediate(Mathf.Clamp01(normalized));
        }

        public void AnimateDrain(int index, float duration, float fromNormalized)
        {
            var m = GetMeter(index);
            if (m == null) return;
            // animate from whatever the meter is showing now → 0
            m.AnimateFromTo(m.CurrentNormalized, 0f, Mathf.Max(0f, duration));
        }

        public void AnimateRefill(int index, float duration, float toNormalized)
        {
            var m = GetMeter(index);
            if (m == null) return;
            // animate from whatever the meter is showing now → target (usually 1f)
            m.AnimateFromTo(m.CurrentNormalized, Mathf.Clamp01(toNormalized), Mathf.Max(0f, duration));
        }


        public void SetToggle(string key, bool on)
        {
            if (_toggleMap.TryGetValue(key, out var go) && go)
                go.SetActive(on);
        }
        
        private R_ResourceDisplay GetMeter(int index)
        {
            if (meters == null || index < 0 || index >= meters.Length) return null;
            return meters[index];
        }
    }
}

