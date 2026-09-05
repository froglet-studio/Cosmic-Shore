using CosmicShore.Data;
using CosmicShore.UI;
using Reflex.Attributes;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Objective provider for Hijack: the nearest BURR still holding mass this pilot could steal.
    ///
    /// <para>Every other mode's arrow names an object that already exists in the scene - a
    /// crystal, a ball, a rival. A burr is not an object: it is a cluster of a few hundred prisms
    /// laid into one trail, whose whole identity is the arena's own map of itself
    /// (<see cref="HijackYard"/>). So this provider owns a single PROXY transform and moves it
    /// onto the chosen cluster's centre - the indicator points at a place, which is what a heist
    /// objective is.</para>
    ///
    /// <para><b>"Hostile" is read LIVE and re-read, because that is the mode.</b> A burr's painted
    /// colour is only where it started; pilots flip this mass back and forth all match. A cached
    /// answer would send a pilot at a cluster they have already emptied, so the choice is
    /// recomputed on a slow cadence (<see cref="RescanSeconds"/>) rather than cached until
    /// something invalidates it - there is no event that fires on "a prism changed hands", the
    /// steal path is a per-hop local call, and adding one would put an allocation on the ride's
    /// hot path to serve an arrow.</para>
    ///
    /// <para>Cost is bounded and small: 18 burrs, each a trail-list walk, four times a second -
    /// against a scan the Rampage provider runs over every live crystal in the arena. Steady-state
    /// <see cref="TryGetObjective"/> between rescans is an O(1) transform read.</para>
    ///
    /// <para>Deliberately NOT the crystal providers' shape: the Switchyard's one omni crystal
    /// idles in the hollow core and is NOT the objective - it is an elemental pickup a pilot may
    /// take in passing. Pointing at it would aim every arrow in the match at the one place in the
    /// arena where there is nothing to steal.</para>
    /// </summary>
    public class HijackObjectiveProvider : MonoBehaviour, IObjectiveProvider
    {
        [Header("Dependencies")]
        [Inject] GameDataSO gameData;

        /// <summary>How often the burr census is re-run. Slow on purpose - a heist objective a
        /// few hundred units across does not need a per-frame answer, and the arrow swinging
        /// between two nearly-equal clusters would read as indecision.</summary>
        const float RescanSeconds = 0.25f;

        Transform _proxy;
        int _burr = -1;
        float _nextScan;

        static readonly ProfilerMarker s_TryGetObjectiveMarker =
            new("HijackObjectiveProvider.TryGetObjective");
        static readonly ProfilerMarker s_RecomputeTargetMarker =
            new("HijackObjectiveProvider.RecomputeTarget");

        public bool TryGetObjective(out Transform target)
        {
            using (s_TryGetObjectiveMarker.Auto())
            {
                var yard = HijackYard.Current;
                if (yard == null)
                {
                    // Arena not laid yet (or torn down). Hiding the arrow is the honest answer -
                    // there is nothing to point at, and a stale proxy would point at a hole.
                    target = null;
                    _burr = -1;
                    return false;
                }

                if (Time.time >= _nextScan)
                {
                    _nextScan = Time.time + RescanSeconds;
                    RecomputeTarget(yard);
                }

                if (_burr < 0 || _burr >= yard.Burrs.Count)
                {
                    target = null;
                    return false;
                }

                EnsureProxy().position = yard.BurrCentre(_burr);
                target = _proxy;
                return true;
            }
        }

        void RecomputeTarget(HijackYard yard)
        {
            using (s_RecomputeTargetMarker.Auto())
            {
                var localPlayer = gameData != null ? gameData.LocalPlayer : null;
                var localTransform = localPlayer?.Vessel?.Transform;
                var localDomain = localPlayer?.Domain ?? Domains.Blue;

                // No local vessel yet (the spawn chain is still running): any burr with loot in
                // it is a better answer than hiding the arrow through the whole countdown.
                Vector3 from = localTransform ? localTransform.position : Vector3.zero;

                _burr = yard.NearestHostileBurr(from, localDomain);
            }
        }

        Transform EnsureProxy()
        {
            if (_proxy) return _proxy;
            var go = new GameObject("HijackObjectiveTarget");
            go.transform.SetParent(transform, false);
            _proxy = go.transform;
            return _proxy;
        }
    }
}
