using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Central ticker for <see cref="PrismOctahedronShield"/> engage/shatter
    /// transitions. Replaces a per-shield MonoBehaviour <c>Update()</c>: at high prism
    /// counts every prism carries an octahedron shield, so thousands of Update()
    /// invocations ran every frame just to early-return (profiled: 9234 calls, ~1.3ms
    /// plus the BehaviourUpdate dispatch overhead). Shields register here ONLY while
    /// actively morphing — the idle majority cost nothing. Mirrors PrismTimerManager /
    /// PrismScaleManager (centralized ticking of the few active members).
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

        readonly HashSet<PrismOctahedronShield> _active = new();
        readonly List<PrismOctahedronShield> _scratch = new(64);

        /// <summary>Concurrently morphing shields (telemetry).</summary>
        public int ActiveCount => _active.Count;

        public void Register(PrismOctahedronShield shield)
        {
            if (shield != null) _active.Add(shield);
        }

        public void Unregister(PrismOctahedronShield shield)
        {
            _active.Remove(shield);
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
                if (s == null) { _active.Remove(s); continue; }
                if (!s.Tick(dt)) _active.Remove(s); // transition complete — stop ticking
            }
        }
    }
}
