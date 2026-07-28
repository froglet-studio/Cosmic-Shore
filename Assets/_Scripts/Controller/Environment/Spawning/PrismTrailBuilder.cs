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

            ConfigureLaid(block, e, trail, ownerId, t);
            return block;
        }

        /// <summary>
        /// Everything after the clone exists: team, pose, scale, trail wiring, Initialize, kind,
        /// reveal-watch. Split out of <see cref="LayOne"/> so the BATCHED clone path shares one
        /// definition of the prism spawn contract with the per-item path (the drift surface the
        /// environment audit flagged — now it cannot diverge).
        /// </summary>
        static void ConfigureLaid(Prism block, in PrismLay e, Trail trail, string ownerId, long t)
        {
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

            // Arena-ready gate: every environment-laid prism is watched until it is SETTLED FOR
            // REVEAL — created (renderer on; creation completions are frame-budgeted, so a laid
            // prism stays invisible until the queue reaches it) AND at final scale. Laid or
            // grown is not enough: un-created prisms pop in batches after the match starts.
            WatchForReveal(block);
        }

        /// <summary>
        /// Register a prism with the arena-ready gate's reveal watch. LayOne does this for every
        /// prism it lays; spawnables with CUSTOM instantiate loops (e.g. SpawnableGyroid's
        /// assembler walk) must call it per block, or their prisms pop in after the connecting
        /// screen drops (the creation queue is frame-budgeted). Self-prunes so ungated contexts
        /// (menu conveyor, per-turn courses) never accumulate settled entries.
        /// </summary>
        public static void WatchForReveal(Prism block)
        {
            if (!block) return;
            s_growWatch.Add(block);
            if ((s_growWatch.Count & 511) == 0) SweepGrowWatch();
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

        // Prisms laid by this builder that are not yet settled for reveal (creation queue not
        // reached them, or grow-in unfinished). Swept (swap-remove) by the gate each poll and
        // pruned periodically on add, so ungated contexts don't leak.
        static readonly List<Prism> s_growWatch = new(1024);

        // Builds announced (BeginArenaBuild) but not yet executed — covers the window where a
        // controller is still WAITING to build (e.g. HexRace's netcode track-seed wait) and no
        // lay has started, which absence-of-activity checks would misread as "arena done".
        static int s_pendingArenaBuilds;

        // Load-gate session state: set while MiniGameHUD holds the connecting screen on this
        // gate. Read by PrismScaleManager (grow-in stepping boost) and Prism (creation-queue
        // boost) so the arena finishes materializing behind the covered screen.
        static bool s_loadGateHolding;
        static float s_loadGateStartTime;
        static float s_allClearSince = -1f;
        static int s_settleSpan = -1;

        /// <summary>Hard cap on the load-gate hold — releases with an error instead of holding a
        /// broken build forever (a wedged build must surface loud, not as an infinite screen).</summary>
        const float LoadGateHardCapSeconds = 180f;

        /// <summary>The all-clear must hold this long before the gate releases — bridges any
        /// same-frame gaps between async build steps (a lay finishing while another spawnable
        /// is still about to schedule its own) so a momentary zero can't slip the screen open.</summary>
        const float ReadyStableSeconds = 0.5f;

        /// <summary>Grow-in snaps applied per gate poll — bounds the per-frame cost of
        /// force-settling a 25k cohort (each snap runs full completion bookkeeping).</summary>
        const int SettleSnapsPerPoll = 2000;

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
            s_allClearSince = -1f;
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
        /// has executed, every streamed lay has drained, and every laid prism is settled for
        /// reveal — creation complete (renderer ON — creation completions are frame-budgeted,
        /// so scale alone proves nothing) AND at final scale. Stragglers are force-settled
        /// behind the covered screen, and the all-clear must hold ReadyStableSeconds before the
        /// gate releases. Nothing lays, materializes, or blooms after this returns true.
        /// </summary>
        public static bool PollArenaReady()
        {
            if (s_loadGateHolding && Time.unscaledTime - s_loadGateStartTime > LoadGateHardCapSeconds)
            {
                Debug.LogError($"[PrismTrailBuilder] Arena build exceeded the {LoadGateHardCapSeconds:F0}s " +
                               $"hold cap (pendingBuilds={s_pendingArenaBuilds}, lays={s_activeBudgetedLays}, " +
                               $"settling={GrowRemainingCount}) — releasing the gate so the match can start. " +
                               "Either the build wedged or the load is pathologically slow; capture a " +
                               "Load Time Insights recording to see which.");
                EndSettleSpan();
                return true;
            }

            if (s_pendingArenaBuilds > 0 || s_activeBudgetedLays > 0)
            {
                s_allClearSince = -1f;
                return false;
            }

            if (SettleGrowWatch(SettleSnapsPerPoll) > 0)
            {
                s_allClearSince = -1f;
                // Everything is laid; the cohort is materializing/settling behind the covered
                // screen. Own span so Load Time Insights attributes this window to the
                // environment (starts after the lay spans end, so it never steals their
                // attribution).
                if (s_settleSpan == -1)
                    s_settleSpan = LoadInsights.Begin(LoadInsightCategory.Environment,
                        "Arena settle: creation queue + grow-in (behind connecting screen)");
                return false;
            }

            // All clear — require it to HOLD before releasing, so a momentary zero between
            // async build steps can't slip the screen open onto a still-materializing arena.
            if (s_allClearSince < 0f) s_allClearSince = Time.unscaledTime;
            if (Time.unscaledTime - s_allClearSince < ReadyStableSeconds) return false;

            EndSettleSpan();
            return true;
        }

        static void EndSettleSpan()
        {
            if (s_settleSpan == -1) return;
            LoadInsights.End(s_settleSpan);
            s_settleSpan = -1;
        }

        /// <summary>
        /// Gate-only pass: force-settle watched prisms that are created but still growing (the
        /// screen is covered — snapping is invisible and saves the multi-second grow-in tail),
        /// drop everything settled or dead, keep prisms whose creation the frame-budgeted
        /// queue hasn't reached yet. Returns (and caches) how many are still not reveal-ready.
        /// </summary>
        static int SettleGrowWatch(int snapBudget)
        {
            var list = s_growWatch;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                bool drop;
                if (p == null || !p.isActiveAndEnabled)
                {
                    drop = true; // destroyed / pooled away — can never pop in later
                }
                else
                {
                    if (snapBudget > 0 && p.IsCreationComplete && !p.IsSettledForReveal)
                    {
                        p.CompleteGrowthImmediately();
                        snapBudget--;
                    }
                    drop = p.IsSettledForReveal;
                }

                if (drop)
                {
                    int last = list.Count - 1;
                    list[i] = list[last];
                    list.RemoveAt(last);
                }
            }
            GrowRemainingCount = list.Count;
            return list.Count;
        }

        /// <summary>Maintenance sweep (prune-on-add + gate arming): drop dead and reveal-ready
        /// entries WITHOUT force-settling — outside the gate the world is visible, so grow-ins
        /// must finish naturally (continuity of existence).</summary>
        static int SweepGrowWatch()
        {
            var list = s_growWatch;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                if (p == null || !p.isActiveAndEnabled || p.IsSettledForReveal)
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

        // ── Batched clone (Unity 6 multithreaded InstantiateAsync) ───────────

        /// <summary>
        /// Prisms cloned per InstantiateAsync call. The engine spreads the clone/deserialize work
        /// for one call across worker threads, so bigger batches parallelize better — but the
        /// whole batch integrates before we can configure any of it, so an oversized batch stalls
        /// the progress readout. A few hundred keeps both.
        /// </summary>
        const int CloneBatchSize = 256;

        /// <summary>Clones per chunk on the per-item path — small enough that overshooting the
        /// frame budget by at most one chunk stays invisible in the build readout.</summary>
        const int SyncCloneChunk = 24;

        /// <summary>
        /// OFF by default — measured REGRESSION, do not flip without re-measuring.
        /// <para>
        /// AsyncInstantiateOperation integrates its clones on the main thread under its own
        /// per-frame time budget (~10 ms by default). That budget, not our lay budget, then
        /// governs the build: a structure needing ~15 s of integration is throttled to ~10 ms
        /// per frame, so it stretches to 25 s+ instead of speeding up. The worker-thread clone
        /// win is real but far smaller than the throttle it imposes.
        /// </para>
        /// Re-enabling requires raising the integration budget for the covered window
        /// (AsyncInstantiateOperation.SetIntegrationTimeMS) and proving the result with a Load
        /// Time Insights capture — the code path is kept for exactly that experiment.
        /// </summary>
        public static bool UseBatchedInstantiate = false;

        /// <summary>
        /// Clone <paramref name="count"/> prisms as children of <paramref name="parent"/> using
        /// Unity 6's multithreaded batched instantiate. Raw per-item Instantiate was ~97% of a
        /// mass environment lay and is the one part the engine can parallelize; everything after
        /// the clone (Awake integration, our spawn contract) still runs on the main thread.
        /// Falls back to per-item cloning if the batched path is unavailable or returns short.
        /// </summary>
        static async UniTask<Prism[]> CloneBatchAsync(Prism prefab, int count, Transform parent)
        {
            if (UseBatchedInstantiate)
            {
                try
                {
                    var op = UnityEngine.Object.InstantiateAsync(prefab, count, parent);
                    while (!op.isDone)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                        if (!parent) return null; // container destroyed mid-flight
                    }

                    var result = op.Result;
                    if (result != null && result.Length == count) return result;
                }
                catch (Exception ex)
                {
                    // Any failure demotes the whole session to the per-item path — the sync
                    // clone below is the behavioural baseline, so a level still builds.
                    UseBatchedInstantiate = false;
                    Debug.LogWarning($"[PrismTrailBuilder] Batched InstantiateAsync failed " +
                                     $"({ex.GetType().Name}: {ex.Message}) — falling back to per-item cloning.");
                }
            }

            if (!parent) return null;
            var clones = new Prism[count];
            for (int i = 0; i < count; i++)
                clones[i] = UnityEngine.Object.Instantiate(prefab, parent);
            return clones;
        }

        /// <summary>
        /// Frame-time-budgeted lay for BIG structures: clones prisms in multithreaded batches,
        /// then applies the spawn contract until <paramref name="budgetMsPerFrame"/> of laying
        /// time has been spent this frame (across ALL budgeted lays) and yields — the structure
        /// builds over frames instead of freezing one (Load Time Insights measured a 25k-prism
        /// geodesic shell at ~95s in a single frame, ~97% of it raw Instantiate cost; per-prism
        /// cost varies with scene size, so a count-per-frame batch can't hold a frame budget — a
        /// time budget can). While the load gate holds the connecting screen the budget is raised
        /// (the screen is covered, so a bigger slice per frame is pure throughput — only the
        /// progress readout needs frames). Bails silently if <paramref name="parent"/> dies
        /// (scene unload / container nuked) — the remaining prisms are simply never born, which
        /// conserves mass.
        /// </summary>
        public static UniTaskVoid LayBudgetedAsync(Prism prefab, SpawnPoint[] points, Domains domain,
            Transform parent, Trail trail, string ownerPrefix, float budgetMsPerFrame)
        {
            if (!prefab || points == null || points.Length == 0) return default;
            var elems = new PrismLay[points.Length];
            for (int i = 0; i < points.Length; i++) elems[i] = new PrismLay(points[i], domain);
            return LayBudgetedAsync(prefab, elems, parent, trail, ownerPrefix, budgetMsPerFrame);
        }

        /// <summary>Per-item-domain overload (custom assembler loops: gyroid, SchwarzP surface…).</summary>
        public static async UniTaskVoid LayBudgetedAsync(Prism prefab, IReadOnlyList<PrismLay> elems,
            Transform parent, Trail trail, string ownerPrefix, float budgetMsPerFrame)
        {
            if (!prefab || elems == null || elems.Count == 0) return;

            int count = elems.Count;

            // Fresh batch: reset the shared progress counters once the previous batch fully drained.
            if (s_activeBudgetedLays == 0)
            {
                s_layQueuedTotal = 0;
                s_layDoneTotal = 0;
            }
            s_layQueuedTotal += count;
            s_activeBudgetedLays++;

            // Wall-clock span for the whole streamed lay: with the connecting panel holding on
            // the arena-ready gate, this span is what attributes the hold window to the
            // environment in a Load Time Insights recording (the per-stage detail lives in the
            // ConfigureLaid accumulators). No-op when not recording.
            int laySpan = LoadInsights.Begin(LoadInsightCategory.Environment,
                $"Streamed prism lay ({ownerPrefix}, {count} prisms)");
            try
            {
                int i = 0;
                while (i < count)
                {
                    if (!parent) return; // container destroyed — stop laying

                    // On the per-item path a batch is cloned in one go with no budget check
                    // inside it, so keep batches small there or the frame budget is overshot by
                    // a whole batch (the progress readout stops ticking). The batched path wants
                    // the opposite: bigger batches parallelize better.
                    int batchCap = UseBatchedInstantiate ? CloneBatchSize : SyncCloneChunk;
                    int batch = Mathf.Min(batchCap, count - i);
                    var clones = await CloneBatchAsync(prefab, batch, parent);
                    if (clones == null || !parent) return;

                    for (int k = 0; k < batch; k++)
                    {
                        var block = clones[k];
                        if (!block) continue;

                        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                        long acc = LoadInsights.AccumulateStart();
                        if (acc != 0L) LoadInsights.Count("Prisms laid during load");
                        ConfigureLaid(block, elems[i + k], trail, $"{ownerPrefix}::{i + k}", acc);
                        s_budgetSpentMs += (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * s_msPerTick;
                        s_layDoneTotal++;

                        while (BudgetExhausted(EffectiveLayBudget(budgetMsPerFrame)))
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update);
                            if (!parent) return;
                        }
                    }

                    i += batch;
                }
            }
            finally
            {
                s_activeBudgetedLays--;
                LoadInsights.End(laySpan);
            }
        }

        /// <summary>
        /// Per-frame laying slice. Behind the covered connecting screen there is nothing to keep
        /// smooth, so the slice is raised: the only reason to yield at all there is to tick the
        /// build readout, and a bigger slice means less of each frame lost to non-laying overhead.
        /// </summary>
        static float EffectiveLayBudget(float authoredBudgetMs)
        {
            float budget = Mathf.Max(0.5f, authoredBudgetMs);
            return s_loadGateHolding ? Mathf.Max(budget, LoadGateLayBudgetMs) : budget;
        }

        /// <summary>Laying slice per frame while the load gate holds (~4 readout updates/second).</summary>
        const float LoadGateLayBudgetMs = 250f;
    }
}
