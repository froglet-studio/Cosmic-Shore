using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The octagon colony's POPULATION-level reproduction book (Docs/ECOSYSTEM.md §32.7,
    /// "organic growth" pass). When a plant COMPLETES its growth it contributes every
    /// unclaimed neighbouring ring centre it can seed - centre, full seed pose, and itself as
    /// the lineage donor - to this per-species book. Reproduction is then a POPULATION event,
    /// not a per-plant one: one cycle per fauna-wave period, one random entry popped, ONE new
    /// lifeform for the whole population. Random choice across every complete plant's frontier
    /// is what de-spheres the growth - the colony wanders organically the way the old
    /// single-plant gyroid wandered prism by prism, now at the level of whole flora.
    ///
    /// <para>This is why the colony no longer needs per-prism-scale occupancy sweeps to
    /// coordinate reproduction: a birth is a rare event (one per ~30s), and its validity check
    /// is a point lookup against the crystal claim book (<see cref="GyroidOctagonRegistry"/>),
    /// not a box overlap. Entries are deduped on add and popped one at a time from the main
    /// thread, so there is no race by construction.</para>
    ///
    /// <para>Bookkeeping only - no MonoBehaviour, no update loop of its own. Living plants
    /// drive the clock from their own grow ticks (<c>AssembledFlora.TickOctagonPopulation</c>),
    /// so the cycle survives any individual death and stops with the population.</para>
    /// </summary>
    public static class GyroidColonyFrontier
    {
        public class Entry
        {
            public Vector3 Center;
            public GyroidGrowthInfo Seed;
            public AssembledFlora Contributor;
        }

        class Book
        {
            public readonly List<Entry> Entries = new();
            public float NextCycleAt;   // 0 = unanchored; the first tick anchors it
        }

        static readonly Dictionary<FloraConfigurationSO, Book> books = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForDomainReload() => books.Clear();

        /// <summary>Open frontier sites across every species' book - for the colony heartbeat.</summary>
        public static int TotalCount
        {
            get
            {
                int n = 0;
                foreach (var b in books.Values) n += b.Entries.Count;
                return n;
            }
        }

        public static int Count(FloraConfigurationSO species) =>
            species && books.TryGetValue(species, out var book) ? book.Entries.Count : 0;

        /// <summary>
        /// Offers one unclaimed neighbouring octagon to the population. Deduped against the
        /// claim book and against entries already offered (multiple plants border the same
        /// octagon, so the same centre arrives from several mature contributors).
        /// </summary>
        public static void Contribute(FloraConfigurationSO species, Vector3 center,
            GyroidGrowthInfo seed, AssembledFlora contributor)
        {
            if (!species || seed == null || !contributor) return;
            if (GyroidOctagonRegistry.IsClaimed(center)) return;

            var book = GetBook(species);
            float dedupeSqr = GyroidOctagonData.CenterDedupeRadius * GyroidOctagonData.CenterDedupeRadius;
            for (int i = 0; i < book.Entries.Count; i++)
                if ((book.Entries[i].Center - center).sqrMagnitude < dedupeSqr)
                    return;

            book.Entries.Add(new Entry { Center = center, Seed = seed, Contributor = contributor });
        }

        /// <summary>
        /// True exactly once per reproduction cycle - the caller that gets true owns THIS
        /// cycle's single birth attempt (plants race the clock from their grow ticks; the
        /// first one past the boundary advances it, so the rest see a future time). Missed
        /// cycles during a long hold (Frenzy, timeScale 0) are skipped, never burst-fired:
        /// the clock advances by whole periods until it leads now.
        /// </summary>
        public static bool TryBeginCycle(FloraConfigurationSO species, float period, float stagger)
        {
            if (!species || period <= 0f) return false;
            var book = GetBook(species);
            if (book.NextCycleAt <= 0f)
            {
                book.NextCycleAt = Time.time + period + stagger;
                return false;
            }
            if (Time.time < book.NextCycleAt) return false;
            while (book.NextCycleAt <= Time.time) book.NextCycleAt += period;
            return true;
        }

        /// <summary>Removes and returns a uniformly random entry (swap-remove). False when empty.</summary>
        public static bool TryPopRandom(FloraConfigurationSO species, out Entry entry)
        {
            entry = null;
            if (!species || !books.TryGetValue(species, out var book) || book.Entries.Count == 0)
                return false;
            int i = Random.Range(0, book.Entries.Count);
            entry = book.Entries[i];
            book.Entries[i] = book.Entries[^1];
            book.Entries.RemoveAt(book.Entries.Count - 1);
            return true;
        }

        static Book GetBook(FloraConfigurationSO species)
        {
            if (!books.TryGetValue(species, out var book))
                books[species] = book = new Book();
            return book;
        }
    }
}
