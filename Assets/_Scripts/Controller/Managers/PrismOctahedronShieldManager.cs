using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A shield morph the central ticker drives while it is actively transitioning.
    /// Return false from Tick when the transition is done — the manager drops the
    /// registration (idle shields must cost nothing).
    /// </summary>
    public interface IPrismShieldMorphTicker
    {
        bool Tick(float dt);
    }

    /// <summary>
    /// Central ticker for shield engage/shatter transitions (octahedron AND
    /// stellated). Replaces per-shield MonoBehaviour <c>Update()</c>s: at high prism
    /// counts every prism carries an octahedron shield, so thousands of Update()
    /// invocations ran every frame just to early-return (profiled: 9234 calls, ~1.3ms
    /// plus the BehaviourUpdate dispatch overhead). Shields register here ONLY while
    /// actively morphing — the idle majority cost nothing. Mirrors PrismTimerManager
    /// (centralized ticking of the few active members).
    /// NOTE (clock-material law): the morphs this ticks are themselves scheduled for
    /// GPU migration (Docs/PRISM_ANIMATION.md §5 B4) — do not add new members.
    /// </summary>
    [DisallowMultipleComponent]
    public class PrismOctahedronShieldManager : Singleton<PrismOctahedronShieldManager>
    {
        static bool _isQuitting;
        void OnApplicationQuit() => _isQuitting = true;

        public static PrismOctahedronShieldManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            if (_isQuitting) return null;
            var go = new GameObject("[PrismOctahedronShieldManager]");
            return go.AddComponent<PrismOctahedronShieldManager>();
        }

        readonly HashSet<IPrismShieldMorphTicker> _active = new();
        readonly List<IPrismShieldMorphTicker> _scratch = new(64);

        /// <summary>Concurrently morphing shields (telemetry).</summary>
        public int ActiveCount => _active.Count;

        public void Register(IPrismShieldMorphTicker shield)
        {
            if (shield != null) _active.Add(shield);
        }

        public void Unregister(IPrismShieldMorphTicker shield)
        {
            if (shield != null) _active.Remove(shield);
        }

        void Update()
        {
            if (_active.Count == 0) return;
            float dt = Time.deltaTime;

            // Snapshot — Tick can finish a transition and remove itself, mutating _active.
            _scratch.Clear();
            _scratch.AddRange(_active);
            for (int i = 0; i < _scratch.Count; i++)
            {
                var s = _scratch[i];
                // Interface refs don't hit Unity's overloaded null — check the
                // underlying Object for destroyed components explicitly.
                if (s is Object obj && obj == null) { _active.Remove(s); continue; }
                if (!s.Tick(dt)) _active.Remove(s); // transition complete — stop ticking
            }
        }
    }
}
