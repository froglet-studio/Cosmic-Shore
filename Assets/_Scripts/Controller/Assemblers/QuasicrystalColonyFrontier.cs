using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The quasicrystal colony's POPULATION-level reproduction book - the heart analogue of
    /// <see cref="SchwarzPColonyFrontier"/> (Docs/ECOSYSTEM.md 32.7, 34.6, 36).
    ///
    /// <para>When a plant COMPLETES its star it walks the measured heart-link census
    /// (<c>QuasicrystalLatticeData.FrontierNeighbour</c>) and contributes every unclaimed
    /// shell heart - and itself as the lineage donor - to this per-species book.
    /// Reproduction is then a POPULATION event: one cycle per fauna-wave period, one random
    /// entry popped, ONE new lifeform for the whole population. Random choice across every
    /// complete plant's frontier is what de-spheres the growth - the colony wanders
    /// organically instead of inflating as a ball.</para>
    ///
    /// <para><b>An entry is just (frame, heart).</b> A heart is an exact Z^6 address: the
    /// daughter's seed prism is derived at birth from the shared frame's closed-form
    /// arithmetic (<see cref="QuasicrystalAssembler.BuildSeed"/>) rather than carried, so
    /// the transcribed-pose error class the gyroid asserts against at every birth cannot
    /// arise. Deduping is exact for the same reason: a HashSet of (frame, heart), never a
    /// radius scan - several complete plants genuinely border the same open heart.</para>
    ///
    /// <para>Bookkeeping only - no MonoBehaviour, no update loop of its own. Living plants
    /// drive the clock from their own grow ticks (<c>AssembledFlora.TickStarPopulation</c>),
    /// so the cycle survives any individual death and stops with the population.</para>
    /// </summary>
    public static class QuasicrystalColonyFrontier
    {
        public class Entry
        {
            public QuasicrystalLatticeFrame Frame;
            public QuasicrystalVertex Heart;
            public AssembledFlora Contributor;
        }

        class Book
        {
            public readonly List<Entry> Entries = new();
            public readonly HashSet<(QuasicrystalLatticeFrame, QuasicrystalVertex)> Offered = new();
            public float NextCycleAt;   // 0 = unanchored; the first tick anchors it
        }

        /// <summary>
        /// Keyed by CELL as well as species, for two independent reasons: the reproduction
        /// clock is the CELL's fauna-wave period (two cells with different profiles would
        /// otherwise fight over one clock), and a cell that tears its life down
        /// (<c>Cell.ResetCell</c>, a Cell-Selector world swap) must be able to drop ITS
        /// hearts without silencing another cell's colony of the same species - which,
        /// since contribution is one-shot per plant, would be permanent.
        /// </summary>
        static readonly Dictionary<(Cell, FloraConfigurationSO), Book> books = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForDomainReload() => books.Clear();

        /// <summary>Open frontier hearts across every book - for the colony heartbeat.</summary>
        public static int TotalCount
        {
            get
            {
                int n = 0;
                foreach (var b in books.Values) n += b.Entries.Count;
                return n;
            }
        }

        public static int Count(Cell cell, FloraConfigurationSO species) =>
            cell && species && books.TryGetValue((cell, species), out var book) ? book.Entries.Count : 0;

        /// <summary>Drops every frontier heart belonging to <paramref name="cell"/>.</summary>
        public static void Clear(Cell cell)
        {
            if (!cell) return;
            var doomed = new List<(Cell, FloraConfigurationSO)>();
            foreach (var key in books.Keys)
                if (key.Item1 == cell) doomed.Add(key);
            for (int i = 0; i < doomed.Count; i++) books.Remove(doomed[i]);
        }

        /// <summary>
        /// Offers one unclaimed shell heart to the population. Deduped against the claim
        /// book and against hearts already offered.
        /// </summary>
        public static void Contribute(Cell cell, FloraConfigurationSO species,
            QuasicrystalLatticeFrame frame, QuasicrystalVertex heart, AssembledFlora contributor)
        {
            if (!cell || !species || frame == null || !contributor) return;
            if (QuasicrystalHeartRegistry.IsClaimed(frame, heart)) return;

            var book = GetBook(cell, species);
            if (!book.Offered.Add((frame, heart))) return;
            book.Entries.Add(new Entry { Frame = frame, Heart = heart, Contributor = contributor });
        }

        /// <summary>
        /// True exactly once per reproduction cycle - the caller that gets true owns THIS
        /// cycle's single birth attempt (plants race the clock from their grow ticks; the
        /// first one past the boundary advances it, so the rest see a future time). Missed
        /// cycles during a long hold (Frenzy, timeScale 0) are skipped, never burst-fired:
        /// the clock advances by whole periods until it leads now.
        /// </summary>
        public static bool TryBeginCycle(Cell cell, FloraConfigurationSO species, float period, float stagger)
        {
            if (!cell || !species || period <= 0f) return false;
            var book = GetBook(cell, species);
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
        public static bool TryPopRandom(Cell cell, FloraConfigurationSO species, out Entry entry)
        {
            entry = null;
            if (!cell || !species || !books.TryGetValue((cell, species), out var book) ||
                book.Entries.Count == 0)
                return false;
            int i = Random.Range(0, book.Entries.Count);
            entry = book.Entries[i];
            book.Entries[i] = book.Entries[^1];
            book.Entries.RemoveAt(book.Entries.Count - 1);
            book.Offered.Remove((entry.Frame, entry.Heart));
            return true;
        }

        static Book GetBook(Cell cell, FloraConfigurationSO species)
        {
            if (!books.TryGetValue((cell, species), out var book))
                books[(cell, species)] = book = new Book();
            return book;
        }
    }
}
