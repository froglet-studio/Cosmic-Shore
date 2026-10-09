// The evidence (Docs/EVOLUTION.md §6; Docs/ECOSYSTEM_MASTERPLAN.md §3 item 4): lineage tracking plus the trait
// distribution over time, per species, per cell. Pure C#: the harness fills one headlessly and the Evolution Monitor
// window draws the one a live Cell keeps. The same JSON (ToJson) feeds both the lab and the doc's tables.
//
// It records DECISIONS the economy already made - a birth, a death and its cause - and never influences one. A ledger
// that steered selection would be a fitness function with extra steps.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmicShore.Gameplay
{
    /// <summary>Why an individual left the living set. Static values: a column id in every export.</summary>
    public enum LifeformDeathCause
    {
        /// <summary>Metabolism burned the stomach to zero.</summary>
        Starvation = 0,
        /// <summary>A predator ate it (the only death that moves its mass into another creature).</summary>
        Predation = 1,
        /// <summary>A vessel destroyed its last body prism, or an ability withered it.</summary>
        Vessel = 2,
        /// <summary>A joust freed its heart (Docs/ECOSYSTEM.md §26.10).</summary>
        Joust = 3,
        /// <summary>Scene teardown, a cell swap, a mode pulling the husk: not a death the economy chose, not selection.</summary>
        Teardown = 4,
        /// <summary>Anything else that reached Die.</summary>
        Other = 5,
    }

    /// <summary>One census row: the living population's trait distribution for one species at one moment.</summary>
    public struct TraitSnapshot
    {
        public double Time;
        public int Population;
        /// <summary>Per locus (GenomeLocus order).</summary>
        public float[] Mean, StdDev, Min, Max;
        public float MeanGeneration;
        public int MaxGeneration;
        /// <summary>Distinct founder lineages still represented among the living.</summary>
        public int Lineages;
        /// <summary>Events since the previous row of this species.</summary>
        public int Births;
        public int[] DeathsByCause;
    }

    /// <summary>
    /// Per-cell, per-species evolution bookkeeping: who was born to whom with which genome, who died of what, and a
    /// census on a cadence. Bounded: it keeps the LIVING individuals' records (population-sized), cumulative counters,
    /// and at most <see cref="MaxSnapshotsPerSpecies"/> census rows per species.
    /// </summary>
    public sealed class EvolutionLedger
    {
        public const int CauseCount = 6;

        /// <summary>One living (or just-recorded) individual.</summary>
        public struct Individual
        {
            public uint Id, ParentId, LineageId;
            public int Generation;
            public string Species;
            public LifeformGenome Genome;
            public double BornAt;
        }

        sealed class SpeciesBook
        {
            public readonly string Species;
            public readonly List<TraitSnapshot> Snapshots = new List<TraitSnapshot>();
            public int BirthsSinceSnapshot, FoundersSinceSnapshot;
            public readonly int[] DeathsSinceSnapshot = new int[CauseCount];
            public long TotalBirths, TotalFounders;
            public readonly long[] TotalDeaths = new long[CauseCount];
            public int MaxGenerationEver;
            public SpeciesBook(string species) => Species = species;
        }

        readonly Dictionary<uint, Individual> _living = new Dictionary<uint, Individual>();
        readonly Dictionary<string, SpeciesBook> _books = new Dictionary<string, SpeciesBook>(StringComparer.Ordinal);
        readonly List<string> _speciesOrder = new List<string>();
        uint _nextId = 1;

        /// <summary>Census rows kept per species before the oldest is dropped.</summary>
        public int MaxSnapshotsPerSpecies = 2048;

        /// <summary>Number of living individuals across every species.</summary>
        public int LivingCount => _living.Count;

        /// <summary>Species in the order they were first seen.</summary>
        public IReadOnlyList<string> Species => _speciesOrder;

        /// <summary>Forgets everything (a cell reset / swap / teardown: the next world starts its own book).</summary>
        public void Clear()
        {
            _living.Clear();
            _books.Clear();
            _speciesOrder.Clear();
            _nextId = 1;
        }

        SpeciesBook Book(string species)
        {
            if (!_books.TryGetValue(species, out var book))
            {
                book = new SpeciesBook(species);
                _books.Add(species, book);
                _speciesOrder.Add(species);
            }
            return book;
        }

        /// <summary>A seeder-spawned individual: it founds its own lineage at generation 0. Returns its id.</summary>
        public uint RecordFounder(string species, in LifeformGenome genome, double time)
        {
            species = species ?? "";
            uint id = _nextId++;
            _living[id] = new Individual
            {
                Id = id, ParentId = 0, LineageId = id, Generation = 0,
                Species = species, Genome = genome, BornAt = time,
            };
            var book = Book(species);
            book.FoundersSinceSnapshot++;
            book.TotalFounders++;
            return id;
        }

        /// <summary>
        /// A birth: the child joins its parent's lineage one generation on. A parent the ledger does not know
        /// (recorded before a Clear, or never recorded) makes the child a founder instead, so the row is never lost.
        /// </summary>
        public uint RecordBirth(string species, uint parentId, in LifeformGenome genome, double time)
        {
            species = species ?? "";
            if (!_living.TryGetValue(parentId, out var parent))
                return RecordFounder(species, genome, time);

            uint id = _nextId++;
            int generation = parent.Generation + 1;
            _living[id] = new Individual
            {
                Id = id, ParentId = parentId, LineageId = parent.LineageId, Generation = generation,
                Species = species, Genome = genome, BornAt = time,
            };
            var book = Book(species);
            book.BirthsSinceSnapshot++;
            book.TotalBirths++;
            if (generation > book.MaxGenerationEver) book.MaxGenerationEver = generation;
            return id;
        }

        /// <summary>An individual leaves the living set. Unknown ids are ignored (a creature recorded before a Clear).</summary>
        public bool RecordDeath(uint id, LifeformDeathCause cause, double time)
        {
            if (!_living.TryGetValue(id, out var ind)) return false;
            _living.Remove(id);
            var book = Book(ind.Species);
            int c = (int)cause;
            if (c < 0 || c >= CauseCount) c = (int)LifeformDeathCause.Other;
            book.DeathsSinceSnapshot[c]++;
            book.TotalDeaths[c]++;
            return true;
        }

        public bool TryGetLiving(uint id, out Individual individual) => _living.TryGetValue(id, out individual);

        /// <summary>The living individuals of one species, appended to <paramref name="into"/>.</summary>
        public void CollectLiving(string species, List<Individual> into)
        {
            foreach (var kv in _living)
                if (string.Equals(kv.Value.Species, species, StringComparison.Ordinal)) into.Add(kv.Value);
        }

        /// <summary>Living individuals of one species.</summary>
        public int LivingOf(string species)
        {
            int n = 0;
            foreach (var kv in _living)
                if (string.Equals(kv.Value.Species, species, StringComparison.Ordinal)) n++;
            return n;
        }

        /// <summary>The census rows of a species (oldest first), or an empty list.</summary>
        public IReadOnlyList<TraitSnapshot> SnapshotsOf(string species) =>
            _books.TryGetValue(species ?? "", out var book) ? book.Snapshots : (IReadOnlyList<TraitSnapshot>)Array.Empty<TraitSnapshot>();

        public long TotalBirthsOf(string species) => _books.TryGetValue(species ?? "", out var b) ? b.TotalBirths : 0;
        public long TotalFoundersOf(string species) => _books.TryGetValue(species ?? "", out var b) ? b.TotalFounders : 0;
        public long TotalDeathsOf(string species, LifeformDeathCause cause) =>
            _books.TryGetValue(species ?? "", out var b) ? b.TotalDeaths[(int)cause] : 0;
        public int MaxGenerationOf(string species) => _books.TryGetValue(species ?? "", out var b) ? b.MaxGenerationEver : 0;

        /// <summary>
        /// Takes one census row for EVERY species seen (a species with no living members still gets a row with
        /// population 0, so an extinction is visible as a row and not as a gap). Resets the since-snapshot counters.
        /// </summary>
        public void Snapshot(double time)
        {
            for (int i = 0; i < _speciesOrder.Count; i++) SnapshotSpecies(_speciesOrder[i], time);
        }

        /// <summary>One census row for one species.</summary>
        public TraitSnapshot SnapshotSpecies(string species, double time)
        {
            var book = Book(species ?? "");
            int L = LifeformGenome.LocusCount;
            var row = new TraitSnapshot
            {
                Time = time,
                Mean = new float[L], StdDev = new float[L], Min = new float[L], Max = new float[L],
                DeathsByCause = new int[CauseCount],
            };

            // Welford over the living of this species, per locus; exact min/max; lineage count by hash set.
            int n = 0;
            var mean = new double[L];
            var m2 = new double[L];
            var min = new double[L];
            var max = new double[L];
            for (int l = 0; l < L; l++) { min[l] = double.PositiveInfinity; max[l] = double.NegativeInfinity; }
            double genSum = 0;
            int genMax = 0;
            var lineages = new HashSet<uint>();

            foreach (var kv in _living)
            {
                var ind = kv.Value;
                if (!string.Equals(ind.Species, book.Species, StringComparison.Ordinal)) continue;
                n++;
                for (int l = 0; l < L; l++)
                {
                    double x = ind.Genome[l];
                    double delta = x - mean[l];
                    mean[l] += delta / n;
                    m2[l] += delta * (x - mean[l]);
                    if (x < min[l]) min[l] = x;
                    if (x > max[l]) max[l] = x;
                }
                genSum += ind.Generation;
                if (ind.Generation > genMax) genMax = ind.Generation;
                lineages.Add(ind.LineageId);
            }

            row.Population = n;
            for (int l = 0; l < L; l++)
            {
                row.Mean[l] = n > 0 ? (float)mean[l] : 0f;
                row.StdDev[l] = n > 1 ? (float)Math.Sqrt(m2[l] / (n - 1)) : 0f;
                row.Min[l] = n > 0 ? (float)min[l] : 0f;
                row.Max[l] = n > 0 ? (float)max[l] : 0f;
            }
            row.MeanGeneration = n > 0 ? (float)(genSum / n) : 0f;
            row.MaxGeneration = genMax;
            row.Lineages = lineages.Count;
            row.Births = book.BirthsSinceSnapshot;
            Array.Copy(book.DeathsSinceSnapshot, row.DeathsByCause, CauseCount);

            book.BirthsSinceSnapshot = 0;
            book.FoundersSinceSnapshot = 0;
            Array.Clear(book.DeathsSinceSnapshot, 0, CauseCount);

            book.Snapshots.Add(row);
            int cap = Math.Max(16, MaxSnapshotsPerSpecies);
            if (book.Snapshots.Count > cap) book.Snapshots.RemoveRange(0, book.Snapshots.Count - cap);
            return row;
        }

        // ------------------------------------------------------------------------------------------------------------
        //  JSON - hand-rolled so the core carries no serializer dependency. Invariant culture throughout.
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The whole ledger as JSON: per species its totals and every census row, plus the locus labels and cause names
        /// so a reader never has to guess a column. Stable key order; the lab and the doc tables parse this.
        /// </summary>
        public string ToJson(string label = null)
        {
            var sb = new StringBuilder(4096);
            sb.Append('{');
            if (!string.IsNullOrEmpty(label)) { sb.Append("\"label\":"); Str(sb, label); sb.Append(','); }
            sb.Append("\"loci\":[");
            for (int l = 0; l < LifeformGenome.LocusCount; l++)
            {
                if (l > 0) sb.Append(',');
                Str(sb, LifeformGenome.LocusLabel((GenomeLocus)l));
            }
            sb.Append("],\"causes\":[");
            for (int c = 0; c < CauseCount; c++)
            {
                if (c > 0) sb.Append(',');
                Str(sb, ((LifeformDeathCause)c).ToString());
            }
            sb.Append("],\"living\":").Append(_living.Count.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"species\":[");
            for (int i = 0; i < _speciesOrder.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteSpecies(sb, _books[_speciesOrder[i]]);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        void WriteSpecies(StringBuilder sb, SpeciesBook book)
        {
            sb.Append("{\"name\":"); Str(sb, book.Species);
            sb.Append(",\"living\":").Append(LivingOf(book.Species).ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"founders\":").Append(book.TotalFounders.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"births\":").Append(book.TotalBirths.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"maxGeneration\":").Append(book.MaxGenerationEver.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"deaths\":[");
            for (int c = 0; c < CauseCount; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append(book.TotalDeaths[c].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append("],\"rows\":[");
            for (int i = 0; i < book.Snapshots.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteRow(sb, book.Snapshots[i]);
            }
            sb.Append("]}");
        }

        static void WriteRow(StringBuilder sb, in TraitSnapshot r)
        {
            sb.Append("{\"t\":"); Num(sb, r.Time);
            sb.Append(",\"n\":").Append(r.Population.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"mean\":"); Arr(sb, r.Mean);
            sb.Append(",\"sd\":"); Arr(sb, r.StdDev);
            sb.Append(",\"min\":"); Arr(sb, r.Min);
            sb.Append(",\"max\":"); Arr(sb, r.Max);
            sb.Append(",\"gen\":"); Num(sb, r.MeanGeneration);
            sb.Append(",\"genMax\":").Append(r.MaxGeneration.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"lineages\":").Append(r.Lineages.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"births\":").Append(r.Births.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"deaths\":[");
            for (int c = 0; c < r.DeathsByCause.Length; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append(r.DeathsByCause[c].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append("]}");
        }

        static void Arr(StringBuilder sb, float[] a)
        {
            sb.Append('[');
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) sb.Append(',');
                Num(sb, a[i]);
            }
            sb.Append(']');
        }

        static void Num(StringBuilder sb, double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) { sb.Append("null"); return; }
            sb.Append(v.ToString("R", CultureInfo.InvariantCulture));
        }

        static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
