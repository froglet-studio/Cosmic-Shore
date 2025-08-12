using System.Collections.Generic;
using CosmicShore.Game.UI;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ShipHUDEffects : MonoBehaviour, IHUDEffects
    {
        [System.Serializable] public struct NamedToggle {  public string key; public GameObject target; }
        [System.Serializable] public struct NamedAnimator { public string key; public Animator animator; }

        [Header("Meters (by index)")]
        [SerializeField] private ResourceDisplay[] meters;

        [Header("Toggles (by key)")]
        [SerializeField] private NamedToggle[] toggles;

        [Header("Animators (by key -> trigger name passed from controller)")]
        [SerializeField] private NamedAnimator[] animators;

        private Dictionary<string, GameObject> _toggleMap;
        private Dictionary<string, Animator> _animMap;
        
        [Header("Debug")]
        [SerializeField] bool verbose = true;
        private void Log(string m) { if (verbose) Debug.Log($"[ShipHUDEffects] {m}", this); }


        private void Awake()
        {
            _toggleMap = new Dictionary<string, GameObject>();
            foreach (var t in toggles) if (!string.IsNullOrEmpty(t.key) && t.target) _toggleMap[t.key] = t.target;

            _animMap = new Dictionary<string, Animator>();
            foreach (var a in animators) if (!string.IsNullOrEmpty(a.key) && a.animator) _animMap[a.key] = a.animator;
        }

        public void AnimateDrain(int idx, float duration, float from)
        {
            Log($"AnimateDrain idx={idx} dur={duration:F2} from={from:F2}");
            var rd = Get(idx); if (rd == null) { Log($"AnimateDrain idx={idx} -> NO METER"); return; }
            rd.AnimateFillDown(duration, Mathf.Clamp01(from));
        }

        public void AnimateRefill(int idx, float duration, float to)
        {
            Log($"AnimateRefill idx={idx} dur={duration:F2} to={to:F2}");
            var rd = Get(idx); if (rd == null) { Log($"AnimateRefill idx={idx} -> NO METER"); return; }
            rd.AnimateFillUp(duration, Mathf.Clamp01(to));
        }

        public void SetMeter(int idx, float value)
        {
            Log($"SetMeter idx={idx} val={value:F3}");
            var rd = Get(idx); if (rd == null) { Log($"SetMeter idx={idx} -> NO METER"); return; }
            rd.UpdateDisplay(Mathf.Clamp01(value));
        }

        public void SetToggle(string key, bool on)
        {
            Log($"SetToggle '{key}' -> {(on ? "ON" : "OFF")}");
            if (key == null || !_toggleMap.TryGetValue(key, out var go) || !go) { Log($"Toggle '{key}' NOT FOUND"); return; }
            go.SetActive(on);
        }

        public void TriggerAnim(string key)
        {
            Log($"TriggerAnim '{key}'");
            if (key == null || !_animMap.TryGetValue(key, out var anim) || !anim) { Log($"Animator '{key}' NOT FOUND"); return; }
            anim.SetTrigger(key);
        }

        private ResourceDisplay Get(int idx)
        {
            if (meters == null || idx < 0 || idx >= meters.Length) return null;
            return meters[idx];
        }
    }
}
