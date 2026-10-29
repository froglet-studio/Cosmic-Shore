using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Returns DEAD prisms to their pool once their death effect has played out (Docs/PERFORMANCE_OPTIMIZATION.md
    /// §2, row 10-29). A destroyed prism used to stay an ACTIVE, hidden, collider-less GameObject in
    /// its trail forever: nothing weighed it, nothing drew it, but every one kept its GameObject, its render-entity
    /// slot and its trail entry, so an idle Lavalamp (AI vessels laying and shooting forever) climbed without bound
    /// in GameObject count, trail length and memory until frame time and then the process gave out.
    ///
    /// <para>This is not a cap and not a TTL on LIVE mass (mass conservation): only prisms that are already
    /// <c>destroyed</c> - mass the food web or a weapon has already taken out of the world - are recycled, and
    /// only after <see cref="DeathEffectSeconds"/> so the explosion/implosion read and every "recently destroyed"
    /// query (DestroyedAt, IsSeated, LastFallenHullPrism) still see the corpse while it matters.</para>
    ///
    /// <para><b>Who is recycled</b> (both are opt-in at the owner, never inferred):
    /// <list type="bullet">
    /// <item>Vessel-trail prisms: issued by the vessel prism pool (<c>OnReturnToPool</c> wired), still a member of
    /// a trail that opted in with <see cref="Trail.RecycleDestroyedMembers"/> (only the vessel's own primary and
    /// secondary trails do). They leave the trail in one batched compaction per trail per frame
    /// (<see cref="Trail.RemoveMembers"/>), then <see cref="Prism.ReturnToPool"/>.</item>
    /// <item>Environment free mass: an <see cref="EnvironmentPrismPool"/> prism stamped
    /// <see cref="Prism.RecyclesWhenDestroyed"/> (a natural-death skeleton) with no trail container, released
    /// with <see cref="EnvironmentPrismPool.TryRelease"/>.</item>
    /// </list></para>
    ///
    /// <para><b>Who is NOT</b>, and why (each owner keeps a reference to its dead prisms and owns their exit):
    /// Ark hull and wake, Hijack yards and every builder-laid environment trail (environment prisms WITH a trail:
    /// the Ark seats/regrows by identity, the Hijack ledger addresses yard trails by append-only index);
    /// flora and fauna body prisms (spindle/LifeForm trackers, occupant arrays - LifeForm.Die and the cell drain
    /// own them); a prism a front-remover already took out of its trail (Wanderway, Arkway, RaceTrailCap, Ark
    /// wake - <c>GetBlockIndex &lt; 0</c>); skeletons an ability plucked (Meerkat swallow, Jacks' net - the
    /// executor keeps holding them); every trail that did not opt in (projectiles, executors, pens); and,
    /// while any Urchin exists, ALL vessel-trail prisms, because GunVesselTransformer restores destroyed prisms it
    /// rides over (<see cref="AddTrailRestorer"/>).</para>
    ///
    /// <para><b>Safety</b>: every entry carries the prism's <see cref="Prism.DestroyEpoch"/>. An entry is acted on
    /// only if the prism is still destroyed, still active, and on the same death - so a Restore, a pool reuse, a
    /// second death or an owner that already returned/parked it all drop the entry. Both pool returns are
    /// idempotent besides. Work is capped at <see cref="MaxPerFrame"/> entries a frame.</para>
    /// </summary>
    public static class PrismRecycler
    {
        /// <summary>Longest death effect (PrismExplosion's authored lifetime; implosion is 2 s). The VFX do not hold
        /// the prism, this delay is only so the corpse outlives its own effect and the "recently destroyed" reads.</summary>
        public const float DeathEffectSeconds = PrismExplosion.DefaultDuration;

        /// <summary>Master switch (diagnostics / A-B profiling). Off: entries are dropped, nothing is recycled.</summary>
        public static bool Enabled = true;

        /// <summary>Queue entries examined per frame. The per-entry cost is a pool return (SetActive false, reparent);
        /// trail compaction is one pass per touched trail per frame on top.</summary>
        public static int MaxPerFrame = 64;

        static readonly Queue<(Prism prism, int epoch, float dueAt)> s_queue = new(1024);
        static readonly List<(Trail trail, Prism prism)> s_trailBatch = new(64);
        static readonly List<Trail> s_trails = new(4);
        static readonly HashSet<Prism> s_members = new();
        static readonly ProfilerMarker s_drainMarker = new("PrismRecycler.Drain");
        static Driver s_driver;
        static int s_trailRestorers;

        /// <summary>Pending entries (diagnostic).</summary>
        public static int PendingCount => s_queue.Count;
        /// <summary>Prisms returned to a pool since load (diagnostic).</summary>
        public static int RecycledCount { get; private set; }
        /// <summary>Registered restorers (Urchins). Non-zero = vessel-trail recycling is on hold (diagnostic).</summary>
        public static int TrailRestorerCount => s_trailRestorers;

        /// <summary>A component that may call <see cref="Prism.Restore"/> on a destroyed vessel-trail prism it rides
        /// (GunVesselTransformer) registers for its lifetime; while any is registered vessel-trail recycling is held
        /// (entries wait, nothing is lost) so a restore never finds its prism re-issued elsewhere.</summary>
        public static void AddTrailRestorer() => s_trailRestorers++;
        public static void RemoveTrailRestorer() => s_trailRestorers = Mathf.Max(0, s_trailRestorers - 1);

        internal static void Enqueue(Prism prism)
        {
            if (!Enabled || !prism) return;
            s_queue.Enqueue((prism, prism.DestroyEpoch, Time.time + DeathEffectSeconds));
            if (s_driver) return;
            var go = new GameObject("[PrismRecycler]") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_driver = go.AddComponent<Driver>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            s_queue.Clear();
            s_trailBatch.Clear();
            s_trails.Clear();
            s_members.Clear();
            s_driver = null;
            s_trailRestorers = 0;
            RecycledCount = 0;
            Enabled = true;
        }

        static bool IsVesselTrailCandidate(Prism prism)
        {
            var trail = prism.Trail;
            return trail != null && trail.RecycleDestroyedMembers && prism.OnReturnToPool != null
                && trail.GetBlockIndex(prism) >= 0;
        }

        static bool IsEnvironmentCandidate(Prism prism) =>
            prism.RecyclesWhenDestroyed && prism.Trail == null && EnvironmentPrismPool.IsIssued(prism)
            && !(prism is HealthPrism hp && (hp.LifeForm || hp.OwnerFauna));

        static void Drain()
        {
            float now = Time.time;
            int budget = Mathf.Max(1, MaxPerFrame);
            int examined = 0;

            while (s_queue.Count > 0 && examined < budget)
            {
                var entry = s_queue.Peek();
                if (entry.dueAt > now) break;   // FIFO with one constant delay: everything behind is later
                s_queue.Dequeue();
                examined++;

                var prism = entry.prism;
                if (!Enabled || !prism || !prism.destroyed || prism.DestroyEpoch != entry.epoch
                    || !prism.gameObject.activeSelf)
                    continue;   // restored, re-issued, re-killed, or its owner already returned/parked it

                try
                {
                    if (IsVesselTrailCandidate(prism))
                    {
                        if (s_trailRestorers > 0)
                        {
                            // Held, not lost: try again after another death-effect window.
                            s_queue.Enqueue((prism, entry.epoch, now + DeathEffectSeconds));
                            continue;
                        }
                        s_trailBatch.Add((prism.Trail, prism));
                    }
                    else if (IsEnvironmentCandidate(prism))
                    {
                        if (EnvironmentPrismPool.TryRelease(prism)) RecycledCount++;
                    }
                    // else: an owner keeps this corpse and owns its exit (see class summary).
                }
                catch (Exception e) { Debug.LogException(e, prism); }   // one bad prism must not stall the queue
            }

            if (s_trailBatch.Count > 0) FlushTrailBatch();
        }

        /// <summary>One compaction per touched trail, then the pool returns. Trail first, so a follower re-indexes
        /// before its prism is reparented and deactivated.</summary>
        static void FlushTrailBatch()
        {
            s_trails.Clear();
            for (int i = 0; i < s_trailBatch.Count; i++)
                if (!s_trails.Contains(s_trailBatch[i].trail)) s_trails.Add(s_trailBatch[i].trail);

            for (int t = 0; t < s_trails.Count; t++)
            {
                var trail = s_trails[t];
                s_members.Clear();
                for (int i = 0; i < s_trailBatch.Count; i++)
                    if (ReferenceEquals(s_trailBatch[i].trail, trail)) s_members.Add(s_trailBatch[i].prism);
                try { trail.RemoveMembers(s_members); }
                catch (Exception e) { Debug.LogException(e); }
            }

            for (int i = 0; i < s_trailBatch.Count; i++)
            {
                var prism = s_trailBatch[i].prism;
                if (!prism) continue;
                try
                {
                    // RemoveMembers leaves prism.Trail as is (ResetState clears it on reuse). The pool's return
                    // delegate unwires itself, so a later owner ReturnToPool is a no-op.
                    prism.ReturnToPool();
                    RecycledCount++;
                }
                catch (Exception e) { Debug.LogException(e, prism); }
            }

            s_trailBatch.Clear();
            s_trails.Clear();
            s_members.Clear();
        }

        sealed class Driver : MonoBehaviour
        {
            void Update()
            {
                if (s_queue.Count == 0) return;
                using (s_drainMarker.Auto()) Drain();
            }

            void OnDestroy()
            {
                if (s_driver == this) s_driver = null;
            }
        }
    }
}
