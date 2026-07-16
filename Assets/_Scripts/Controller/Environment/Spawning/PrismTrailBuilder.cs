using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility.PerformanceBenchmark;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One prism to lay: a pose/scale (<see cref="SpawnPoint"/>) + its domain colour + gameplay kind.</summary>
    public readonly struct PrismLay
    {
        public readonly SpawnPoint Point;
        public readonly Domains Domain;
        public readonly PrismKind Kind;

        public PrismLay(SpawnPoint point, Domains domain, PrismKind kind = PrismKind.Plain)
        {
            Point = point;
            Domain = domain;
            Kind = kind;
        }
    }

    /// <summary>
    /// THE canonical "lay a prism into a trail" primitive, shared by every environment builder - the
    /// static/procedural spawnables (<see cref="SpawnableBase"/>, <c>SpawnableShapeBase</c>) and the
    /// freestyle microscene conveyor (<c>Microscene</c>). Consolidates the previously-triplicated
    /// sequence - Instantiate → ChangeTeam → ownerID → pose → TargetScale → Trail → Initialize →
    /// kind → trail.Add - into one place, so a change to the prism spawn contract lands once, not
    /// three times (the drift surface the environment audit flagged).
    ///
    /// Three lay modes over the same per-prism <see cref="LayOne"/> step:
    ///   • <see cref="LaySync"/>    - lay all at once (SpawnableBase leaf spawn).
    ///   • <see cref="LayGradual"/> - one every <c>interval</c> seconds (SpawnableShapeBase reveal).
    ///   • <see cref="LayBatched"/> - a few per frame via UniTask (microscene populate - single-frame
    ///                                 prism batches are a known spike).
    /// </summary>
    public static class PrismTrailBuilder
    {
        /// <summary>The one place a prism is born into a trail. Kind is applied AFTER Initialize.</summary>
        public static Prism LayOne(Prism prefab, PrismLay e, Transform parent, Trail trail, string ownerId)
        {
            // Load Time Insights hot-path breakdown: per-stage accumulators (NOT per-item spans —
            // a 25k-prism lay would blow the span budget). Inert (t stays 0) unless a load
            // recording is active, so gameplay laying pays only a long-compare per stage.
            long t = LoadInsights.AccumulateStart();
            if (t != 0L) LoadInsights.Count("Prisms laid during load");

            var block = UnityEngine.Object.Instantiate(prefab, parent);
            t = LoadInsights.AccumulateSample("Prism lay: Instantiate + component Awakes", t);

            block.ChangeTeam(e.Domain);
            block.ownerID = ownerId;
            block.transform.localPosition = e.Point.Position;
            block.transform.localRotation = e.Point.Rotation;
            t = LoadInsights.AccumulateSample("Prism lay: team + pose", t);

            block.TargetScale = e.Point.Scale;
            t = LoadInsights.AccumulateSample("Prism lay: TargetScale (scale-manager registration)", t);

            block.Trail = trail;
            block.Initialize();
            t = LoadInsights.AccumulateSample("Prism lay: Initialize (reset + grow coroutine start)", t);

            PrismKinds.Apply(block, e.Kind); // additive: Plain leaves baked/prefab state intact
            trail.Add(block);
            LoadInsights.AccumulateSample("Prism lay: kind + trail.Add", t);

            // Arena-ready gate: every environment-laid prism is watched until its grow-in
            // settles, so the connecting screen can hold until the arena is fully GROWN, not
            // just fully laid (slice-starved growth left freshly-laid prisms invisible at
            // reveal, popping in waves during play). Self-prunes so ungated contexts (menu
            // conveyor, per-turn courses) never accumulate settled entries.
            s_growWatch.Add(block);
            if ((s_growWatch.Count & 511) == 0) SweepGrowWatch();
            return block;
        }

        // ── Sync ─────────────────────────────────────────────────────────────

        public static void LaySync(Prism prefab, IReadOnlyList<PrismLay> elems, Transform parent, Trail trail, string ownerPrefix)
        {
            if (!prefab) return;
            for (int i = 0; i < elems.Count; i++)
                LayOne(prefab, elems[i], parent, trail, $"{ownerPrefix}::{i}");
        }

        /// <summary>Convenience overload for the single-domain, plain-kind environment path.</summary>
        public static void LaySync(Prism prefab, SpawnPoint[] points, Domains domain, Transform parent, Trail trail, string ownerPrefix)
        {
            if (!prefab) return;
            for (int i = 0; i < points.Length; i++)
                LayOne(prefab, new PrismLay(points[i], domain), parent, trail, $"{ownerPrefix}::{i}");
        }

        // ── Gradual (coroutine) ──────────────────────────────────────────────

        /// <summary>Single-domain, plain-kind gradual reveal (SpawnableShapeBase). Bails if <paramref name="parent"/> dies.</summary>
        public static IEnumerator LayGradual(Prism prefab, SpawnPoint[] points, Domains domain, Transform parent,
            Trail trail, string ownerPrefix, float interval, Action<Prism> onEach = null)
        {
            if (!prefab) yield break;
            for (int i = 0; i < points.Length; i++)
            {
                if (!parent) yield break;
                var block = LayOne(prefab, new PrismLay(points[i], domain), parent, trail, $"{ownerPrefix}::{i}");
                onEach?.Invoke(block);
                if (interval > 0f) yield return new WaitForSeconds(interval);
            }
        }

        // ── Batched (UniTask, a few per frame) ───────────────────────────────

        public static async UniTask LayBatched(Prism prefab, IReadOnlyList<PrismLay> elems, Transform parent,
            Trail trail, string ownerPrefix, int perFrame, CancellationToken ct, List<Prism> collected = null)
        {
            if (!prefab) return;
            perFrame = Mathf.Max(1, perFrame);
            for (int i = 0; i < elems.Count; i++)
            {
                var block = LayOne(prefab, elems[i], parent, trail, $"{ownerPrefix}::{i}");
                collected?.Add(block);
                if ((i + 1) % perFrame == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        // ── Budgeted (UniTask, N milliseconds per frame, budget shared globally) ──

        // All budgeted lays draw from ONE per-frame time pool, so three concurrently-streaming
        // shells cost max(budget) per frame, not 3 × budget.
        static readonly double s_msPerTick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        static int s_budgetFrame = -1;
        static double s_budgetSpentMs;
        static int s_activeBudgetedLays;
        static int s_layQueuedTotal;
        static int s_layDoneTotal;

        /// <summary>
        /// True while any budgeted lay is still placing prisms. Part of the arena-ready gate
        /// (see <see cref="PollArenaReady"/>) so the arena is COMPLETE before the player ever
        /// sees the world — prisms may bloom behind the loading screen, never during play.
        /// </summary>
        public static bool IsLayingInProgress => s_activeBudgetedLays > 0;

        // ── Arena-ready gate (pending builds + lays + grow-in, all done) ─────

        // Prisms laid by this builder that are still playing their grow-in. Swept (swap-remove)
        // by the gate each poll and pruned periodically on add, so ungated contexts don't leak.
        static readonly List<Prism> s_growWatch = new(1024);

        // Builds announced (BeginArenaBuild) but not yet executed — covers the window where a
        // controller is still WAITING to build (e.g. HexRace's netcode track-seed wait) and no
        // lay has started, which absence-of-activity checks would misread as "arena done".
        static int s_pendingArenaBuilds;

        // Load-gate session state: set while MiniGameHUD holds the connecting screen on this
        // gate. Read by PrismScaleManager to boost grow-in stepping behind the covered screen.
        static bool s_loadGateHolding;
        static float s_loadGateStartTime;
        static int s_settleSpan = -1;

        /// <summary>Hard cap on the load-gate hold — releases with an error instead of holding a
        /// broken build forever (a wedged build must surface loud, not as an infinite screen).</summary>
        const float LoadGateHardCapSeconds = 180f;

        /// <summary>
        /// Announce an arena build whose SegmentSpawner.Initialize happens LATER than scene
        /// start (e.g. HexRace initializes only after the netcode track seed arrives). While
        /// any build is pending, the arena-ready gate stays closed even though no lay has
        /// started yet. Pair with exactly one <see cref="EndArenaBuild"/>.
        /// </summary>
        public static void BeginArenaBuild() => s_pendingArenaBuilds++;

        /// <summary>Close a <see cref="BeginArenaBuild"/> bracket (idempotence is the caller's job).</summary>
        public static void EndArenaBuild() => s_pendingArenaBuilds = Mathf.Max(0, s_pendingArenaBuilds - 1);

        /// <summary>Laid prisms still growing in, as of the last gate sweep (progress readouts).</summary>
        public static int GrowRemainingCount { get; private set; }

        /// <summary>
        /// True while the loading gate is holding the connecting screen on this builder.
        /// PrismScaleManager boosts grow-in stepping while this is set — the screen is covered,
        /// so frames are free to settle the arena cohort at full tempo.
        /// </summary>
        public static bool IsLoadGateHolding => s_loadGateHolding;

        /// <summary>Bracket the connecting-screen hold (MiniGameHUD). Stamps the hard-cap clock.
        /// Both edges close any settle span left open by an aborted/cancelled hold — a stale
        /// handle would otherwise block the next load's settle span from ever opening.</summary>
        public static void SetLoadGateHolding(bool holding)
        {
            s_loadGateHolding = holding;
            if (holding)
            {
                s_loadGateStartTime = Time.unscaledTime;
                // Fresh readout for this load: purge last match's (destroyed) entries so the
                // panel never shows a stale grow count during the dwell.
                SweepGrowWatch();
            }
            EndSettleSpan();
        }

        /// <summary>
        /// THE arena-complete predicate the connecting screen holds on: every announced build
        /// has executed, every streamed lay has drained, and every laid prism has finished its
        /// grow-in. Only when all three are true is the arena what the player will see for the
        /// rest of the match — nothing lays or blooms after this returns true.
        /// </summary>
        public static bool PollArenaReady()
        {
            if (s_loadGateHolding && Time.unscaledTime - s_loadGateStartTime > LoadGateHardCapSeconds)
            {
                Debug.LogError($"[PrismTrailBuilder] Arena build exceeded the {LoadGateHardCapSeconds:F0}s " +
                               $"hold cap (pendingBuilds={s_pendingArenaBuilds}, lays={s_activeBudgetedLays}, " +
                               $"growing={GrowRemainingCount}) — releasing the gate so the match can start. " +
                               "Either the build wedged or the load is pathologically slow; capture a " +
                               "Load Time Insights recording to see which.");
                EndSettleSpan();
                return true;
            }

            if (s_pendingArenaBuilds > 0 || s_activeBudgetedLays > 0) return false;

            if (SweepGrowWatch() > 0)
            {
                // Everything is laid; the cohort is settling behind the covered screen. Own
                // span so Load Time Insights attributes this window to the environment
                // (starts after the lay spans end, so it never steals their attribution).
                if (s_settleSpan == -1)
                    s_settleSpan = LoadInsights.Begin(LoadInsightCategory.Environment,
                        "Arena grow-in settle (behind connecting screen)");
                return false;
            }

            EndSettleSpan();
            return true;
        }

        static void EndSettleSpan()
        {
            if (s_settleSpan == -1) return;
            LoadInsights.End(s_settleSpan);
            s_settleSpan = -1;
        }

        /// <summary>Swap-remove settled/dead entries; returns (and caches) how many still grow.</summary>
        static int SweepGrowWatch()
        {
            var list = s_growWatch;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                if (p == null || !p.IsGrowing)
                {
                    int last = list.Count - 1;
                    list[i] = list[last];
                    list.RemoveAt(last);
                }
            }
            GrowRemainingCount = list.Count;
            return list.Count;
        }

        /// <summary>Prisms laid so far in the current budgeted batch (for progress readouts).</summary>
        public static int LayDoneCount => s_layDoneTotal;

        /// <summary>Total prisms queued in the current budgeted batch (for progress readouts).</summary>
        public static int LayQueuedCount => s_layQueuedTotal;

        /// <summary>0..1 progress of the current budgeted batch (1 when idle).</summary>
        public static float LayProgress =>
            s_layQueuedTotal <= 0 ? 1f : Mathf.Clamp01((float)s_layDoneTotal / s_layQueuedTotal);

        static bool BudgetExhausted(float budgetMs)
        {
            if (Time.frameCount != s_budgetFrame)
            {
                s_budgetFrame = Time.frameCount;
                s_budgetSpentMs = 0.0;
            }
            return s_budgetSpentMs >= budgetMs;
        }

        /// <summary>
        /// Frame-time-budgeted lay for BIG decorative structures: lays prisms until
        /// <paramref name="budgetMsPerFrame"/> of laying time has been spent this frame (across
        /// ALL budgeted lays), then yields — the structure blooms in over frames instead of
        /// freezing one (Load Time Insights measured a 25k-prism geodesic shell at ~95s in a
        /// single frame, ~97% of it raw Instantiate cost; per-prism cost varies with scene size,
        /// so a count-per-frame batch can't hold a frame budget — a time budget can). Bails
        /// silently if <paramref name="parent"/> dies (scene unload / container nuked) — the
        /// remaining prisms are simply never born, which conserves mass.
        /// </summary>
        public static async UniTaskVoid LayBudgetedAsync(Prism prefab, SpawnPoint[] points, Domains domain,
            Transform parent, Trail trail, string ownerPrefix, float budgetMsPerFrame)
        {
            if (!prefab || points == null || points.Length == 0) return;

            float budget = Mathf.Max(0.5f, budgetMsPerFrame);

            // Fresh batch: reset the shared progress counters once the previous batch fully drained.
            if (s_activeBudgetedLays == 0)
            {
                s_layQueuedTotal = 0;
                s_layDoneTotal = 0;
            }
            s_layQueuedTotal += points.Length;
            s_activeBudgetedLays++;

            // Wall-clock span for the whole streamed lay: with the connecting panel holding on
            // IsLayingInProgress, this span is what attributes the hold window to the
            // environment in a Load Time Insights recording (the per-stage detail lives in the
            // LayOne accumulators). No-op when not recording.
            int laySpan = LoadInsights.Begin(LoadInsightCategory.Environment,
                $"Streamed prism lay ({ownerPrefix}, {points.Length} prisms)");
            try
            {
                for (int i = 0; i < points.Length; i++)
                {
                    if (!parent) return; // container destroyed — stop laying

                    while (BudgetExhausted(budget))
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                        if (!parent) return;
                    }

                    long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                    LayOne(prefab, new PrismLay(points[i], domain), parent, trail, $"{ownerPrefix}::{i}");
                    s_budgetSpentMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * s_msPerTick;
                    s_layDoneTotal++;
                }
            }
            finally
            {
                s_activeBudgetedLays--;
                LoadInsights.End(laySpan);
            }
        }
    }
}
