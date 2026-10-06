using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An owner of VIRTUAL prism entries that are creatures' BODIES (Docs/SWARM_FAUNA.md §19): a population
    /// simulated as data whose members have no GameObject until something needs one. The body is an ordinary
    /// <see cref="PrismSpatialIndex"/> virtual entry, so every prism query, the AOE pass and the cell's volume
    /// sum already see it; this adds the three questions only a CREATURE can answer, so a predator or a
    /// heart-seeking blast can treat the member as the creature it is without knowing what owns it.
    /// </summary>
    public interface IVirtualFaunaOwner : IVirtualPrismOwner
    {
        /// <summary>The predator's own diet, asked of the member's state: diet class, post-birth grace measured
        /// from the member's real age, and the predator's band. Never materialises.</summary>
        bool IsVirtualPrey(int slot, Vector3 at, Fauna predator, bool herbivoresOnly);

        /// <summary>The member's living heart (its creature root) in world space, where it is drawn.</summary>
        bool TryGetVirtualHeart(int slot, out Vector3 heart);

        /// <summary>The furthest any of the owner's hearts is drawn from its stored entry point (the body seat plus
        /// the motion between the stored and the drawn pose). Widens a heart walk so no heart is missed.</summary>
        float HeartReach { get; }

        /// <summary>
        /// The owner's members that already HAVE a GameObject (a proxy) but sit in no cell's fauna registry,
        /// within <paramref name="radius"/> of <paramref name="centre"/>. Their bodies are real prisms and their
        /// virtual entries are suspended, so the index alone would return them as plain prisms.
        /// Appends; never materialises.
        /// </summary>
        void CollectMaterialisedFauna(Vector3 centre, float radius, List<Fauna> results);
    }

    /// <summary>
    /// The predator- and heart-side front door to every <see cref="IVirtualFaunaOwner"/> - owner-agnostic, so
    /// no weapon or predator names a swarm (Docs/SWARM_FAUNA.md §19). Candidates come from the index's own
    /// virtual-entry query (<see cref="PrismSpatialIndex.QuerySphereVirtualIds"/>, the QuerySphere predicate),
    /// the materialisation from <see cref="PrismSpatialIndex.ResolvePrism"/> (which suspends the entry, so a
    /// member is counted once), and the per-frame budget from <see cref="IVirtualPrismBudget"/>.
    /// Main thread only.
    /// </summary>
    public static class VirtualFauna
    {
        static readonly List<IVirtualFaunaOwner> s_owners = new();
        static readonly List<int> s_ids = new(64);
        static readonly List<Fauna> s_materialised = new(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_owners.Clear();

        /// <summary>Called by an owner when its population exists / is gone.</summary>
        public static void Register(IVirtualFaunaOwner owner)
        {
            if (owner != null && !s_owners.Contains(owner)) s_owners.Add(owner);
        }

        public static void Unregister(IVirtualFaunaOwner owner) => s_owners.Remove(owner);

        /// <summary>True while any virtual population exists - lets a predator skip the pass outright.</summary>
        public static bool Any => s_owners.Count > 0;

        /// <summary>The growing nearest-prey sphere doubles from 64 u up to this radius; past it the next step is the
        /// whole remaining reach in ONE query (the index then scans linearly), so a predator with no territory still
        /// finds a member at the far side of the arena without paying a dozen ever-larger walks first.</summary>
        public const float GrowRadius = 2048f;

        /// <summary>An unbounded search (a predator with no territory passes +inf) is clamped to this, beyond any
        /// cell's extent, so the query box stays finite.</summary>
        public const float MaxSearchRadius = 100000f;

        static PrismSpatialIndex Index
        {
            get
            {
                var index = PrismSpatialIndex.Instance;
                return index != null && index.IsAvailable ? index : null;
            }
        }

        static Fauna Materialise(PrismSpatialIndex index, int id, bool force)
        {
            if (!force && !index.HasMaterialiseBudget(id)) return null;
            var prism = index.ResolvePrism(id, materialise: true) as HealthPrism;
            return prism ? prism.ResolveOwnerFauna() : null;
        }

        static bool IsPreyFauna(Fauna f, Fauna predator, bool herbivoresOnly)
        {
            if (!f || f == predator || !f.IsAlivePrey || f.IsPredationImmune) return false;
            return !herbivoresOnly || f.Diet == FaunaDiet.Herbivore;
        }

        /// <summary>
        /// The nearest virtual-population creature <paramref name="predator"/> can eat within
        /// <paramref name="maxSqr"/> of <paramref name="origin"/>, as a REAL creature: one that already has a
        /// GameObject is returned as it is; one that is only data is materialised (budgeted). The search grows a
        /// sphere from 64 u, so it touches only the entries near the answer. Null when nothing qualifies or the
        /// frame's budget is spent (the member is still there next behaviour tick).
        /// </summary>
        public static Fauna NearestPrey(Vector3 origin, float maxSqr, Fauna predator, bool herbivoresOnly)
        {
            if (!Any || !predator) return null;
            var index = Index;
            float best = maxSqr;
            Fauna bestFauna = null;
            float rMax = Mathf.Min(Mathf.Sqrt(maxSqr), MaxSearchRadius);
            if (!(rMax > 0f)) return null;

            // members that already have a GameObject
            s_materialised.Clear();
            for (int k = 0; k < s_owners.Count; k++) s_owners[k].CollectMaterialisedFauna(origin, rMax, s_materialised);
            for (int q = 0; q < s_materialised.Count; q++)
            {
                var f = s_materialised[q];
                if (!IsPreyFauna(f, predator, herbivoresOnly)) continue;
                var at = f.transform.position;
                float d = (at - origin).sqrMagnitude;
                if (d <= best && predator.IsInsideBand(at)) { best = d; bestFauna = f; }
            }
            if (index == null) return bestFauna;

            // members that are only data: the index's own virtual entries, nearest first by a growing sphere
            int bestId = -1;
            for (float r = 64f; ; r = r >= GrowRadius ? rMax : r * 2f)
            {
                float rr = Mathf.Min(r, Mathf.Min(rMax, Mathf.Sqrt(best)));
                index.QuerySphereVirtualIds(origin, rr, s_ids);
                bool found = false;
                for (int q = 0; q < s_ids.Count; q++)
                {
                    int id = s_ids[q];
                    if (!index.TryGetVirtual(id, out var owner, out int slot) || owner is not IVirtualFaunaOwner fo) continue;
                    if (!index.TryGetVirtualEntry(id, out var at, out _, out _)) continue;
                    float d = (at - origin).sqrMagnitude;
                    if (d > best || !fo.IsVirtualPrey(slot, at, predator, herbivoresOnly)) continue;
                    best = d; bestId = id; found = true;
                }
                if (found || rr >= rMax || rr >= Mathf.Sqrt(best)) break;
            }
            if (bestId < 0) return bestFauna;
            var member = Materialise(index, bestId, force: false);
            return member && !member.IsPredationImmune ? member : bestFauna;
        }

        /// <summary>
        /// Every virtual-population creature within <paramref name="range"/> of a predator's MOUTH that it can eat,
        /// as REAL creatures (existing ones, plus members materialised under the per-frame budget), appended to
        /// <paramref name="results"/>. The predator then calls <see cref="Fauna.Predated"/> on each exactly as on a
        /// registry creature, so the suction, the mass transfer and the crystal are the platform's.
        /// </summary>
        public static void PreyInReach(Vector3 mouth, float range, Fauna predator, bool herbivoresOnly, List<Fauna> results)
        {
            if (!Any || !predator || !(range > 0f)) return;
            float r2 = range * range;
            s_materialised.Clear();
            for (int k = 0; k < s_owners.Count; k++) s_owners[k].CollectMaterialisedFauna(mouth, range, s_materialised);
            for (int q = 0; q < s_materialised.Count; q++)
            {
                var f = s_materialised[q];
                if (!f || f == predator || !f.IsAlivePrey) continue;
                if (herbivoresOnly && f.Diet != FaunaDiet.Herbivore) continue;
                if ((f.transform.position - mouth).sqrMagnitude <= r2) results.Add(f);
            }

            var index = Index;
            if (index == null) return;
            index.QuerySphereVirtualIds(mouth, range, s_ids);
            for (int q = 0; q < s_ids.Count; q++)
            {
                int id = s_ids[q];
                if (!index.TryGetVirtual(id, out var owner, out int slot) || owner is not IVirtualFaunaOwner fo) continue;
                if (!index.TryGetVirtualEntry(id, out var at, out _, out _)) continue;
                if (!fo.IsVirtualPrey(slot, at, predator, herbivoresOnly)) continue;
                if (!index.HasMaterialiseBudget(id)) break;   // the frame's budget is spent - the rest are there next frame
                var f = Materialise(index, id, force: true);
                if (f) results.Add(f);
            }
        }

        /// <summary>
        /// The living hearts of virtual-population creatures within <paramref name="radius"/> of
        /// <paramref name="centre"/>, as (index id, heart position) pairs - appended, never materialised. The
        /// index stores a member's BODY centre, so the walk is widened by the owners' <see cref="IVirtualFaunaOwner.HeartReach"/>
        /// (how far a heart may sit from its stored point) and each heart is then tested where it is drawn. What a blast's
        /// lifeform-crystal effects meet when the member has no crystal collider to overlap yet; the caller
        /// applies its own narrowphase, then <see cref="MaterialiseHeart"/>.
        /// </summary>
        public static void CollectHearts(Vector3 centre, float radius, List<int> ids, List<Vector3> hearts)
        {
            if (!Any || !(radius > 0f)) return;
            var index = Index;
            if (index == null) return;
            float r2 = radius * radius;
            float bodyReach = 0f;
            for (int k = 0; k < s_owners.Count; k++) bodyReach = Mathf.Max(bodyReach, s_owners[k].HeartReach);
            index.QuerySphereVirtualIds(centre, radius + bodyReach, s_ids);
            for (int q = 0; q < s_ids.Count; q++)
            {
                int id = s_ids[q];
                if (!index.TryGetVirtual(id, out var owner, out int slot) || owner is not IVirtualFaunaOwner fo) continue;
                if (!fo.TryGetVirtualHeart(slot, out var heart)) continue;
                if ((heart - centre).sqrMagnitude > r2) continue;
                ids.Add(id);
                hearts.Add(heart);
            }
        }

        /// <summary>
        /// Materialises the member behind index id <paramref name="id"/> and returns its living heart. Null with
        /// <paramref name="retry"/> true when the owner's per-frame budget is spent (keep the id, ask next frame);
        /// null with <paramref name="retry"/> false when the member is gone or already has a GameObject (its real
        /// crystal is then an ordinary collider the caller's overlap finds).
        /// </summary>
        public static Crystal MaterialiseHeart(int id, bool force, out bool retry)
        {
            retry = false;
            var index = Index;
            if (index == null || !index.IsLiveVirtual(id)) return null;
            if (!force && !index.HasMaterialiseBudget(id)) { retry = true; return null; }
            var f = Materialise(index, id, force: true);
            return f ? f.LivingHeart : null;
        }
    }
}
