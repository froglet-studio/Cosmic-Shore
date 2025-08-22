using System;
using System.Collections.Generic;
using TMPro;
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

        [Serializable]
        public struct NamedText
        {
            public string key; public TMP_Text text;
        }

        [Header("Texts (by key)")]
        [SerializeField] private NamedText[] texts;

        readonly Dictionary<string, GameObject> _toggleMap = new();
        readonly Dictionary<string, TMP_Text>   _textMap   = new();
        readonly Dictionary<int, Coroutine>     _meterCo   = new();

        void Awake()
        {
            foreach (var t in toggles) if (!string.IsNullOrEmpty(t.key) && t.target) _toggleMap[t.key] = t.target;
            foreach (var t in texts)   if (!string.IsNullOrEmpty(t.key) && t.text)   _textMap[t.key]   = t.text;
        }

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

        public void SetText(string key, string value)
        {
            if (_textMap.TryGetValue(key, out var txt) && txt) txt.text = value;
        }

        private R_ResourceDisplay GetMeter(int index)
        {
            if (meters == null || index < 0 || index >= meters.Length) return null;
            return meters[index];
        }
    }
}

