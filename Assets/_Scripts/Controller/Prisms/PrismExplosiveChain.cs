using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The EXPLOSIVE prism state's consequence (the Manta's Space skim primes it, MANTA_STING_KABLOOM.md §3a): when an
    /// explosive prism dies, the prisms ADJACENT to it in its prismscape die too, a beat later. An explosive neighbour
    /// fires its own beat when it goes, so a primed run burns down the ribbon as a visible chain rather than vanishing
    /// in one frame - "potentially resulting in chain reactions of explosive prisms".
    ///
    /// <para><b>Adjacent in its prismscape</b> is <see cref="PrismscapeTopology"/>'s reading of membership: a prism that
    /// carries a <see cref="Trail"/> is adjacent to the members of THAT trail within reach (its ribbon, never a crossing
    /// ribbon that happens to touch it); anything else (a flora shell, a wall) is adjacent to whatever the canonical
    /// spatial index finds within reach. Reach is measured in multiples of the dead prism's own largest extent, so the
    /// rule is scale-free. Every neighbour takes an ordinary <see cref="Prism.Damage"/>: a shield pops instead of the
    /// prism and a super-shield deflects, exactly as any other hit - the chain is a damage source, not a bypass.</para>
    ///
    /// <para><b>Slow on purpose.</b> Each beat waits <see cref="BeatSeconds"/> and at most <see cref="DamagesPerFrame"/>
    /// neighbours are resolved a frame; the rest wait for the next frame (QUEUED, not dropped - unlike
    /// <see cref="ChainReactionBudget"/>, whose volleys spawn colliders, this only spends damage calls, and a chain that
    /// silently stopped would read as the state not working). The pace is the spectacle and the frame budget at once.</para>
    ///
    /// Local, like the skim that primes it: every machine that saw the prime runs its own chain off its own deaths.
    /// </summary>
    public static class PrismExplosiveChain
    {
        static readonly ProfilerMarker StepMarker = new("PrismExplosiveChain.Step");

        /// <summary>Seconds between an explosive prism's death and its neighbours'.</summary>
        public static float BeatSeconds = 0.12f;

        /// <summary>Neighbour reach, in multiples of the dead prism's largest world extent.</summary>
        public static float ReachInExtents = 1.6f;

        /// <summary>Neighbour damage calls allowed per frame across every chain.</summary>
        public static int DamagesPerFrame = 24;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            BeatSeconds = 0.12f;
            ReachInExtents = 1.6f;
            DamagesPerFrame = 24;
            s_pending.Clear();
            s_hits.Clear();
            s_runner = null;
        }

        struct Beat
        {
            public Vector3 Position;
            public float Reach;
            public Trail Trail;
            public Domains Domain;
            public string Attacker;
            public float Due;
        }

        struct Hit
        {
            public Prism Prism;
            public int Epoch;
            public Vector3 From;
            public Domains Domain;
            public string Attacker;
        }

        static readonly List<Beat> s_pending = new();
        static readonly Queue<Hit> s_hits = new();
        static readonly List<Prism> s_scratch = new();
        static Runner s_runner;

        /// <summary>Beats still waiting plus neighbour hits still queued - a diagnostics/test read.</summary>
        public static int PendingCount => s_pending.Count + s_hits.Count;

        /// <summary>
        /// An explosive prism just died at <paramref name="position"/>: schedule its neighbours' deaths. Called once per
        /// death by <see cref="Prism"/>'s destruction setup, which spends the flag first.
        /// </summary>
        public static void Fire(Prism source, Vector3 position, Vector3 lossyScale, Domains domain, string attacker)
        {
            float extent = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
            s_pending.Add(new Beat
            {
                Position = position,
                Reach = Mathf.Max(1f, extent) * Mathf.Max(0.5f, ReachInExtents),
                Trail = source ? source.Trail : null,
                Domain = domain,
                Attacker = attacker,
                Due = Time.time + Mathf.Max(0f, BeatSeconds),
            });
            EnsureRunner();
        }

        static void EnsureRunner()
        {
            if (s_runner) return;
            var go = new GameObject("[PrismExplosiveChain]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            s_runner = go.AddComponent<Runner>();
        }

        static void Step()
        {
            using var _ = StepMarker.Auto();
            float now = Time.time;

            // Resolve every beat that is due into the neighbours it kills.
            for (int i = s_pending.Count - 1; i >= 0; i--)
            {
                var beat = s_pending[i];
                if (beat.Due > now) continue;
                s_pending.RemoveAt(i);
                Gather(beat);
            }

            // Then spend this frame's damage budget on the queue, oldest first.
            int budget = Mathf.Max(1, DamagesPerFrame);
            while (budget > 0 && s_hits.Count > 0)
            {
                var hit = s_hits.Dequeue();
                var prism = hit.Prism;
                // The epoch test: a neighbour that died and was recycled into a NEW life since it was queued is not
                // the prism this beat meant, and must not be killed for the old one's neighbour.
                if (!prism || prism.destroyed || prism.DestroyEpoch != hit.Epoch) continue;
                budget--;
                Vector3 away = prism.transform.position - hit.From;
                if (away.sqrMagnitude < 1e-4f) away = Random.onUnitSphere;
                prism.Damage(away.normalized * 40f, hit.Domain, hit.Attacker);
            }
        }

        static void Gather(in Beat beat)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null) return;
            s_scratch.Clear();
            index.QuerySphere(beat.Position, beat.Reach, s_scratch);
            for (int i = 0; i < s_scratch.Count; i++)
            {
                var p = s_scratch[i];
                if (!p || p.destroyed) continue;
                // A ribbon's neighbours are its own members; a crossing ribbon is another prismscape.
                if (beat.Trail != null && p.Trail != beat.Trail) continue;
                s_hits.Enqueue(new Hit
                {
                    Prism = p,
                    Epoch = p.DestroyEpoch,
                    From = beat.Position,
                    Domain = beat.Domain,
                    Attacker = beat.Attacker,
                });
            }
            s_scratch.Clear();
        }

        sealed class Runner : MonoBehaviour
        {
            void Update()
            {
                if (s_pending.Count == 0 && s_hits.Count == 0) return;
                try { Step(); }
                catch (System.Exception ex) { CSDebug.LogError($"[PrismExplosiveChain] {ex}"); }
            }
        }
    }
}
