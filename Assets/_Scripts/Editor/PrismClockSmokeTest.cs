using System.Linq;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Play-mode smoke tests for the clock-material path (Docs/PRISM_ANIMATION.md
    /// §4.4 verification protocol). Re-blooms live prisms with a from-zero clock
    /// stamp: on a correctly wired BlockGraph every stamped prism regrows
    /// per-vertex smooth with ZERO CPU animation while colliding at full size
    /// throughout. On an unwired graph they snap and the console carries
    /// [PrismClock] errors — which is strict mode telling you what to wire.
    /// </summary>
    public static class PrismClockSmokeTest
    {
        const int MaxPrisms = 500;
        const float Radius = 400f;

        [MenuItem("FrogletTools/Ecology/Prism Animation/Smoke Test - Re-Bloom Nearby Prisms (Play Mode)")]
        public static void ReBloomNearbyPrisms()
        {
            var origin = Camera.main != null ? Camera.main.transform.position : Vector3.zero;

            var prisms = Object.FindObjectsByType<Prism>(FindObjectsSortMode.None)
                .Where(p => p != null && p.isActiveAndEnabled && !p.destroyed && p.IsCreationComplete)
                .OrderBy(p => (p.transform.position - origin).sqrMagnitude)
                .Take(MaxPrisms)
                .Where(p => (p.transform.position - origin).sqrMagnitude < Radius * Radius)
                .ToList();

            int stamped = 0;
            foreach (var prism in prisms)
            {
                if (prism.TryGetComponent(out PrismScaleAnimator animator))
                {
                    animator.BeginGrowthAnimation(resetToZero: true);
                    stamped++;
                }
            }

            Debug.Log($"[PrismClock] Smoke test: re-bloom stamped on {stamped} live prisms within {Radius:F0}u. " +
                      "EXPECT: smooth GPU regrowth (wired graph) with 0 active CPU animators, full-size collision throughout. " +
                      "SNAP + [PrismClock] errors = that material's graph still needs the §4.4 wiring.");
        }

        [MenuItem("FrogletTools/Ecology/Prism Animation/Smoke Test - Re-Bloom Nearby Prisms (Play Mode)", isValidateFunction: true)]
        static bool ValidatePlayMode() => Application.isPlaying;
    }
}
