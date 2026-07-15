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
    /// THE canonical "lay a prism into a trail" primitive, shared by every environment builder — the
    /// static/procedural spawnables (<see cref="SpawnableBase"/>, <c>SpawnableShapeBase</c>) and the
    /// freestyle microscene conveyor (<c>Microscene</c>). Consolidates the previously-triplicated
    /// sequence — Instantiate → ChangeTeam → ownerID → pose → TargetScale → Trail → Initialize →
    /// kind → trail.Add — into one place, so a change to the prism spawn contract lands once, not
    /// three times (the drift surface the environment audit flagged).
    ///
    /// Three lay modes over the same per-prism <see cref="LayOne"/> step:
    ///   • <see cref="LaySync"/>    — lay all at once (SpawnableBase leaf spawn).
    ///   • <see cref="LayGradual"/> — one every <c>interval</c> seconds (SpawnableShapeBase reveal).
    ///   • <see cref="LayBatched"/> — a few per frame via UniTask (microscene populate — single-frame
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
    }
}
