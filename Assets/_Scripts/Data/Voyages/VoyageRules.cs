using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The voyage's rules, pure (Docs/Voyages/SLICE.md §1): the board a port offers the seated pilots, the course to a
    /// job's destination, what stands there on arrival, how an outcome writes the news the next board is made of, a
    /// fallen Ark, and extraction at home. No engine type and no clock: the world (<c>VoyageRun</c>) asks, and these
    /// answer, so every rule runs in an edit-mode test.
    ///
    /// <para><b>The chain.</b> A job grows from a port's news (a sighting becomes a hunt, a wreck a salvage, a call for
    /// aid a defence), and its outcome writes new news (VOYAGES.md §6.3). Couriers carry news between ports, so what a
    /// port knows, and so what its pilots can find there, depends on who sailed in. Nothing is minted: a wreck exists
    /// because an Ark fell, a sighting because a vault chart was found.</para>
    /// </summary>
    public static class VoyageRules
    {
        /// <summary>The most jobs a board offers, beside the way home: one per seated pilot (a squad has three).</summary>
        public const int BoardSize = SavedSquad.SlotCount;

        /// <summary>News older than this many days is forgotten when a port is next read.</summary>
        public const int NewsLifeDays = 12;

        /// <summary>The template id of the always-offered return job; it is no faction's work.</summary>
        public const string ReturnJobId = "job.return";

        // ── Beginning ───────────────────────────────────────────────────────

        /// <summary>
        /// A galaxy as new as the content: each port's seed news and the seed wrecks. The squad's Ark is at
        /// <paramref name="home"/>'s harbour.
        /// </summary>
        public static VoyageState NewGalaxy(VoyageContent content, Domains home)
        {
            var state = new VoyageState();
            foreach (var s in content.Seeds ?? new List<SeedNews>())
                if (s != null && content.Location(s.Port) != null)
                    state.Ledger(s.Port).News.Add(new NewsItem { Tag = s.Tag, About = s.About, Line = s.Line, Day = 0 });
            foreach (var w in content.SeedWrecks ?? new List<SeedWreck>())
                if (w != null && w.Artifacts is { Count: > 0 })
                    state.Wrecks.Add(new WreckRecord { At = w.At, Artifacts = new List<string>(w.Artifacts), Line = w.Line });
            Embark(state, content, home);
            return state;
        }

        /// <summary>
        /// The squad sets out from its harbour: the Ark is home, between jobs, with nothing aboard. The galaxy (news,
        /// wrecks, the story) carries over from earlier voyages.
        /// </summary>
        public static void Embark(VoyageState state, VoyageContent content, Domains home)
        {
            state.Normalize();
            var harbour = content.HarbourOf(home) ?? content.HarbourOf(Domains.Jade);
            state.Home = harbour?.Id;
            state.At = state.Home;
            state.LastPort = state.Home;
            state.AtSea = false;
            state.Current = null;
            state.Aboard.Clear();
            state.Owed.Clear();
        }

        // ── The board ───────────────────────────────────────────────────────

        static bool IsPort(VoyageLocation l) =>
            l != null && (l.Experience == VoyageExperience.Port || l.Experience == VoyageExperience.Harbour);

        /// <summary>
        /// The jobs offered here: one from each seated pilot, in seat order, from the news the crew knows (this port's,
        /// or the last port's while out at a location), then the way home when the Ark is not there. A pilot with no
        /// person on the crew, or whose faction type has no work here, brings nothing, so which pilots you seat
        /// decides what you are offered.
        /// </summary>
        public static List<VoyageJob> Board(VoyageState state, VoyageContent content, IReadOnlyList<string> seatedPilotIds)
        {
            var board = new List<VoyageJob>();
            if (state == null || content == null || state.AtSea) return board;
            var here = content.Location(state.At);
            if (here == null) return board;

            string newsPort = IsPort(here) ? here.Id : state.LastPort;
            var ledger = state.Ledger(newsPort);
            Forget(ledger, state.Day);

            var usedNews = new List<NewsItem>();
            var usedTo = new List<string>();
            if (seatedPilotIds != null)
            {
                foreach (var pilotId in seatedPilotIds)
                {
                    if (board.Count >= BoardSize) break;
                    var person = content.Person(pilotId);
                    if (person == null) continue;
                    var job = DrawFor(state, content, here, ledger, person, usedNews, usedTo);
                    if (job == null) continue;
                    board.Add(job);
                    if (job.Source != null) usedNews.Add(job.Source);
                    usedTo.Add(job.To);
                }
            }

            if (state.At != state.Home && content.Location(state.Home) != null)
                board.Add(ReturnJob(state, content));
            return board;
        }

        static VoyageJob DrawFor(VoyageState state, VoyageContent content, VoyageLocation here, PortLedger ledger,
            CrewPerson person, List<NewsItem> usedNews, List<string> usedTo)
        {
            // News-grown work first: it is the chain, and it pays more.
            foreach (var t in content.Jobs)
            {
                if (t == null || t.Faction != person.Faction || t.Needs == NewsTag.None) continue;
                var news = FreshestNews(ledger, t.Needs, here.Id, usedNews, state);
                if (news == null) continue;
                if (content.Location(news.About) == null || Route(content, here.Id, news.About) == null) continue;
                return Make(state, content, t, person, here, news.About, news, null);
            }
            foreach (var t in content.Jobs)
            {
                if (t == null || t.Faction != person.Faction || t.Needs != NewsTag.None || t.Id == ReturnJobId) continue;
                var to = StandingDestination(state, content, t, here, person, usedTo);
                if (to == null) continue;
                NewsItem carried = t.Kind == JobKind.Courier ? DispatchFor(ledger, state.Ledger(to.Id), usedNews) : null;
                if (carried != null) usedNews.Add(carried);
                return Make(state, content, t, person, here, to.Id, null, carried);
            }
            return null;
        }

        static NewsItem FreshestNews(PortLedger ledger, NewsTag tag, string here, List<NewsItem> used, VoyageState state)
        {
            NewsItem best = null;
            foreach (var n in ledger.News)
            {
                if (n == null || n.Tag != tag || n.About == here || used.Contains(n)) continue;
                // A wreck is only news while something still lies there.
                if (tag == NewsTag.Wreck && state.WreckAt(n.About) == null) continue;
                if (best == null || n.Day > best.Day) best = n;
            }
            return best;
        }

        /// <summary>Where standing work goes: the nearest location of the template's kind, not here, not already on
        /// the board; ties broken by a draw seeded from the day and the pilot, so a board is the same when re-read.</summary>
        static VoyageLocation StandingDestination(VoyageState state, VoyageContent content, JobTemplate t,
            VoyageLocation here, CrewPerson person, List<string> usedTo)
        {
            var distances = Distances(content, here.Id);
            VoyageLocation best = null;
            int bestHops = int.MaxValue;
            int bestTie = int.MaxValue;
            foreach (var l in content.Locations)
            {
                if (l == null || l.Id == here.Id || usedTo.Contains(l.Id)) continue;
                if (!distances.TryGetValue(l.Id, out int hops)) continue;
                bool fits = t.Kind == JobKind.Courier ? IsPort(l) : l.Experience == t.Goes;
                if (!fits) continue;
                int tie = Hash(state.Day, person.PilotId, l.Id);
                if (hops < bestHops || (hops == bestHops && tie < bestTie))
                {
                    best = l;
                    bestHops = hops;
                    bestTie = tie;
                }
            }
            return best;
        }

        /// <summary>The freshest news here that the destination port does not already know: what a courier carries.</summary>
        static NewsItem DispatchFor(PortLedger from, PortLedger to, List<NewsItem> used)
        {
            NewsItem best = null;
            foreach (var n in from.News)
            {
                if (n == null || used.Contains(n) || to.Knows(n.Tag, n.About) || n.About == to.Port) continue;
                if (best == null || n.Day > best.Day) best = n;
            }
            return best;
        }

        static VoyageJob Make(VoyageState state, VoyageContent content, JobTemplate t, CrewPerson person,
            VoyageLocation here, string to, NewsItem from, NewsItem carried)
        {
            var dest = content.Location(to);
            var about = carried != null ? content.Location(carried.About) : dest;
            return new VoyageJob
            {
                TemplateId = t.Id,
                Kind = t.Kind,
                PilotId = person.PilotId,
                From = here.Id,
                To = to,
                Source = from,
                Carried = carried,
                Title = Fill(t.Title, here, dest, about, person),
                Summary = Fill(t.Summary, here, dest, about, person),
            };
        }

        static VoyageJob ReturnJob(VoyageState state, VoyageContent content)
        {
            var home = content.Location(state.Home);
            var here = content.Location(state.At);
            var t = content.Job(ReturnJobId);
            return new VoyageJob
            {
                TemplateId = ReturnJobId,
                Kind = JobKind.Courier,
                From = state.At,
                To = state.Home,
                IsReturn = true,
                Title = t != null ? Fill(t.Title, here, home, home, null) : string.Empty,
                Summary = t != null ? Fill(t.Summary, here, home, home, null) : string.Empty,
            };
        }

        /// <summary>Fills a template's tokens. An empty template stays empty (that is how a line is deleted).</summary>
        public static string Fill(string template, VoyageLocation from, VoyageLocation to, VoyageLocation about,
            CrewPerson pilot)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            return template
                .Replace("{from}", from?.Name ?? string.Empty)
                .Replace("{to}", to?.Name ?? string.Empty)
                .Replace("{about}", about?.Name ?? string.Empty)
                .Replace("{pilot}", pilot?.Name ?? string.Empty);
        }

        static void Forget(PortLedger ledger, int day) =>
            ledger.News.RemoveAll(n => n == null || day - n.Day > NewsLifeDays);

        // ── The course ──────────────────────────────────────────────────────

        /// <summary>Lane hops from <paramref name="from"/> to every reachable location.</summary>
        public static Dictionary<string, int> Distances(VoyageContent content, string from)
        {
            var dist = new Dictionary<string, int>();
            if (content.Location(from) == null) return dist;
            var queue = new Queue<string>();
            dist[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var next in Neighbours(content, at))
                {
                    if (dist.ContainsKey(next)) continue;
                    dist[next] = dist[at] + 1;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        static IEnumerable<string> Neighbours(VoyageContent content, string at)
        {
            foreach (var lane in content.Lanes)
            {
                if (lane == null) continue;
                if (lane.From == at) yield return lane.To;
                else if (lane.To == at) yield return lane.From;
            }
        }

        /// <summary>The locations passed from <paramref name="from"/> to <paramref name="to"/>, both included, by the
        /// fewest lanes (ties: the lane listed first). Null when there is no way there.</summary>
        public static List<string> Route(VoyageContent content, string from, string to)
        {
            if (content.Location(from) == null || content.Location(to) == null) return null;
            if (from == to) return new List<string> { from };
            var back = new Dictionary<string, string> { [from] = null };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                if (at == to) break;
                foreach (var next in Neighbours(content, at))
                {
                    if (back.ContainsKey(next)) continue;
                    back[next] = at;
                    queue.Enqueue(next);
                }
            }
            if (!back.ContainsKey(to)) return null;
            var path = new List<string>();
            for (var at = to; at != null; at = back[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        static VoyageLane Lane(VoyageContent content, string a, string b)
        {
            foreach (var lane in content.Lanes)
                if (lane != null && ((lane.From == a && lane.To == b) || (lane.From == b && lane.To == a))) return lane;
            return null;
        }

        /// <summary>
        /// The cells the Ark sails, in order, as indices into the content asset's cell pool: each lane's open-sea cells
        /// (drawn from the pool, seeded by the lane and the day, never the next location's own world) and then each
        /// location passed, ending with the destination's own world. Empty when there is no way there.
        /// </summary>
        public static List<int> Corridor(VoyageContent content, string from, string to, int day, int poolSize)
        {
            var cells = new List<int>();
            var path = Route(content, from, to);
            if (path == null || path.Count < 2 || poolSize <= 0) return cells;
            for (int i = 1; i < path.Count; i++)
            {
                var lane = Lane(content, path[i - 1], path[i]);
                var stop = content.Location(path[i]);
                int own = Mod(stop.Cell, poolSize);
                int open = lane != null ? Math.Max(0, lane.Cells) : 1;
                for (int k = 0; k < open; k++)
                {
                    int pick = Mod(Hash(day, path[i - 1] + ">" + path[i], k.ToString()), poolSize);
                    if (pick == own && poolSize > 1) pick = (pick + 1) % poolSize;
                    if (cells.Count > 0 && pick == cells[^1] && poolSize > 2) pick = (pick + 1) % poolSize;
                    if (pick == own && poolSize > 1) pick = (pick + 1) % poolSize;
                    cells.Add(pick);
                }
                cells.Add(own);
            }
            return cells;
        }

        // ── Taking a job and arriving ───────────────────────────────────────

        /// <summary>Take <paramref name="job"/> off the board: the Ark sets sail for its destination.</summary>
        public static bool Take(VoyageState state, VoyageContent content, VoyageJob job)
        {
            if (state == null || job == null || state.AtSea) return false;
            if (job.From != state.At || Route(content, state.At, job.To) == null) return false;
            state.Current = job;
            state.AtSea = true;
            state.Day++;
            if (!job.IsReturn) Tell(state, job.Title);
            return true;
        }

        /// <summary>What stands at a location when the Ark arrives.</summary>
        public sealed class Arrival
        {
            public VoyageLocation Location;
            /// <summary>The artifact inside each vault shell (vault fields), in shell order.</summary>
            public readonly List<string> VaultFinds = new();
            /// <summary>The artifacts lying in a wreck here, spilled for anyone.</summary>
            public readonly List<string> WreckFinds = new();
            /// <summary>Seconds the Ark must hold this core (cores; 0 otherwise).</summary>
            public float HoldSeconds;
            /// <summary>True when docking here takes everything aboard home.</summary>
            public bool Extracts;
        }

        /// <summary>
        /// The Ark arrived at the job's destination. The location's experience is stood from this: the vault shells and
        /// what each holds (vaults restock per visit, seeded by the day), any wreck lying here, a core's hold.
        /// </summary>
        public static Arrival Arrive(VoyageState state, VoyageContent content)
        {
            if (state == null || !state.AtSea || state.Current == null) return null;
            state.AtSea = false;
            state.At = state.Current.To;
            var here = content.Location(state.At);
            if (IsPort(here)) state.LastPort = here.Id;
            return Describe(state, content);
        }

        /// <summary>What stands at the Ark's location now (also used to re-stand it).</summary>
        public static Arrival Describe(VoyageState state, VoyageContent content)
        {
            var here = content.Location(state.At);
            if (here == null) return null;
            var arrival = new Arrival { Location = here, Extracts = state.At == state.Home };
            if (here.Experience == VoyageExperience.VaultField && here.Finds is { Count: > 0 })
                for (int i = 0; i < here.Vaults; i++)
                    arrival.VaultFinds.Add(here.Finds[Mod(Hash(state.Day, here.Id, i.ToString()), here.Finds.Count)]);
            if (here.Experience == VoyageExperience.Core) arrival.HoldSeconds = here.HoldSeconds;
            var wreck = state.WreckAt(here.Id);
            if (wreck != null) arrival.WreckFinds.AddRange(wreck.Artifacts);
            return arrival;
        }

        /// <summary>True when the job under way is done by arriving (a courier or the way home, Ark standing).</summary>
        public static bool DoneOnArrival(VoyageJob job) => job != null && job.Kind == JobKind.Courier;

        // ── Finds ───────────────────────────────────────────────────────────

        /// <summary>An artifact was stowed in the Ark here. A wreck's artifact leaves the wreck.</summary>
        public static void Stowed(VoyageState state, string artifactId)
        {
            if (state == null || string.IsNullOrEmpty(artifactId)) return;
            state.Aboard.Add(artifactId);
            var wreck = state.WreckAt(state.At);
            if (wreck != null && wreck.Artifacts.Remove(artifactId) && wreck.Artifacts.Count == 0)
                state.Wrecks.Remove(wreck);
        }

        /// <summary>True when the job's work is done here: a hunt or salvage once something was stowed at the
        /// destination (<paramref name="stowedHere"/>), a defence once the hold ran out, a courier on arrival.</summary>
        public static bool WorkDone(VoyageJob job, int stowedHere, bool held) => job?.Kind switch
        {
            JobKind.Courier => true,
            JobKind.Hunt => stowedHere > 0,
            JobKind.Salvage => stowedHere > 0,
            JobKind.Defence => held,
            _ => false,
        };

        // ── Outcomes and the chain ──────────────────────────────────────────

        /// <summary>What a resolution wrote: the story line and the news, for the HUD to say.</summary>
        public sealed class Resolution
        {
            public JobOutcome Outcome;
            public string StoryLine = string.Empty;
            public readonly List<string> NewsLines = new();
        }

        /// <summary>
        /// The job under way ended with <paramref name="outcome"/>. A courier's dispatch reaches its destination's news;
        /// the template's event writes more news (the chain); a success owes its reward at extraction; the log gets the
        /// authored line. The squad is between jobs afterwards, wherever the Ark is.
        /// </summary>
        public static Resolution Resolve(VoyageState state, VoyageContent content, JobOutcome outcome)
        {
            var result = new Resolution { Outcome = outcome };
            var job = state?.Current;
            if (job == null) return result;
            state.Current = null;
            state.AtSea = false;
            if (job.IsReturn) return result;

            var t = content.Job(job.TemplateId);
            var from = content.Location(job.From);
            var to = content.Location(job.To);
            var person = content.Person(job.PilotId);
            bool ok = outcome == JobOutcome.Succeeded;

            if (ok && job.Carried != null && IsPort(to))
            {
                var dispatch = job.Carried.Copy();
                dispatch.Day = state.Day;
                var ledger = state.Ledger(to.Id);
                if (!ledger.Knows(dispatch.Tag, dispatch.About))
                {
                    ledger.News.Add(dispatch);
                    result.NewsLines.Add(dispatch.Line);
                }
            }

            if (t != null)
            {
                if (ok) state.AddOwed(t.RewardElement, t.Reward);
                var ev = content.Event(ok ? t.OnSuccess : t.OnFail);
                if (ev != null) Apply(state, content, ev, job, result);
                result.StoryLine = Fill(ok ? t.SuccessLine : t.FailLine, from, to, to, person);
                Tell(state, result.StoryLine);
            }
            return result;
        }

        static void Apply(VoyageState state, VoyageContent content, VoyageEvent ev, VoyageJob job, Resolution result)
        {
            var dest = content.Location(job.To);
            if (ev.SpendsNews && job.Source != null)
                foreach (var l in state.Ledgers)
                    l.News.RemoveAll(n => n != null && n.Tag == job.Source.Tag && n.About == job.Source.About);

            foreach (var w in ev.Writes ?? new List<NewsWrite>())
            {
                if (w == null || w.Tag == NewsTag.None) continue;
                var port = PortFor(state, content, w.At, job);
                var about = AboutFor(content, w.About, job);
                if (port == null || about == null) continue;
                var ledger = state.Ledger(port.Id);
                if (ledger.Knows(w.Tag, about.Id)) continue;
                var line = Fill(w.Line, content.Location(job.From), dest, about, content.Person(job.PilotId));
                ledger.News.Add(new NewsItem { Tag = w.Tag, About = about.Id, Line = line, Day = state.Day });
                result.NewsLines.Add(line);
            }
        }

        static VoyageLocation PortFor(VoyageState state, VoyageContent content, NewsPlace at, VoyageJob job) => at switch
        {
            NewsPlace.Origin => NearestPort(content, job.From),
            NewsPlace.Home => content.Location(state.Home),
            _ => NearestPort(content, job.To),
        };

        static VoyageLocation AboutFor(VoyageContent content, NewsAbout about, VoyageJob job) => about switch
        {
            NewsAbout.AnotherVaultField => NearestOf(content, job.To, VoyageExperience.VaultField, job.To),
            NewsAbout.AnotherCore => NearestOf(content, job.To, VoyageExperience.Core, job.To),
            _ => content.Location(job.To),
        };

        /// <summary>The port or harbour nearest <paramref name="from"/> (itself, when it is one).</summary>
        public static VoyageLocation NearestPort(VoyageContent content, string from)
        {
            var here = content.Location(from);
            if (IsPort(here)) return here;
            var dist = Distances(content, from);
            VoyageLocation best = null;
            int bestHops = int.MaxValue;
            foreach (var l in content.Locations)
                if (IsPort(l) && dist.TryGetValue(l.Id, out int hops) && hops < bestHops) { best = l; bestHops = hops; }
            return best;
        }

        static VoyageLocation NearestOf(VoyageContent content, string from, VoyageExperience kind, string notThis)
        {
            var dist = Distances(content, from);
            VoyageLocation best = null;
            int bestHops = int.MaxValue;
            foreach (var l in content.Locations)
                if (l != null && l.Experience == kind && l.Id != notThis && dist.TryGetValue(l.Id, out int hops) && hops < bestHops)
                {
                    best = l;
                    bestHops = hops;
                }
            return best;
        }

        // ── The Ark falls; the squad comes home ─────────────────────────────

        /// <summary>
        /// The Ark fell at <paramref name="where"/> (the location it was at or sailing for). Everything found on this
        /// voyage is lost: to the raiders when <paramref name="taken"/>, otherwise into a wreck lying there, which is
        /// news at the nearest port, so another squad (or this one, next time) can salvage it. The squad's job fails
        /// and it limps home with nothing aboard.
        /// </summary>
        public static Resolution ArkLost(VoyageState state, VoyageContent content, string where, bool taken,
            IReadOnlyList<string> spilled)
        {
            string wreckLine = content.WreckLine, newsLine = content.WreckNewsLine;
            var result = state.Current != null
                ? Resolve(state, content, JobOutcome.ArkLost)
                : new Resolution { Outcome = JobOutcome.ArkLost };
            var at = content.Location(where);
            if (!taken && at != null && spilled is { Count: > 0 })
            {
                var wreck = state.WreckAt(at.Id);
                if (wreck == null)
                {
                    wreck = new WreckRecord { At = at.Id, Line = Fill(wreckLine, at, at, at, null) };
                    state.Wrecks.Add(wreck);
                }
                wreck.Artifacts.AddRange(spilled);
                var port = NearestPort(content, at.Id);
                if (port != null && !state.Ledger(port.Id).Knows(NewsTag.Wreck, at.Id))
                {
                    var line = Fill(newsLine, at, at, at, null);
                    state.Ledger(port.Id).News.Add(new NewsItem { Tag = NewsTag.Wreck, About = at.Id, Line = line, Day = state.Day });
                    result.NewsLines.Add(line);
                }
            }
            state.Aboard.Clear();
            state.Owed.Clear();
            state.AtSea = false;
            state.At = state.Home;
            return result;
        }

        /// <summary>
        /// The squad gave the voyage up away from home (flew the harbour station, left freestyle). The job under way
        /// fails, and what was found and owed is lost: only docking at home extracts (VOYAGES.md §11's default). The
        /// galaxy keeps what happened. Null when there was nothing to give up (the squad was home between jobs).
        /// </summary>
        public static Resolution Abandon(VoyageState state, VoyageContent content)
        {
            if (state == null || state.IsHome() && state.Current == null) return null;
            var result = state.Current != null
                ? Resolve(state, content, JobOutcome.Failed)
                : new Resolution { Outcome = JobOutcome.Failed };
            state.Aboard.Clear();
            state.Owed.Clear();
            state.AtSea = false;
            state.At = state.Home;
            state.LastPort = state.Home;
            return result;
        }

        /// <summary>
        /// A new voyage sets out from wherever the last one left the Ark (Garrett, 2026-10-28: "the jobs should be the
        /// different voyages"): each job is sailed as a voyage of its own, and a squad that finished one is anchored at
        /// its destination with what it found still aboard and still at stake. A leg cut off at sea (the game closed
        /// mid-voyage) is a voyage given up: <see cref="Abandon"/>. A first voyage is <see cref="NewGalaxy"/>'s.
        /// </summary>
        public static void Resume(VoyageState state, VoyageContent content, Domains home)
        {
            state.Normalize();
            if (string.IsNullOrEmpty(state.Home) || content.Location(state.Home) == null)
            {
                Embark(state, content, home);
                return;
            }
            if (state.AtSea)
            {
                Abandon(state, content);
                return;
            }
            if (state.Current != null) Resolve(state, content, JobOutcome.Failed);
            if (content.Location(state.At) == null)
            {
                state.At = state.Home;
                state.LastPort = state.Home;
            }
        }

        /// <summary>
        /// The run ends at a stop (the pilot left freestyle, the voyage over): a job still open there is done if its
        /// work is done and left undone otherwise, and the squad is anchored where it is. Ending at sea is
        /// <see cref="Abandon"/>. Null when nothing was open.
        /// </summary>
        public static Resolution Anchor(VoyageState state, VoyageContent content, bool workDone)
        {
            if (state == null) return null;
            if (state.AtSea) return Abandon(state, content);
            if (state.Current == null) return null;
            return Resolve(state, content, workDone ? JobOutcome.Succeeded : JobOutcome.Failed);
        }

        /// <summary>What docking at home hands over.</summary>
        public sealed class Extraction
        {
            public readonly List<string> Artifacts = new();
            /// <summary>Crystals by element (index = (int)Element).</summary>
            public readonly int[] Crystals = new int[(int)Element.Omni + 1];
        }

        /// <summary>
        /// The Ark is docked at home: everything aboard and every crystal owed is the player's now. Null away from
        /// home or at sea. Idempotent: a second call hands over nothing.
        /// </summary>
        public static Extraction Extract(VoyageState state)
        {
            if (state == null || !state.IsHome()) return null;
            var x = new Extraction();
            x.Artifacts.AddRange(state.Aboard);
            for (int i = 0; i < x.Crystals.Length; i++) x.Crystals[i] = state.OwedOf((Element)i);
            state.Aboard.Clear();
            state.Owed.Clear();
            return x;
        }

        // ── Small helpers ───────────────────────────────────────────────────

        /// <summary>The story keeps its last lines; older ones are let go so the record stays small.</summary>
        public const int StoryLength = 40;

        static void Tell(VoyageState state, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            state.Story.Add(new StoryEntry { Day = state.Day, Line = line });
            if (state.Story.Count > StoryLength) state.Story.RemoveRange(0, state.Story.Count - StoryLength);
        }

        static int Mod(int x, int m) => m <= 0 ? 0 : ((x % m) + m) % m;

        /// <summary>A stable hash (FNV-1a): string.GetHashCode is randomised per process, and a board must read the
        /// same on every machine and every run.</summary>
        static int Hash(int day, string a, string b)
        {
            unchecked
            {
                uint h = 2166136261;
                void Mix(string s)
                {
                    if (s == null) return;
                    foreach (char c in s) { h ^= c; h *= 16777619; }
                    h ^= 0xff; h *= 16777619;
                }
                Mix(day.ToString());
                Mix(a);
                Mix(b);
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
