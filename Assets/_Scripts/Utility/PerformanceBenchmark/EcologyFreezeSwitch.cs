#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The DiagnosticsHUD <c>freeze on | off</c> console command: raise or release
    /// <see cref="Cell.DiagnosticProductionHold"/> on every cell at once.
    ///
    /// Why it exists: an A/B is only valid between two arms taken in the SAME state
    /// (Docs/PERFORMANCE_OPTIMIZATION.md §4.3), and a growing world breaks that - the Lattice
    /// cell adds prisms every fauna-wave period, so the arm measured second always carries
    /// more mass than the arm measured first, and the difference between them is partly the
    /// treatment and partly the forest. Holding PRODUCTION removes the growth term.
    ///
    /// What it does NOT do, by construction: it removes nothing, ages nothing, and runs no
    /// timer. It is the same class of gate as Frenzy (which already stops planting and
    /// growth), applied to every cell regardless of phase, plus fauna seeding, reproduction
    /// and worm-colony growth. Grazing, predation, starvation and vessel abilities keep
    /// working, so a frozen world can only LOSE mass. Releasing it resumes production at the
    /// ordinary rate: every producer turns its cycle whether or not it produced, so no held
    /// wave is hatched on release.
    ///
    /// Released on any active-scene change (loudly), so a hold set for one scenario can never
    /// silently freeze the next one - an unfrozen Lattice run measured inside a frozen boot
    /// would look like a world that never grows, which is a measurement of nothing.
    /// </summary>
    public static class EcologyFreezeSwitch
    {
        const string StatsSection = "Ecology";

        public static bool IsFrozen => Cell.DiagnosticProductionHold;

        /// <summary>The console handler: <c>freeze</c> reports, <c>freeze on</c> holds, <c>freeze off</c> releases.</summary>
        public static string Handle(string[] args)
        {
            string verb = args is { Length: > 0 } ? args[0].ToLowerInvariant() : "";
            switch (verb)
            {
                case "on":  return Freeze();
                case "off": return Release();
                case "":    return (IsFrozen ? "ecology production FROZEN" : "ecology production running") +
                                   " | usage: freeze on | off";
                default:    return "usage: freeze on | off";
            }
        }

        public static string Freeze()
        {
            if (IsFrozen) return "already frozen - 'freeze off' to release";

            Cell.SetDiagnosticProductionHold(true);
            // Unsubscribe first: a hold released by a scene change and raised again must not
            // end up with two handlers.
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            DiagnosticsHUD.SetStat(StatsSection, "production", "FROZEN - 'freeze off' to release");

            return $"ecology production FROZEN in {Cell.ActiveCellsSnapshot.Count} cell(s): no growth, " +
                   "planting, reproduction or fauna seeding. Nothing is removed - grazing, predation " +
                   "and starvation continue. 'freeze off' to release.";
        }

        public static string Release()
        {
            if (!IsFrozen) return "ecology production is not frozen";

            Cell.SetDiagnosticProductionHold(false);
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            DiagnosticsHUD.ClearStats(StatsSection);
            return "ecology production released - growth and seeding resume at their ordinary rate";
        }

        /// <summary>Release a hold, if any - a frozen world must never outlive the HUD that froze it.</summary>
        public static void ReleaseIfFrozen() { if (IsFrozen) Release(); }

        static void OnActiveSceneChanged(Scene from, Scene to)
        {
            if (!IsFrozen) return;
            Release();
            Debug.LogWarning($"[DiagnosticsHUD] Scene changed to '{to.name}' - ecology freeze RELEASED. " +
                             "A hold never carries into the next scene; type 'freeze on' again if you want one.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }
}
#endif
