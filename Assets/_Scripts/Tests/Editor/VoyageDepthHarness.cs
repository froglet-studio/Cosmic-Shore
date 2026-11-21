#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The voyage depth harness (Docs/Voyages/DEPTH.md §11): plays thousands of voyages headless on the pure rules and
    /// measures what a player would feel. Random crews of three pilots re-seated every few voyages, a policy that takes
    /// the best-paying job six times in ten and any job otherwise (and chases a saga chapter when one is offered), the
    /// work rolled against the course's intensity and the rivals met, the galaxy ticking between voyages. The scorecard
    /// is also a test (<see cref="VoyageDepthTests"/>), so a content change that flattens the game fails before it ships.
    /// Runs in an edit-mode test and in <c>Tools/Build/voyage_depth_harness</c> (no engine type).
    /// </summary>
    public static class VoyageDepthHarness
    {
        /// <summary>The content as the C# classes read it, written by <c>author_voyage_assets.py</c> beside the spec.</summary>
        public const string ContentPath = "Docs/Voyages/voyage_content.json";

        /// <summary>The repository root: the nearest directory above the working directory (or the harness binary) that
        /// holds the content twin.</summary>
        public static string RepoRoot()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, ContentPath))) return dir.FullName;
                    dir = dir.Parent;
                }
            }
            throw new FileNotFoundException("No repository root above the working directory holds " + ContentPath);
        }

        public static VoyageContent LoadShippedContent() => LoadContent(Path.Combine(RepoRoot(), ContentPath));

        public static VoyageContent LoadContent(string path)
        {
            var content = JsonUtility.FromJson<VoyageContent>(File.ReadAllText(path));
            if (content == null) throw new InvalidDataException("The voyage content at " + path + " would not read.");
            return content;
        }

        // ── The scorecard ───────────────────────────────────────────────────

        /// <summary>What the harness measured, and the bars it is held to (DEPTH.md §11).</summary>
        public sealed class Scorecard
        {
            public int Galaxies, VoyagesPerGalaxy;
            public int Boards, DeadBoards, StuckAtHome;
            public int Jobs, NewsGrown, ChapterJobs, Successes, Failures_, ArksLost, Extractions, Extracted, Relics, RelicDuplicates;
            public int Meetings, HostileMeetings, CoOpMeetings, Races, KaijuMet, KaijuFelled, Charted;
            public int Days, RivalDeeds, RivalWrecks;
            public int MaxNewsAtPort;
            public int TypesWithWorkDay0, TypesEverOffered;
            public int GalaxiesWithRelic;
            public int SwapChecks, SwapChanged;
            public int MaxStateJson;
            public double DistinctPairsSum;
            public double PaySum;
            public int Unreachable, Unchartable;
            public readonly List<string> TextProblems = new();
            public readonly Dictionary<WorkVerb, int> Verbs = new();
            public readonly Dictionary<JobKind, int> Kinds = new();
            public readonly Dictionary<string, int> Destinations = new();
            public readonly Dictionary<int, int> Intensities = new();
            public readonly Dictionary<int, int> TiersReached = new();

            public double DeadBoardShare => Boards == 0 ? 0 : (double)DeadBoards / Boards;
            public double DistinctPairsMean => Galaxies == 0 ? 0 : DistinctPairsSum / Galaxies;
            public double NewsGrownShare => Jobs == 0 ? 0 : (double)NewsGrown / Jobs;
            public double RivalEventsPer10Days => Days == 0 ? 0 : 10.0 * (RivalDeeds + RivalWrecks) / Days;
            public double SagaFinishShare => Galaxies == 0 ? 0 : (double)GalaxiesWithRelic / Galaxies;
            public double MeanPay => Jobs == 0 ? 0 : PaySum / Jobs;
            public double ArkLossShare => Jobs == 0 ? 0 : (double)ArksLost / Jobs;
            public double ExtractedPerExtraction => Extractions == 0 ? 0 : (double)Extracted / Extractions;
            public double SwapChangedShare => SwapChecks == 0 ? 0 : (double)SwapChanged / SwapChecks;

            /// <summary>The bars that did not hold, one line each; empty when the content is deep enough.</summary>
            public List<string> Failures()
            {
                var f = new List<string>();
                if (DeadBoardShare > 0.03) f.Add($"dead boards {DeadBoardShare:P1} (bar: under 3%)");
                if (DistinctPairsMean < 12) f.Add($"distinct (job, destination) pairs in the first 20 voyages {DistinctPairsMean:F1} (bar: at least 12)");
                if (NewsGrownShare < 0.45) f.Add($"news-grown share {NewsGrownShare:P0} (bar: at least 45%)");
                if (RivalEventsPer10Days < 4) f.Add($"rival events per 10 days {RivalEventsPer10Days:F1} (bar: at least 4)");
                if (SagaFinishShare < 0.70) f.Add($"galaxies with a relic found {SagaFinishShare:P0} (bar: at least 70%)");
                if (RelicDuplicates > 0) f.Add($"relics granted twice: {RelicDuplicates} (bar: 0)");
                if (MaxNewsAtPort > 24) f.Add($"news items alive at a port {MaxNewsAtPort} (bar: at most 24)");
                if (TypesWithWorkDay0 < 16) f.Add($"faction types with work at a harbour on day 0: {TypesWithWorkDay0} (bar: 16)");
                if (TypesEverOffered < 16) f.Add($"faction types ever offered work: {TypesEverOffered} (bar: 16)");
                if (Unreachable > 0) f.Add($"unreachable places: {Unreachable} (bar: 0)");
                if (Unchartable > 0) f.Add($"uncharted places nobody could find: {Unchartable} (bar: 0)");
                if (TextProblems.Count > 0) f.Add($"text problems: {TextProblems.Count} (bar: 0): {TextProblems[0]}");
                if (SwapChangedShare < 0.5) f.Add($"boards changed by swapping one seat {SwapChangedShare:P0} (bar: at least 50%)");
                if (MaxStateJson > 120_000) f.Add($"the galaxy's record as JSON {MaxStateJson} chars (bar: at most 120,000, the course message a friend receives)");
                return f;
            }

            public string Report()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Voyage depth scorecard: {Galaxies} galaxies x {VoyagesPerGalaxy} voyages ({Jobs} jobs, {Days} days)");
                sb.AppendLine($"  dead boards              {DeadBoardShare:P1}  ({DeadBoards} of {Boards}; stuck at home {StuckAtHome})");
                sb.AppendLine($"  distinct pairs / 20      {DistinctPairsMean:F1}");
                sb.AppendLine($"  news-grown share         {NewsGrownShare:P0}  (chapters {ChapterJobs})");
                sb.AppendLine($"  rival events / 10 days   {RivalEventsPer10Days:F1}  (deeds {RivalDeeds}, wrecks {RivalWrecks})");
                sb.AppendLine($"  galaxies with a relic    {SagaFinishShare:P0}  (relics {Relics}, duplicates {RelicDuplicates})");
                sb.AppendLine($"  max news at a port       {MaxNewsAtPort}");
                sb.AppendLine($"  types with work day 0    {TypesWithWorkDay0} of 16; ever offered {TypesEverOffered} of 16");
                sb.AppendLine($"  reach                    unreachable {Unreachable}, unchartable {Unchartable}, text problems {TextProblems.Count}");
                sb.AppendLine($"  outcomes                 done {Successes}, undone {Failures_}, Arks lost {ArksLost} ({ArkLossShare:P1}), extractions {Extractions} ({ExtractedPerExtraction:F2} finds each)");
                sb.AppendLine($"  meetings                 {Meetings} (co-op {CoOpMeetings}, races {Races}, hostile {HostileMeetings}); Kaiju met {KaijuMet}, felled {KaijuFelled}; charted {Charted}");
                sb.AppendLine($"  mean pay                 {MeanPay:F1}");
                sb.AppendLine($"  seat swap changes board  {SwapChangedShare:P0}  ({SwapChanged} of {SwapChecks}); record as JSON at most {MaxStateJson} chars");
                sb.Append("  verbs                   ");
                foreach (var kv in Verbs) sb.Append($" {kv.Key}:{kv.Value}");
                sb.AppendLine();
                sb.Append("  kinds                   ");
                foreach (var kv in Kinds) sb.Append($" {kv.Key}:{kv.Value}");
                sb.AppendLine();
                sb.Append("  intensities             ");
                for (int i = 1; i <= 4; i++) sb.Append($" I{i}:{(Intensities.TryGetValue(i, out int n) ? n : 0)}");
                sb.AppendLine();
                sb.Append("  destinations            ");
                var dests = new List<KeyValuePair<string, int>>(Destinations);
                dests.Sort((a, b) => b.Value.CompareTo(a.Value));
                foreach (var kv in dests) sb.Append($" {kv.Key.Replace("loc.", string.Empty)}:{kv.Value}");
                sb.AppendLine();
                var fails = Failures();
                sb.AppendLine(fails.Count == 0 ? "  BARS: all hold" : "  BARS FAILED: " + string.Join("; ", fails));
                return sb.ToString();
            }
        }

        // ── Playing ─────────────────────────────────────────────────────────

        /// <summary>A stable generator (xorshift64*), the same on every runtime.</summary>
        public sealed class Dice
        {
            ulong _s;
            public Dice(ulong seed) { _s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
            public ulong NextRaw() { _s ^= _s >> 12; _s ^= _s << 25; _s ^= _s >> 27; return _s * 2685821657736338717UL; }
            public double NextDouble() => (NextRaw() >> 11) * (1.0 / 9007199254740992.0);
            public int Next(int n) => n <= 0 ? 0 : (int)(NextRaw() % (ulong)n);
        }

        static readonly Domains[] Homes = { Domains.Jade, Domains.Ruby, Domains.Gold };

        public static Scorecard Play(VoyageContent content, int voyagesPerGalaxy, int galaxies, ulong seed = 7)
        {
            var card = new Scorecard { Galaxies = galaxies, VoyagesPerGalaxy = voyagesPerGalaxy };
            MeasureContent(content, card);
            var crew = new List<string>();
            foreach (var p in content.Crew) if (p != null) crew.Add(p.PilotId);
            var everOffered = new HashSet<FactionType>();

            for (int g = 0; g < galaxies; g++)
            {
                var dice = new Dice(seed * 1000003UL + (ulong)g * 7919UL + 1);
                var home = Homes[g % Homes.Length];
                var state = VoyageRules.NewGalaxy(content, home);
                var seated = Seat(crew, dice);
                var pairs = new HashSet<string>();
                var relicsSeen = new HashSet<string>();
                int nonReturn = 0, stuck = 0;
                bool relic = false;

                for (int v = 0; v < voyagesPerGalaxy; v++)
                {
                    if (v > 0 && v % 4 == 0) seated = Seat(crew, dice);
                    var board = VoyageRules.Board(state, content, seated, crew);
                    card.Boards++;
                    // Does the pilot card change the voyage? Swap one seat and compare the cards.
                    if (v % 5 == 0 && crew.Count > 3)
                    {
                        var swapped = new List<string>(seated);
                        string other = crew[dice.Next(crew.Count)];
                        if (!swapped.Contains(other))
                        {
                            swapped[0] = other;
                            var alt = VoyageRules.Board(state, content, swapped, crew);
                            var a = new HashSet<string>();
                            foreach (var j in board) a.Add(j.TemplateId + "@" + j.To);
                            var b = new HashSet<string>();
                            foreach (var j in alt) b.Add(j.TemplateId + "@" + j.To);
                            card.SwapChecks++;
                            if (!a.SetEquals(b)) card.SwapChanged++;
                        }
                    }
                    foreach (var l in state.Ledgers) card.MaxNewsAtPort = Math.Max(card.MaxNewsAtPort, l.News.Count);
                    var work = new List<VoyageJob>();
                    VoyageJob home_ = null;
                    foreach (var j in board)
                    {
                        if (j.IsReturn) home_ = j; else work.Add(j);
                    }
                    foreach (var j in work)
                    {
                        var t = content.Job(j.TemplateId);
                        if (t != null) everOffered.Add(t.Faction);
                    }
                    if (work.Count == 0)
                    {
                        card.DeadBoards++;
                        if (home_ == null)
                        {
                            card.StuckAtHome++;
                            seated = Seat(crew, dice);
                            if (++stuck > 3) break;
                            VoyageWorld.TickDays(state, content, 1);
                            continue;
                        }
                    }
                    stuck = 0;

                    VoyageJob pick = null;
                    VoyageJob chapter = null;
                    foreach (var j in work) if (!string.IsNullOrEmpty(j.Chapter)) { chapter = j; break; }
                    // A crew chasing a story: the chapter when it is offered, else work that earns the standing the next
                    // chapter waits on (a saga's open chapter names the faction type it asks for).
                    VoyageJob toward = null;
                    foreach (var j in work)
                    {
                        var t = content.Job(j.TemplateId);
                        if (t != null && WaitsOn(content, state, t.Faction)) { toward = j; break; }
                    }
                    if (chapter != null && dice.NextDouble() < 0.7) pick = chapter;
                    else if (toward != null && dice.NextDouble() < 0.5) pick = toward;
                    else if (work.Count == 0) pick = home_;
                    else if (home_ != null && state.Aboard.Count >= 2 && dice.NextDouble() < 0.35) pick = home_;
                    else if (dice.NextDouble() < 0.6)
                    {
                        // The best pay for the risk: a player weighs the course, not the number alone.
                        double best = double.MinValue;
                        foreach (var j in work)
                        {
                            double value = j.Pay / (1.0 + 0.45 * (j.Intensity - 1) + 0.25 * j.Warnings.Count);
                            if (value > best) { best = value; pick = j; }
                        }
                    }
                    else pick = work[dice.Next(work.Count)];

                    if (!VoyageRules.Take(state, content, pick)) continue;
                    var arrival = VoyageRules.Arrive(state, content);
                    if (arrival == null) continue;
                    card.Days = Math.Max(card.Days, 0);

                    int hostile = 0;
                    foreach (var m in arrival.Rivals)
                    {
                        card.Meetings++;
                        switch (m.Relation)
                        {
                            case RivalRelation.Hostile: hostile++; card.HostileMeetings++; break;
                            case RivalRelation.CoOp: card.CoOpMeetings++; break;
                            case RivalRelation.Race: card.Races++; break;
                        }
                    }
                    if (arrival.KaijuHere) card.KaijuMet++;
                    if (arrival.NewlyCharted) card.Charted++;

                    if (!pick.IsReturn)
                    {
                        card.Jobs++;
                        if (pick.Source != null) card.NewsGrown++;
                        if (!string.IsNullOrEmpty(pick.Chapter)) card.ChapterJobs++;
                        card.PaySum += pick.Pay;
                        Bump(card.Verbs, pick.Verb);
                        Bump(card.Kinds, pick.Kind);
                        Bump(card.Destinations, pick.To);
                        Bump(card.Intensities, pick.Intensity);
                        if (nonReturn < 20) pairs.Add(pick.TemplateId + "@" + pick.To);
                        nonReturn++;
                    }

                    // The Ark may fall: rougher water and a hostile crew make it likelier.
                    double loss = 0.015 * pick.Intensity + 0.06 * hostile + (arrival.KaijuHere ? 0.05 : 0) + (arrival.HostileWorld ? 0.02 : 0);
                    if (!arrival.Extracts && dice.NextDouble() < loss)
                    {
                        card.ArksLost++;
                        VoyageRules.ArkLost(state, content, state.At, hostile > 0, new List<string>(state.Aboard));
                        continue;
                    }

                    if (pick.IsReturn)
                    {
                        VoyageRules.Resolve(state, content, JobOutcome.Succeeded);
                    }
                    else
                    {
                        bool ok = Work(content, state, pick, arrival, hostile, dice);
                        var r = VoyageRules.Resolve(state, content, ok ? JobOutcome.Succeeded : JobOutcome.Failed);
                        if (ok) card.Successes++; else card.Failures_++;
                        if (!string.IsNullOrEmpty(r.RelicId))
                        {
                            card.Relics++;
                            relic = true;
                            if (!relicsSeen.Add(r.RelicId)) card.RelicDuplicates++;
                        }
                        if (pick.Source != null && pick.Source.Tag == NewsTag.Kaiju && ok) card.KaijuFelled++;
                    }

                    if (arrival.Extracts)
                    {
                        var x = VoyageRules.Extract(state, content);
                        if (x != null)
                        {
                            card.Extractions++;
                            card.Extracted += x.Artifacts.Count;
                        }
                    }
                }
                card.Days += state.Day;
                card.DistinctPairsSum += pairs.Count;
                card.MaxStateJson = Math.Max(card.MaxStateJson, JsonUtility.ToJson(state).Length);
                if (relic) card.GalaxiesWithRelic++;
                foreach (var rs in state.Rivals) card.RivalDeeds += rs.Deeds;
                card.RivalWrecks += state.Log.RivalsWrecked;
                foreach (FactionType type in Enum.GetValues(typeof(FactionType)))
                    if (type != FactionType.None) Bump(card.TiersReached, VoyageRules.Tier(state, content, type));
            }
            card.TypesEverOffered = everOffered.Count;
            return card;
        }

        /// <summary>True when an open chapter asks for <paramref name="type"/> at a tier the crew has not reached.</summary>
        static bool WaitsOn(VoyageContent content, VoyageState state, FactionType type)
        {
            foreach (var id in state.ChaptersOpen)
            {
                var ch = content.Chapter(id, out _);
                if (ch != null && ch.Faction == type && ch.Tier > VoyageRules.Tier(state, content, type)) return true;
            }
            return false;
        }

        /// <summary>The work at the stop, rolled: a stow takes what stands there; a meet needs the crew there; the rest
        /// succeed by a chance that falls with the course and the rivals.</summary>
        static bool Work(VoyageContent content, VoyageState state, VoyageJob job, VoyageRules.Arrival arrival, int hostile, Dice dice)
        {
            double chance = Math.Clamp(0.92 - 0.14 * (job.Intensity - 1) - 0.12 * hostile - (arrival.HostileWorld ? 0.08 : 0), 0.25, 0.95);
            switch (job.Verb)
            {
                case WorkVerb.Stow:
                {
                    int available = arrival.VaultFinds.Count + arrival.WreckFinds.Count;
                    if (available == 0 || dice.NextDouble() >= chance) return false;
                    int want = Math.Max(1, (int)Math.Ceiling(job.Goal));
                    int took = 0;
                    foreach (var id in arrival.WreckFinds) { if (took >= want) break; VoyageRules.Stowed(state, content, id); took++; }
                    foreach (var id in arrival.VaultFinds) { if (took >= want) break; VoyageRules.Stowed(state, content, id); took++; }
                    return took >= want;
                }
                case WorkVerb.Meet:
                    return arrival.MetRival;
                case WorkVerb.Arrive:
                    return job.HullFloor <= 0f || dice.NextDouble() < chance + 0.05;
                default:
                    return dice.NextDouble() < chance;
            }
        }

        static List<string> Seat(List<string> crew, Dice dice)
        {
            var seats = new List<string>();
            var pool = new List<string>(crew);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int k = dice.Next(pool.Count);
                seats.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return seats;
        }

        static void Bump<T>(Dictionary<T, int> d, T key) => d[key] = d.TryGetValue(key, out int n) ? n + 1 : 1;

        // ── The content, measured once ──────────────────────────────────────

        static void MeasureContent(VoyageContent content, Scorecard card)
        {
            card.TextProblems.AddRange(content.Validate());
            card.TextProblems.AddRange(TextProblems(content));

            // Every place reachable from every harbour; every uncharted place a leaf off charted water.
            foreach (var d in Homes)
            {
                var harbour = content.HarbourOf(d);
                if (harbour == null) continue;
                var dist = VoyageRules.Distances(content, harbour.Id);
                foreach (var l in content.Locations)
                    if (l != null && !dist.ContainsKey(l.Id)) card.Unreachable++;
            }
            foreach (var l in content.Locations)
            {
                if (l == null || !l.Uncharted) continue;
                bool reached = false;
                foreach (var n in VoyageRules.Neighbours(content, l.Id))
                {
                    var other = content.Location(n);
                    if (other != null && !other.Uncharted) { reached = true; break; }
                }
                if (!reached) card.Unchartable++;
            }

            // Every faction type on the crew finds work at a harbour on the first day.
            var typesWithWork = new HashSet<FactionType>();
            foreach (var d in Homes)
            {
                var state = VoyageRules.NewGalaxy(content, d);
                var here = content.Location(state.At);
                var ledger = state.Ledger(state.At);
                foreach (var p in content.Crew)
                {
                    if (p == null || typesWithWork.Contains(p.Faction)) continue;
                    var options = VoyageRules.Candidates(state, content, here, ledger, p, null, null, forRival: false);
                    if (options.Count > 0) typesWithWork.Add(p.Faction);
                }
            }
            card.TypesWithWorkDay0 = typesWithWork.Count;
        }

        /// <summary>Every authored string that breaks the text rule: a non-ASCII character, or an unknown token.</summary>
        public static List<string> TextProblems(VoyageContent content)
        {
            var problems = new List<string>();
            var allowed = new HashSet<string> { "from", "to", "about", "pilot", "rival", "n", "kaiju", "relic", "saga", "faction",
                "tier", "holder", "home", "count", "seconds", "done", "goal", "percent" };
            Walk(content, "content", problems, allowed, 0);
            return problems;
        }

        static void Walk(object o, string path, List<string> problems, HashSet<string> allowed, int depth)
        {
            if (o == null || depth > 6) return;
            if (o is string s)
            {
                foreach (char c in s)
                    if (c < 32 || c > 126) { problems.Add($"{path}: non-ASCII '{c}'"); break; }
                int i = 0;
                while ((i = s.IndexOf('{', i)) >= 0)
                {
                    int j = s.IndexOf('}', i);
                    if (j < 0) { problems.Add($"{path}: unclosed token"); break; }
                    string tok = s.Substring(i + 1, j - i - 1);
                    if (!allowed.Contains(tok)) problems.Add($"{path}: unknown token {{{tok}}}");
                    i = j + 1;
                }
                return;
            }
            if (o is System.Collections.IEnumerable list && o is not string)
            {
                int k = 0;
                foreach (var item in list) Walk(item, $"{path}[{k++}]", problems, allowed, depth + 1);
                return;
            }
            var type = o.GetType();
            if (type.IsPrimitive || type.IsEnum) return;
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.IsStatic) continue;
                var v = f.GetValue(o);
                if (v is string || v is System.Collections.IEnumerable || (v != null && v.GetType().IsClass && v.GetType().Namespace == type.Namespace))
                    Walk(v, path + "." + f.Name, problems, allowed, depth + 1);
            }
        }
    }
}
#endif
