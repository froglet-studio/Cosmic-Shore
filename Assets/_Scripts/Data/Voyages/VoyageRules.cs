using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The voyage's rules, pure (Docs/Voyages/SLICE.md §1, DEPTH.md): the board a port offers the crew, the course to
    /// a job's destination, what stands there on arrival (the finds, the hold, the rival crews, the Kaiju), how an
    /// outcome writes the news the next board is made of and moves the world, a fallen Ark, and extraction at home. No
    /// engine type and no clock: the world (<c>VoyageRun</c>) asks, and these answer, so every rule runs in an edit-mode
    /// test. The galaxy's own motion between jobs is <see cref="VoyageWorld"/>; the other crews are
    /// <see cref="VoyageRivals"/>.
    ///
    /// <para><b>The chain.</b> A job grows from a port's news (a sighting becomes a hunt, a wreck a salvage, a call for
    /// aid a defence), and its outcome writes new news (VOYAGES.md §6.3). Couriers carry news between ports, so what a
    /// port knows, and so what its pilots can find there, depends on who sailed in. Nothing is minted: a wreck exists
    /// because an Ark fell, a sighting because a vault chart was found or a field restocked.</para>
    /// </summary>
    public static class VoyageRules
    {
        /// <summary>The seats whose pilots lead the board: one job each, before the rest of the crew fills it.</summary>
        public const int BoardSize = SavedSquad.SlotCount;

        /// <summary>News older than this many days is forgotten when a port is next read (the world's own setting wins).</summary>
        public const int NewsLifeDays = 12;

        /// <summary>The template id of the always-offered return job; it is no faction's work.</summary>
        public const string ReturnJobId = "job.return";

        /// <summary>The rival id news about the squad's own Ark carries (a bounty on you, a rumour of your hold).</summary>
        public const string SelfRival = "self";

        // ── Beginning ───────────────────────────────────────────────────────

        /// <summary>
        /// A galaxy as new as the content: each port's seed news, the seed wrecks, every place at rest, every rival crew
        /// at home, the Kaiju sighted. The squad's Ark is at <paramref name="home"/>'s harbour.
        /// </summary>
        public static VoyageState NewGalaxy(VoyageContent content, Domains home)
        {
            var state = new VoyageState();
            foreach (var s in content.Seeds ?? new List<SeedNews>())
                if (s != null && content.Location(s.Port) != null)
                    state.Ledger(s.Port).News.Add(new NewsItem { Tag = s.Tag, About = s.About, Line = s.Line, Day = 0 });
            foreach (var w in content.SeedWrecks ?? new List<SeedWreck>())
                if (w != null && w.Artifacts is { Count: > 0 })
                    state.Wrecks.Add(new WreckRecord { At = w.At, Artifacts = new List<string>(w.Artifacts), Line = w.Line, Day = 0 });
            Embark(state, content, home);
            VoyageWorld.Awaken(state, content);
            return state;
        }

        /// <summary>
        /// The squad sets out from its harbour: the Ark is home, between jobs, with nothing aboard. The galaxy (news,
        /// wrecks, places, rivals, the story) carries over from earlier voyages.
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
            state.Carrying.Clear();
            VoyageWorld.Begin(state, content);
        }

        // ── The board ───────────────────────────────────────────────────────

        public static bool IsPort(VoyageLocation l) =>
            l != null && (l.Experience == VoyageExperience.Port || l.Experience == VoyageExperience.Harbour);

        internal static bool HasSealedFinds(VoyageLocation l) =>
            l != null && (l.Experience == VoyageExperience.VaultField || l.Experience == VoyageExperience.Hoard);

        /// <summary>The jobs offered here from the seated pilots alone (one each), then the way home.</summary>
        public static List<VoyageJob> Board(VoyageState state, VoyageContent content, IReadOnlyList<string> seatedPilotIds) =>
            Board(state, content, seatedPilotIds, null);

        /// <summary>
        /// The jobs offered here: one from each seated pilot, in seat order, from the news the crew knows (this port's,
        /// or the last port's while out at a location); then one from each other pilot of the crew until the board is
        /// full (DEPTH.md §4, the team's "about six options"); then the way home when the Ark is not there. A pilot with
        /// no person on the crew, or whose faction type has no work here, brings nothing, so which pilots you seat and
        /// recruit decides what you are offered. The seated pilots' jobs come first: the pilot card changes the voyage.
        /// </summary>
        public static List<VoyageJob> Board(VoyageState state, VoyageContent content, IReadOnlyList<string> seatedPilotIds,
            IReadOnlyList<string> crewPilotIds)
        {
            var board = new List<VoyageJob>();
            if (state == null || content == null || state.AtSea) return board;
            var here = content.Location(state.At);
            if (here == null) return board;
            VoyageWorld.Begin(state, content);

            string newsPort = IsPort(here) ? here.Id : state.LastPort;
            var ledger = state.Ledger(newsPort);
            Forget(ledger, state.Day, content);

            int size = Math.Max(1, content.World?.BoardSize ?? 6);
            var usedNews = new List<NewsItem>();
            var usedTo = new List<string>();
            var drawn = new HashSet<string>();
            void Draw(string pilotId, VoyageJob job)
            {
                board.Add(job);
                if (job.Source != null) usedNews.Add(job.Source);
                if (job.Carried != null) usedNews.Add(job.Carried);
                usedTo.Add(job.To);
            }
            // The seated pilots lead, one each, in seat order.
            if (seatedPilotIds != null)
                foreach (var pilotId in seatedPilotIds)
                {
                    if (board.Count >= Math.Min(size, BoardSize)) break;
                    if (string.IsNullOrEmpty(pilotId) || !drawn.Add(pilotId)) continue;
                    var person = content.Person(pilotId);
                    if (person == null) continue;
                    var options = Candidates(state, content, here, ledger, person, usedNews, usedTo, forRival: false);
                    if (options.Count > 0) Draw(pilotId, options[0]);
                }
            // The rest of the crew fills the board with its most interesting work first: a saga's chapter, then work
            // grown from the news, then by how far the crew's standing with that pilot's faction has come.
            if (crewPilotIds != null && board.Count < size)
            {
                var offers = new List<(VoyageJob job, string pilot, int rank)>();
                foreach (var pilotId in crewPilotIds)
                {
                    if (string.IsNullOrEmpty(pilotId) || drawn.Contains(pilotId)) continue;
                    var person = content.Person(pilotId);
                    if (person == null) continue;
                    var options = Candidates(state, content, here, ledger, person, usedNews, usedTo, forRival: false);
                    if (options.Count == 0) continue;
                    var job = options[0];
                    int rank = (!string.IsNullOrEmpty(job.Chapter) ? 1000 : 0) + (job.Source != null ? 100 : 0)
                               + Tier(state, content, person.Faction) * 10;
                    offers.Add((job, pilotId, rank));
                }
                offers.Sort((a, b) => b.rank.CompareTo(a.rank));
                foreach (var (job, pilot, _) in offers)
                {
                    if (board.Count >= size) break;
                    // Another pilot's pick may have taken this news or place since the offers were drawn.
                    if ((job.Source != null && usedNews.Contains(job.Source)) || usedTo.Contains(job.To)) continue;
                    drawn.Add(pilot);
                    Draw(pilot, job);
                }
            }

            if (state.At != state.Home && content.Location(state.Home) != null)
                board.Add(ReturnJob(state, content));
            return board;
        }

        /// <summary>
        /// Every job <paramref name="person"/> could bring here, best first: a saga chapter their faction type and tier
        /// can take, then news-grown work (the chain, freshest first, one per template), then standing work (nearest
        /// first). A rival crew draws from the same list (<paramref name="forRival"/>: it ignores the tiers and the
        /// chart, and never takes the Kaiju, a chapter or a bounty on itself).
        /// </summary>
        public static List<VoyageJob> Candidates(VoyageState state, VoyageContent content, VoyageLocation here,
            PortLedger ledger, CrewPerson person, List<NewsItem> usedNews, List<string> usedTo, bool forRival)
        {
            var options = new List<VoyageJob>();
            if (person == null || here == null) return options;
            usedNews ??= new List<NewsItem>();
            usedTo ??= new List<string>();
            var routeState = forRival ? null : state;
            int tier = Tier(state, content, person.Faction);

            if (!forRival)
            {
                foreach (var n in ledger.News)
                {
                    if (n == null || n.Tag != NewsTag.Chapter || usedNews.Contains(n)) continue;
                    var chapter = content.Chapter(n.Chapter, out _);
                    if (chapter == null || chapter.Faction != person.Faction || chapter.Tier > tier) continue;
                    if (state.ChaptersDone.Contains(chapter.Id)) continue;
                    var t = content.Job(chapter.Job);
                    if (t == null || content.Location(n.About) == null || Route(content, here.Id, n.About, routeState) == null) continue;
                    if (n.About == here.Id) continue;
                    var job = Make(state, content, t, person, here, n.About, n, null);
                    job.Chapter = chapter.Id;
                    options.Add(job);
                }
            }

            // News-grown work first: it is the chain, and it pays more. The Kaiju, the one lit beacon, leads it; the
            // rest by the freshness of the news.
            var grown = new List<VoyageJob>();
            foreach (var t in content.Jobs)
            {
                if (t == null || t.Faction != person.Faction || t.Needs == NewsTag.None) continue;
                if (forRival && !t.RivalsMayTake) continue;
                if (!forRival && t.Tier > tier) continue;
                var news = FreshestNews(ledger, t.Needs, here.Id, usedNews, state, forRival);
                if (news == null) continue;
                if (content.Location(news.About) == null || Route(content, here.Id, news.About, routeState) == null) continue;
                grown.Add(Make(state, content, t, person, here, news.About, news, null));
            }
            grown.Sort((a, b) =>
            {
                int ka = a.Source?.Tag == NewsTag.Kaiju ? 1 : 0, kb = b.Source?.Tag == NewsTag.Kaiju ? 1 : 0;
                if (ka != kb) return kb - ka;
                return (b.Source?.Day ?? 0).CompareTo(a.Source?.Day ?? 0);
            });
            options.AddRange(grown);
            foreach (var t in content.Jobs)
            {
                if (t == null || t.Faction != person.Faction || t.Needs != NewsTag.None || t.Id == ReturnJobId) continue;
                if (forRival && !t.RivalsMayTake) continue;
                if (!forRival && t.Tier > tier) continue;
                var to = StandingDestination(state, content, t, here, person, usedTo, forRival);
                if (to == null) continue;
                NewsItem carried = !forRival && t.Kind == JobKind.Courier ? DispatchFor(ledger, state.Ledger(to.Id), usedNews) : null;
                options.Add(Make(state, content, t, person, here, to.Id, null, carried));
            }
            return options;
        }

        static NewsItem FreshestNews(PortLedger ledger, NewsTag tag, string here, List<NewsItem> used, VoyageState state,
            bool forRival)
        {
            NewsItem best = null;
            foreach (var n in ledger.News)
            {
                if (n == null || n.Tag != tag || n.About == here || used.Contains(n)) continue;
                // News about the squad's own Ark is a warning to it, never its work (and never a rival's draw either:
                // the hunt for a laden Ark is VoyageRivals' own move).
                if (n.Rival == SelfRival) continue;
                // A wreck is only news while something still lies there.
                if (tag == NewsTag.Wreck && state.WreckAt(n.About) == null) continue;
                if (best == null || n.Day > best.Day) best = n;
            }
            return best;
        }

        /// <summary>Where standing work goes: the nearest location of the template's kind, not here, not already on
        /// the board; ties broken by a draw seeded from the day and the pilot, so a board is the same when re-read.</summary>
        static VoyageLocation StandingDestination(VoyageState state, VoyageContent content, JobTemplate t,
            VoyageLocation here, CrewPerson person, List<string> usedTo, bool forRival)
        {
            var distances = Distances(content, here.Id, forRival ? null : state, null);
            var squadDomain = SquadDomain(state, content);
            VoyageLocation best = null;
            int bestHops = int.MaxValue;
            int bestTie = int.MaxValue;
            foreach (var l in content.Locations)
            {
                if (l == null || l.Id == here.Id || usedTo.Contains(l.Id)) continue;
                bool fits;
                if (t.GoesUncharted)
                {
                    var place = state.PlaceIfAny(l.Id);
                    fits = l.Uncharted && (forRival ? !(place?.KnownToOthers ?? false) : !(place?.Charted ?? !l.Uncharted));
                    // An uncharted place is reached only along charted lanes, as a leaf.
                    if (fits && Route(content, here.Id, l.Id, forRival ? null : state) == null) fits = false;
                }
                else if (t.Kind == JobKind.Courier) fits = IsPort(l);
                else fits = l.Experience == t.Goes;
                // A field picked clean and not yet lit again has nothing sealed to open.
                if (fits && EffectiveVerb(t) == WorkVerb.Stow && HasSealedFinds(l) && (state.PlaceIfAny(l.Id)?.Sealed ?? -1) == 0
                    && state.WreckAt(l.Id) == null) fits = false;
                if (fits && t.AvoidsOwnDomain && l.Experience == VoyageExperience.Harbour && l.Home == squadDomain) fits = false;
                if (fits && !t.GoesUncharted && !IsVisible(state, content, l.Id) && !forRival) fits = false;
                if (!fits) continue;
                if (!distances.TryGetValue(l.Id, out int hops))
                {
                    if (!t.GoesUncharted) continue;
                    hops = Route(content, here.Id, l.Id, forRival ? null : state).Count - 1;
                }
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
                if (n.Tag == NewsTag.Chapter || n.Rival == SelfRival) continue;
                if (best == null || n.Day > best.Day) best = n;
            }
            return best;
        }

        static VoyageJob Make(VoyageState state, VoyageContent content, JobTemplate t, CrewPerson person,
            VoyageLocation here, string to, NewsItem from, NewsItem carried)
        {
            var dest = content.Location(to);
            var about = carried != null ? content.Location(carried.About) : dest;
            var crew = from != null && !string.IsNullOrEmpty(from.Rival) && from.Rival != SelfRival ? content.Rival(from.Rival) : null;
            var route = Route(content, here.Id, to, state) ?? Route(content, here.Id, to);
            int hops = route != null ? Math.Max(1, route.Count - 1) : 1;
            int intensity = Intensity(state, content, route);
            int tier = Tier(state, content, person.Faction);
            var job = new VoyageJob
            {
                TemplateId = t.Id,
                Kind = t.Kind,
                PilotId = person.PilotId,
                From = here.Id,
                To = to,
                Source = from,
                Carried = carried,
                Title = Fill(t.Title, here, dest, about, person, crew?.Name),
                Summary = Fill(t.Summary, here, dest, about, person, crew?.Name),
                Verb = EffectiveVerb(t),
                Goal = t.Goal,
                HullFloor = t.HullFloor,
                NoKill = t.NoKill,
                Role = t.Role,
                Intensity = intensity,
                Hops = hops,
                RivalId = crew?.Id ?? string.Empty,
            };
            // The Kaiju is never easier than the hardest felled (DEPTH.md §3).
            if (from != null && from.Tag == NewsTag.Kaiju && content.Kaiju != null)
                job.Goal = content.Kaiju.CullGoal + content.Kaiju.CullStep * state.Kaiju.Tier;
            if (t.Verb == WorkVerb.Hold && job.Goal <= 1f && dest != null) job.Goal = dest.HoldSeconds;
            job.Pay = Pay(state, content, t, intensity, hops, tier, from);
            // An explorer who comes second is paid less (the Notion Explore page: first back is paid most).
            if ((t.GoesUncharted || from?.Tag == NewsTag.Chart) && (state.PlaceIfAny(to)?.KnownToOthers ?? false))
                job.Pay = Math.Max(1, job.Pay / 2);
            job.Warnings = Warnings(state, content, job);
            return job;
        }

        /// <summary>The work a template asks: its verb, or, for a template authored before the verbs (or a test fixture
        /// that names only a kind), what its kind always asked.</summary>
        public static WorkVerb EffectiveVerb(JobTemplate t)
        {
            if (t == null) return WorkVerb.Arrive;
            if (t.Verb != WorkVerb.Arrive) return t.Verb;
            return t.Kind switch
            {
                JobKind.Hunt or JobKind.Salvage or JobKind.Raid => WorkVerb.Stow,
                JobKind.Defence or JobKind.Rite or JobKind.Tend or JobKind.Rescue => WorkVerb.Hold,
                JobKind.Cull => WorkVerb.Cull,
                JobKind.Harvest or JobKind.Exhibition => WorkVerb.Gather,
                JobKind.Study => WorkVerb.Observe,
                JobKind.Build => WorkVerb.Lay,
                JobKind.Liberate => WorkVerb.Raze,
                JobKind.Bounty => WorkVerb.Meet,
                _ => WorkVerb.Arrive,
            };
        }

        static VoyageJob ReturnJob(VoyageState state, VoyageContent content)
        {
            var home = content.Location(state.Home);
            var here = content.Location(state.At);
            var t = content.Job(ReturnJobId);
            var route = Route(content, state.At, state.Home, state) ?? Route(content, state.At, state.Home);
            var job = new VoyageJob
            {
                TemplateId = ReturnJobId,
                Kind = JobKind.Courier,
                From = state.At,
                To = state.Home,
                IsReturn = true,
                Verb = WorkVerb.Arrive,
                Title = t != null ? Fill(t.Title, here, home, home, null) : string.Empty,
                Summary = t != null ? Fill(t.Summary, here, home, home, null) : string.Empty,
                Hops = route != null ? Math.Max(1, route.Count - 1) : 1,
                Intensity = Intensity(state, content, route),
            };
            job.Warnings = Warnings(state, content, job);
            return job;
        }

        /// <summary>Fills a template's tokens. An empty template stays empty (that is how a line is deleted).</summary>
        public static string Fill(string template, VoyageLocation from, VoyageLocation to, VoyageLocation about,
            CrewPerson pilot) => Fill(template, from, to, about, pilot, null);

        /// <summary>Fills a template's tokens, the rival crew's name included.</summary>
        public static string Fill(string template, VoyageLocation from, VoyageLocation to, VoyageLocation about,
            CrewPerson pilot, string rival)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            return template
                .Replace("{from}", from?.Name ?? string.Empty)
                .Replace("{to}", to?.Name ?? string.Empty)
                .Replace("{about}", about?.Name ?? string.Empty)
                .Replace("{pilot}", pilot?.Name ?? string.Empty)
                .Replace("{rival}", rival ?? string.Empty);
        }

        /// <summary>One of several ways to say a thing, picked by the day and the place (so no two outcomes read the
        /// same, and the same outcome reads the same on every machine); the one line when there are no variants.</summary>
        public static string PickLine(List<string> variants, string line, int day, string a, string b)
        {
            if (variants == null || variants.Count == 0) return line ?? string.Empty;
            var pick = variants[Mod(Hash(day, a, b), variants.Count)];
            return string.IsNullOrEmpty(pick) ? line ?? string.Empty : pick;
        }

        /// <summary>Replaces each "{token}" with its value; <paramref name="pairs"/> is token, value, token, value...</summary>
        public static string Tokens(string template, params string[] pairs)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                template = template.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            return template;
        }

        internal static void Forget(PortLedger ledger, int day, VoyageContent content)
        {
            int life = content?.World != null && content.World.NewsLifeDays > 0 ? content.World.NewsLifeDays : NewsLifeDays;
            // A saga's chapter waits for the pilot who can take it; news about the squad itself is the world's to refresh.
            ledger.News.RemoveAll(n => n == null || (n.Tag != NewsTag.Chapter && n.Rival != SelfRival && day - n.Day > life));
        }

        // ── Standing, pay, intensity, warnings ──────────────────────────────

        /// <summary>The squad's standing tier with a faction type: 0 Stranger, 1 Known, 2 Trusted (DEPTH.md §6).</summary>
        public static int Tier(VoyageState state, VoyageContent content, FactionType type)
        {
            if (state == null) return 0;
            var thresholds = content?.World?.TierThresholds;
            int known = thresholds is { Count: >= 1 } ? thresholds[0] : 2;
            int trusted = thresholds is { Count: >= 2 } ? thresholds[1] : 5;
            int standing = state.StandingOf(type);
            return standing >= trusted ? 2 : standing >= known ? 1 : 0;
        }

        /// <summary>The tier's authored name ({tier}).</summary>
        public static string TierName(VoyageContent content, int tier) => tier switch
        {
            2 => content?.Words?.TierTrusted ?? string.Empty,
            1 => content?.Words?.TierKnown ?? string.Empty,
            _ => content?.Words?.TierStranger ?? string.Empty,
        };

        /// <summary>The role's authored name ({role}).</summary>
        public static string RoleName(VoyageContent content, SquadRole role) => role switch
        {
            SquadRole.Guardian => content?.Words?.RoleGuardian ?? string.Empty,
            SquadRole.Destroyer => content?.Words?.RoleDestroyer ?? string.Empty,
            SquadRole.Cultivator => content?.Words?.RoleCultivator ?? string.Empty,
            _ => content?.Words?.RoleAny ?? string.Empty,
        };

        /// <summary>
        /// What a job pays (DEPTH.md §7): the template's base, times the course's intensity, times its lanes beyond the
        /// first, times the standing tier, fading with the age of the news it grew from. Rounded, never below one for
        /// paid work.
        /// </summary>
        public static int Pay(VoyageState state, VoyageContent content, JobTemplate t, int intensity, int hops, int tier, NewsItem news)
        {
            if (t == null || t.Reward <= 0) return 0;
            var w = content?.World ?? new VoyageWorldRules();
            double pay = t.Reward;
            pay *= At(w.IntensityPay, intensity - 1, 1.0);
            pay *= 1.0 + Math.Max(0, hops - 1) * w.HopsPay;
            pay *= At(w.TierPay, tier, 1.0);
            if (news != null && state != null)
            {
                int age = Math.Max(0, state.Day - news.Day);
                pay *= Math.Max(w.FreshnessFloor, 1.0 - age * w.FreshnessPerDay);
            }
            return Math.Max(1, (int)Math.Round(pay, MidpointRounding.AwayFromZero));
        }

        static double At(List<float> table, int i, double fallback) =>
            table != null && i >= 0 && i < table.Count ? table[i] : fallback;

        /// <summary>
        /// How rough a course is, I1 to I4: the real sea's intensity along the route when the content is charted
        /// (<see cref="VoyageChart"/>), else from the lanes' length; plus the destination's pressure (DEPTH.md §2).
        /// </summary>
        public static int Intensity(VoyageState state, VoyageContent content, IReadOnlyList<string> route)
        {
            if (content == null || route == null || route.Count < 2) return 1;
            int sea = 1;
            var chart = VoyageChart.For(content);
            var along = chart?.Along(route);
            if (along != null) sea = along.Intensity;
            else
            {
                int cells = 0;
                for (int i = 1; i < route.Count; i++) cells += Math.Max(1, Lane(content, route[i - 1], route[i])?.Cells ?? 1);
                sea = 1 + cells / 3;
            }
            float pressure = state?.PlaceIfAny(route[^1])?.Pressure ?? 0f;
            return Math.Clamp(sea + (int)Math.Floor(pressure / 1.5f), 1, 4);
        }

        /// <summary>The warnings a card carries, from the world as it is (DEPTH.md §7): authored lines, in order.</summary>
        public static List<string> Warnings(VoyageState state, VoyageContent content, VoyageJob job)
        {
            var lines = new List<string>();
            var words = content?.Words;
            if (state == null || content == null || job == null || words == null) return lines;
            var w = content.World ?? new VoyageWorldRules();
            var to = content.Location(job.To);
            var place = state.PlaceIfAny(job.To);
            void Add(string template, string rival = null, int n = 0)
            {
                string line = Tokens(Fill(template, content.Location(job.From), to, to, null, rival), "n", n.ToString());
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }
            if (place != null && place.Pressure >= w.AidPressure) Add(words.WarningPressure);
            if (place is { HasHolder: true } && to != null && to.Experience == VoyageExperience.Core && place.Holder != SquadDomain(state, content))
                Add(words.WarningHeld);
            foreach (var crew in content.Rivals ?? new List<RivalCrew>())
            {
                var rs = state.Rival(crew?.Id);
                if (crew == null || rs == null || !rs.IsOut) continue;
                bool there = (string.IsNullOrEmpty(rs.To) && rs.At == job.To) || rs.To == job.To;
                if (!there || crew.Domain == SquadDomain(state, content)) continue;
                Add(words.WarningRival, crew.Name);
                break;
            }
            if (content.Kaiju is { Enabled: true } && state.Kaiju is { Alive: true } && !string.IsNullOrEmpty(state.Kaiju.At))
            {
                bool near = state.Kaiju.At == job.To;
                if (!near)
                    foreach (var next in Neighbours(content, job.To))
                        if (next == state.Kaiju.At) { near = true; break; }
                if (near) Add(words.WarningKaiju);
            }
            if (state.Notoriety >= w.BountyAt) Add(words.WarningBounty);
            if (state.Aboard.Count >= w.LadenRumourAt) Add(words.WarningLaden, null, state.Aboard.Count);
            return lines;
        }

        internal static Domains SquadDomain(VoyageState state, VoyageContent content)
        {
            var home = content?.Location(state?.Home);
            return home != null ? home.Home : Domains.Jade;
        }

        /// <summary>True when this crew can see <paramref name="locationId"/> on its chart (DEPTH.md §8): every place the
        /// content charts, and an uncharted place once the crew has found it.</summary>
        public static bool IsVisible(VoyageState state, VoyageContent content, string locationId)
        {
            var l = content?.Location(locationId);
            if (l == null) return false;
            if (!l.Uncharted) return true;
            var place = state?.PlaceIfAny(locationId);
            return place != null && place.Charted;
        }

        // ── The course ──────────────────────────────────────────────────────

        /// <summary>Lane hops from <paramref name="from"/> to every reachable location (every lane, charted or not).</summary>
        public static Dictionary<string, int> Distances(VoyageContent content, string from) => Distances(content, from, null, null);

        /// <summary>Lane hops from <paramref name="from"/> to every location reachable along places this crew can see
        /// (when <paramref name="state"/> is given); <paramref name="allow"/> is one place passable regardless.</summary>
        public static Dictionary<string, int> Distances(VoyageContent content, string from, VoyageState state, string allow)
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
                    if (dist.ContainsKey(next) || !Passable(content, state, next, allow)) continue;
                    dist[next] = dist[at] + 1;
                    // An uncharted place is a leaf: nothing is routed through it.
                    if (next == allow && !IsVisible(state, content, next)) continue;
                    queue.Enqueue(next);
                }
            }
            return dist;
        }

        static bool Passable(VoyageContent content, VoyageState state, string id, string allow) =>
            state == null || id == allow || IsVisible(state, content, id);

        public static IEnumerable<string> Neighbours(VoyageContent content, string at)
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
        public static List<string> Route(VoyageContent content, string from, string to) => Route(content, from, to, null);

        /// <summary>The route along places this crew can see (when <paramref name="state"/> is given); the destination
        /// itself may be uncharted (that is how it is found). Null when there is no way there.</summary>
        public static List<string> Route(VoyageContent content, string from, string to, VoyageState state)
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
                    if (back.ContainsKey(next) || !Passable(content, state, next, to)) continue;
                    back[next] = at;
                    if (next == to) { queue.Clear(); queue.Enqueue(next); break; }
                    queue.Enqueue(next);
                }
            }
            if (!back.ContainsKey(to)) return null;
            var path = new List<string>();
            for (var at = to; at != null; at = back[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        internal static VoyageLane Lane(VoyageContent content, string a, string b)
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
        public static List<int> Corridor(VoyageContent content, string from, string to, int day, int poolSize) =>
            Corridor(content, from, to, day, poolSize, null);

        /// <summary>The corridor along the route this crew can sail (<paramref name="state"/> given).</summary>
        public static List<int> Corridor(VoyageContent content, string from, string to, int day, int poolSize, VoyageState state)
        {
            var cells = new List<int>();
            var path = Route(content, from, to, state) ?? Route(content, from, to);
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

        /// <summary>
        /// The population scale of every cell of a corridor, in corridor order (DEPTH.md §2, §10): each lane's profile
        /// (or the world's leg profile) over its open-sea cells, hardest in the middle; each location passed at 1; the
        /// destination's own world as <see cref="Arrival.PopulationScale"/> says. Empty when there is no way there.
        /// </summary>
        public static List<float> CorridorProfile(VoyageState state, VoyageContent content, string from, string to)
        {
            var scales = new List<float>();
            var path = Route(content, from, to, state) ?? Route(content, from, to);
            if (path == null || path.Count < 2) return scales;
            var world = content.World ?? new VoyageWorldRules();
            for (int i = 1; i < path.Count; i++)
            {
                var lane = Lane(content, path[i - 1], path[i]);
                int open = lane != null ? Math.Max(0, lane.Cells) : 1;
                var profile = lane?.Profile is { Count: > 0 } ? lane.Profile : world.LegProfile;
                for (int k = 0; k < open; k++)
                    scales.Add(profile is { Count: > 0 } ? Math.Max(0.1f, profile[Math.Min(k, profile.Count - 1)]) : 1f);
                scales.Add(i == path.Count - 1 ? PopulationAt(state, content, path[i]) : 1f);
            }
            return scales;
        }

        /// <summary>The population scale of a place's own world, from what it is and how it stands (DEPTH.md §2).</summary>
        public static float PopulationAt(VoyageState state, VoyageContent content, string locationId)
        {
            var l = content?.Location(locationId);
            if (l == null) return 1f;
            var w = content.World ?? new VoyageWorldRules();
            var place = state?.PlaceIfAny(locationId);
            float scale = 1f;
            if (l.Experience == VoyageExperience.Lair) scale = w.LairPopulation;
            else if (l.Experience == VoyageExperience.Pen) scale = w.PenPopulation;
            if (place != null)
            {
                scale += place.Pressure * w.PressurePopulation;
                scale += place.Bloom * w.BloomPopulation;
            }
            if (content.Kaiju is { Enabled: true } && state?.Kaiju is { Alive: true } && state.Kaiju.At == locationId)
                scale += content.Kaiju.PopulationPerTier * (1 + state.Kaiju.Tier);
            return Math.Max(0.1f, scale);
        }

        // ── Taking a job and arriving ───────────────────────────────────────

        /// <summary>Take <paramref name="job"/> off the board: the Ark sets sail for its destination, and a day passes
        /// on the sea (the galaxy ticks, <see cref="VoyageWorld.Tick"/>).</summary>
        public static bool Take(VoyageState state, VoyageContent content, VoyageJob job)
        {
            if (state == null || job == null || state.AtSea) return false;
            if (job.From != state.At || Route(content, state.At, job.To) == null) return false;
            state.Current = job;
            state.AtSea = true;
            state.Day++;
            state.Log.Voyages++;
            state.Log.Legs += Math.Max(1, job.Hops);
            if (!job.IsReturn) Tell(state, job.Title);
            VoyageWorld.Tick(state, content);
            return true;
        }

        /// <summary>A rival crew met at the stop.</summary>
        public sealed class RivalMeeting
        {
            public RivalCrew Crew;
            public RivalState State;
            public RivalRelation Relation;
            /// <summary>The crew's arrival line, filled.</summary>
            public string Line = string.Empty;
            /// <summary>The relation's line, filled.</summary>
            public string RelationLine = string.Empty;
        }

        /// <summary>What stands at a location when the Ark arrives.</summary>
        public sealed class Arrival
        {
            public VoyageLocation Location;
            /// <summary>The artifact inside each vault shell (vault fields, hoards), in shell order.</summary>
            public readonly List<string> VaultFinds = new();
            /// <summary>The artifacts lying in a wreck here, spilled for anyone.</summary>
            public readonly List<string> WreckFinds = new();
            /// <summary>Seconds the Ark must hold here (cores, shrines, nurseries, derelicts; 0 otherwise).</summary>
            public float HoldSeconds;
            /// <summary>True when docking here takes everything aboard home.</summary>
            public bool Extracts;
            /// <summary>The place as it stands (DEPTH.md §2).</summary>
            public float Pressure;
            public int Bloom;
            public bool HasHolder;
            public Domains Holder;
            /// <summary>True when the world should start under a domain opposing the squad's (a lair, a pen, a core
            /// another domain holds, the Kaiju's lair): its fauna wear that colour and graze the hull.</summary>
            public bool HostileWorld;
            /// <summary>The destination world's population scale.</summary>
            public float PopulationScale = 1f;
            public bool KaijuHere;
            public int KaijuTier;
            /// <summary>The rival crews standing here.</summary>
            public readonly List<RivalMeeting> Rivals = new();
            /// <summary>True when the job's rival stands here (a bounty's work).</summary>
            public bool MetRival;
            /// <summary>True when arriving put this place on the chart.</summary>
            public bool NewlyCharted;
            /// <summary>The news aboard that reached this port's ledger on arrival.</summary>
            public readonly List<string> Delivered = new();
        }

        /// <summary>
        /// The Ark arrived at the job's destination. The location's experience is stood from this: the vault shells and
        /// what each holds, any wreck lying here, a core's hold, the rivals standing here. Arriving charts the place,
        /// delivers the news aboard at a port, and is written in the logbook.
        /// </summary>
        public static Arrival Arrive(VoyageState state, VoyageContent content)
        {
            if (state == null || !state.AtSea || state.Current == null) return null;
            state.AtSea = false;
            state.At = state.Current.To;
            var here = content.Location(state.At);
            if (IsPort(here)) state.LastPort = here.Id;
            var place = state.Place(state.At);
            place.Visits++;
            if (place.FirstVisitDay < 0) place.FirstVisitDay = state.Day;
            place.LastVisitDay = state.Day;
            bool newlyCharted = false;
            if (here != null && here.Uncharted && !place.Charted)
            {
                place.Charted = true;
                newlyCharted = true;
                state.Log.Charted++;
                Tell(state, Fill(content.Words?.Charted, here, here, here, null));
            }
            var arrival = Describe(state, content);
            if (arrival == null) return null;
            arrival.NewlyCharted = newlyCharted;
            if (IsPort(here) && state.Carrying.Count > 0)
            {
                var ledger = state.Ledger(here.Id);
                foreach (var n in state.Carrying)
                {
                    if (n == null || ledger.Knows(n.Tag, n.About) || n.About == here.Id) continue;
                    var copy = n.Copy();
                    copy.Day = state.Day;
                    ledger.News.Add(copy);
                    arrival.Delivered.Add(copy.Line);
                }
                state.Carrying.Clear();
            }
            return arrival;
        }

        /// <summary>What stands at the Ark's location now (also used to re-stand it).</summary>
        public static Arrival Describe(VoyageState state, VoyageContent content)
        {
            var here = content.Location(state.At);
            if (here == null) return null;
            VoyageWorld.Begin(state, content);
            var w = content.World ?? new VoyageWorldRules();
            var place = state.Place(here.Id);
            var squadDomain = SquadDomain(state, content);
            var arrival = new Arrival
            {
                Location = here,
                Extracts = state.At == state.Home,
                Pressure = place.Pressure,
                Bloom = place.Bloom,
                HasHolder = place.HasHolder,
                Holder = place.Holder,
                PopulationScale = PopulationAt(state, content, here.Id),
            };
            if (HasSealedFinds(here) && here.Finds is { Count: > 0 })
            {
                if (place.Sealed < 0 || (place.Sealed == 0 && place.RestockDay > 0 && state.Day >= place.RestockDay))
                {
                    place.Sealed = here.Vaults;
                    place.RestockDay = 0;
                }
                for (int i = 0; i < place.Sealed; i++)
                    arrival.VaultFinds.Add(here.Finds[Mod(Hash(state.Day, here.Id, i.ToString()), here.Finds.Count)]);
            }
            if (here.Experience is VoyageExperience.Core or VoyageExperience.Shrine or VoyageExperience.Nursery or VoyageExperience.Derelict)
                arrival.HoldSeconds = here.HoldSeconds;
            var wreck = state.WreckAt(here.Id);
            if (wreck != null) arrival.WreckFinds.AddRange(wreck.Artifacts);

            arrival.KaijuHere = content.Kaiju is { Enabled: true } && state.Kaiju is { Alive: true } && state.Kaiju.At == here.Id;
            arrival.KaijuTier = state.Kaiju?.Tier ?? 0;
            arrival.HostileWorld = HostileWorld(state, content, here.Id);

            foreach (var rs in VoyageRivals.StandingAt(state, content, here.Id))
            {
                var crew = content.Rival(rs.Id);
                if (crew == null) continue;
                var relation = VoyageRivals.Relation(state, content, crew, rs, here);
                var words = content.Words;
                string relationLine = relation switch
                {
                    RivalRelation.CoOp => words?.RelationCoOp,
                    RivalRelation.Race => words?.RelationRace,
                    RivalRelation.Hostile => words?.RelationHostile,
                    _ => string.Empty,
                };
                arrival.Rivals.Add(new RivalMeeting
                {
                    Crew = crew,
                    State = rs,
                    Relation = relation,
                    Line = Fill(crew.ArriveLine, here, here, here, null, crew.Name),
                    RelationLine = Fill(relationLine, here, here, here, null, crew.Name),
                });
                if (state.Current != null && state.Current.RivalId == crew.Id) arrival.MetRival = true;
            }
            return arrival;
        }

        /// <summary>True when a place's world starts under a domain opposing the squad's (DEPTH.md §2): a lair, a pen,
        /// a core another domain holds, or the Kaiju's lair. Its fauna then wear that colour and graze the hull.</summary>
        public static bool HostileWorld(VoyageState state, VoyageContent content, string locationId)
        {
            var here = content?.Location(locationId);
            if (here == null || state == null) return false;
            if (content.Kaiju is { Enabled: true } && state.Kaiju is { Alive: true } && state.Kaiju.At == here.Id) return true;
            if (here.Experience is VoyageExperience.Lair or VoyageExperience.Pen) return true;
            var place = state.PlaceIfAny(here.Id);
            return here.Experience == VoyageExperience.Core && place is { HasHolder: true } && place.Holder != SquadDomain(state, content);
        }

        /// <summary>True when the job under way is done by arriving (a courier with no hull floor, the way home).</summary>
        public static bool DoneOnArrival(VoyageJob job) =>
            job != null && job.Verb == WorkVerb.Arrive && job.HullFloor <= 0f;

        // ── Finds and the work ──────────────────────────────────────────────

        /// <summary>An artifact was stowed in the Ark here. A wreck's artifact leaves the wreck. A unique artifact (a
        /// relic) is never aboard twice.</summary>
        public static void Stowed(VoyageState state, string artifactId) => Stowed(state, null, artifactId);

        /// <summary>
        /// An artifact was stowed in the Ark here: a wreck's artifact leaves the wreck, a sealed find's shell is one
        /// fewer standing (and a picked field restocks later), and a rival racing for the same finds remembers it
        /// (DEPTH.md §3).
        /// </summary>
        public static void Stowed(VoyageState state, VoyageContent content, string artifactId)
        {
            if (state == null || string.IsNullOrEmpty(artifactId)) return;
            if (UniqueId.IsUnique(artifactId) && state.Aboard.Contains(artifactId)) return;
            state.Aboard.Add(artifactId);
            var wreck = state.WreckAt(state.At);
            if (wreck != null && wreck.Artifacts.Remove(artifactId))
            {
                if (wreck.Artifacts.Count == 0)
                {
                    state.Wrecks.Remove(wreck);
                    if (content != null) VoyageWorld.ForgetNews(state, NewsTag.Wreck, wreck.At);
                }
                if (content != null) VoyageRivals.OnPlayerStowed(state, content, state.At);
                return;
            }
            if (content == null) return;
            var here = content.Location(state.At);
            if (HasSealedFinds(here))
            {
                var place = state.Place(here.Id);
                if (place.Sealed < 0) place.Sealed = here.Vaults;
                if (place.Sealed > 0) place.Sealed--;
                if (place.Sealed == 0 && place.RestockDay <= 0)
                    place.RestockDay = state.Day + Math.Max(1, here.RestockDays > 0 ? here.RestockDays : content.World?.RestockDays ?? 4);
            }
            VoyageRivals.OnPlayerStowed(state, content, state.At);
        }

        /// <summary>What the world has counted at the stop: every verb's tally (DEPTH.md §4).</summary>
        public struct VoyageTally
        {
            /// <summary>Artifacts stowed at this stop.</summary>
            public int StowedHere;
            /// <summary>Seconds the pilot has stayed within the hold radius of the Ark here.</summary>
            public float HeldSeconds;
            /// <summary>Crystals the squad collected since arriving.</summary>
            public int Gathered;
            /// <summary>Hostile creatures felled near the Ark since arriving.</summary>
            public int Culled;
            /// <summary>Prisms the squad laid since arriving.</summary>
            public int Laid;
            /// <summary>Hostile volume the squad destroyed since arriving.</summary>
            public float Razed;
            /// <summary>Seconds the pilot has kept a living creature within reach.</summary>
            public float Observed;
            /// <summary>The Ark's hull as a share of full, on arrival.</summary>
            public float HullFraction;
            /// <summary>True when the job's rival stood here on arrival.</summary>
            public bool MetRival;
            /// <summary>Creatures killed by the squad since arriving (undoes a NoKill job's work).</summary>
            public int Kills;

            public static VoyageTally Of(int stowedHere, bool held) =>
                new() { StowedHere = stowedHere, HeldSeconds = held ? float.MaxValue : 0f, HullFraction = 1f };
        }

        /// <summary>True when the job's work is done here: a hunt or salvage once something was stowed at the
        /// destination (<paramref name="stowedHere"/>), a hold once it ran out, a courier on arrival.</summary>
        public static bool WorkDone(VoyageJob job, int stowedHere, bool held) =>
            WorkDone(job, VoyageTally.Of(stowedHere, held));

        /// <summary>True when the job's work is done here, by its verb and goal (DEPTH.md §4). One switch.</summary>
        public static bool WorkDone(VoyageJob job, in VoyageTally tally)
        {
            if (job == null) return false;
            if (job.NoKill && tally.Kills > 0) return false;
            float goal = job.Goal;
            bool done = job.Verb switch
            {
                WorkVerb.Arrive => tally.HullFraction >= job.HullFloor,
                WorkVerb.Stow => tally.StowedHere >= Math.Max(1, (int)Math.Ceiling(goal)),
                WorkVerb.Hold => tally.HeldSeconds >= goal,
                WorkVerb.Gather => tally.Gathered >= goal,
                WorkVerb.Cull => tally.Culled >= goal,
                WorkVerb.Lay => tally.Laid >= goal,
                WorkVerb.Raze => tally.Razed >= goal,
                WorkVerb.Observe => tally.Observed >= goal,
                WorkVerb.Meet => tally.MetRival,
                _ => false,
            };
            return done;
        }

        /// <summary>How far along the work is, 0 to 1, for a progress line.</summary>
        public static float Progress(VoyageJob job, in VoyageTally tally)
        {
            if (job == null) return 0f;
            float goal = Math.Max(1e-3f, job.Goal);
            float have = job.Verb switch
            {
                WorkVerb.Stow => tally.StowedHere,
                WorkVerb.Hold => tally.HeldSeconds,
                WorkVerb.Gather => tally.Gathered,
                WorkVerb.Cull => tally.Culled,
                WorkVerb.Lay => tally.Laid,
                WorkVerb.Raze => tally.Razed,
                WorkVerb.Observe => tally.Observed,
                WorkVerb.Meet => tally.MetRival ? goal : 0f,
                _ => goal,
            };
            return Math.Clamp(have / goal, 0f, 1f);
        }

        // ── Outcomes and the chain ──────────────────────────────────────────

        /// <summary>What a resolution wrote: the story line and the news, for the HUD to say.</summary>
        public sealed class Resolution
        {
            public JobOutcome Outcome;
            public string StoryLine = string.Empty;
            public readonly List<string> NewsLines = new();
            /// <summary>More of the story this outcome wrote (standing, a relic, a chapter, a fall).</summary>
            public readonly List<string> StoryLines = new();
            /// <summary>The line for the pilot's chronicle, and whose (empty when none).</summary>
            public string ChronicleLine = string.Empty;
            public string PilotId = string.Empty;
            /// <summary>A relic that came aboard, and its authored name and lore (empty when none).</summary>
            public string RelicId = string.Empty;
            public string RelicName = string.Empty;
            public string RelicLore = string.Empty;
        }

        /// <summary>
        /// The job under way ended with <paramref name="outcome"/>. A courier's dispatch reaches its destination's news;
        /// the template's event writes more news and moves the world (the chain); a success owes its reward at
        /// extraction, raises standing (and notoriety where the work is that kind), writes the pilot's chronicle, and
        /// opens the next chapter of a saga or grants its relic. The squad is between jobs afterwards, wherever the Ark is.
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
            var crew = content.Rival(job.RivalId);
            bool ok = outcome == JobOutcome.Succeeded;
            var w = content.World ?? new VoyageWorldRules();

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
                if (ok) state.AddOwed(t.RewardElement, job.Pay > 0 ? job.Pay : t.Reward);
                if (ok) state.Log.JobsDone++; else state.Log.JobsFailed++;

                // Standing and notoriety (DEPTH.md §6).
                if (t.Faction != FactionType.None && t.StandingDelta != 0)
                {
                    int before = Tier(state, content, t.Faction);
                    state.AddStanding(t.Faction, ok ? t.StandingDelta : -t.StandingDelta);
                    int after = Tier(state, content, t.Faction);
                    if (after > before)
                        Say(state, result, Tokens(content.Words?.StandingUp, "faction", FactionCatalog.TypeName(t.Faction),
                            "tier", TierName(content, after)));
                }
                if (ok && t.NotorietyDelta != 0)
                {
                    int before = state.Notoriety;
                    state.Notoriety = Math.Max(0, state.Notoriety + t.NotorietyDelta);
                    if (before < w.BountyAt && state.Notoriety >= w.BountyAt) Say(state, result, content.Words?.Wanted);
                }

                var ev = content.Event(ok ? t.OnSuccess : t.OnFail);
                if (ev != null) Apply(state, content, ev, job, result);
                if (!ok && to != null && (t.Verb == WorkVerb.Hold || t.Verb == WorkVerb.Raze || t.Verb == WorkVerb.Lay))
                    Pressure(state, to.Id, w.FailurePressure);

                if (ok)
                {
                    switch (job.Kind)
                    {
                        case JobKind.Lift:
                            Lift(state, content, to, result);
                            break;
                        case JobKind.Bounty:
                            if (crew != null) VoyageRivals.Catch(state, content, crew, job.To, result);
                            break;
                    }
                    if (job.Source != null && job.Source.Tag == NewsTag.Kaiju) VoyageWorld.KaijuFelled(state, content, result);
                    if (!string.IsNullOrEmpty(t.ChronicleLine))
                    {
                        result.ChronicleLine = Fill(t.ChronicleLine, from, to, to, person, crew?.Name);
                        result.PilotId = job.PilotId;
                    }
                }

                result.StoryLine = Fill(PickLine(ok ? t.SuccessLines : t.FailLines, ok ? t.SuccessLine : t.FailLine, state.Day, job.To, t.Id),
                    from, to, to, person, crew?.Name);
                Tell(state, result.StoryLine);
            }

            if (!string.IsNullOrEmpty(job.Chapter)) Chapter(state, content, job, ok, result);
            return result;
        }

        /// <summary>A thief's lift: the destination's freshest news comes aboard, to be sold at the next port.</summary>
        static void Lift(VoyageState state, VoyageContent content, VoyageLocation port, Resolution result)
        {
            if (port == null || !IsPort(port)) return;
            var ledger = state.Ledger(port.Id);
            NewsItem best = null;
            foreach (var n in ledger.News)
            {
                if (n == null || n.Tag == NewsTag.Chapter || n.Rival == SelfRival || n.About == port.Id) continue;
                if (best == null || n.Day > best.Day) best = n;
            }
            if (best == null) return;
            ledger.News.Remove(best);
            var lifted = best.Copy();
            lifted.Day = state.Day;
            state.Carrying.Add(lifted);
            result.NewsLines.Add(lifted.Line);
        }

        /// <summary>A saga chapter resolved: done opens the next (posted where the squad is), the finale grants the relic.</summary>
        static void Chapter(VoyageState state, VoyageContent content, VoyageJob job, bool ok, Resolution result)
        {
            var chapter = content.Chapter(job.Chapter, out var saga);
            if (chapter == null || saga == null) return;
            if (!ok) return;   // the chapter's news stays posted: the saga can be tried again
            if (!state.ChaptersDone.Contains(chapter.Id)) state.ChaptersDone.Add(chapter.Id);
            state.ChaptersOpen.Remove(chapter.Id);
            VoyageWorld.ForgetChapter(state, chapter.Id);
            var to = content.Location(job.To);
            string relicName = saga.Relic?.Name;
            Say(state, result, Tokens(Fill(chapter.DoneLine, content.Location(job.From), to, to, content.Person(job.PilotId)),
                "relic", relicName ?? string.Empty, "saga", saga.Name ?? string.Empty));
            if (!string.IsNullOrEmpty(chapter.Opens))
            {
                var next = content.Chapter(chapter.Opens, out _);
                if (next != null) VoyageWorld.PostChapter(state, content, saga, next, NearestPort(content, job.To)?.Id ?? state.Home);
                return;
            }
            // The finale: the relic comes aboard, once per galaxy.
            if (state.RelicsFound.Contains(saga.Id)) return;
            string relicId = RelicId(saga);
            if (string.IsNullOrEmpty(relicId)) return;
            state.RelicsFound.Add(saga.Id);
            if (!state.Aboard.Contains(relicId)) state.Aboard.Add(relicId);
            state.Log.Relics++;
            result.RelicId = relicId;
            result.RelicName = relicName ?? string.Empty;
            result.RelicLore = saga.Relic?.Lore ?? string.Empty;
            Say(state, result, Tokens(content.Words?.RelicFound, "relic", relicName ?? string.Empty));
        }

        /// <summary>A relic's unique artifact id: its kind, and a seed hashed from its authored word, the same in every
        /// galaxy (Docs/Pilots/INVENTORY.md §1: template#16 hex). Empty when the saga has no relic.</summary>
        public static string RelicId(Saga saga)
        {
            if (saga?.Relic == null || string.IsNullOrEmpty(saga.Relic.Kind)) return string.Empty;
            ulong seed = Hash64(string.IsNullOrEmpty(saga.Relic.Seed) ? saga.Id : saga.Relic.Seed);
            return saga.Relic.Kind + UniqueId.Separator + seed.ToString("x16");
        }

        /// <summary>The saga whose relic this artifact id is, or null.</summary>
        public static Saga RelicSaga(VoyageContent content, string artifactId)
        {
            if (content?.Sagas == null || string.IsNullOrEmpty(artifactId)) return null;
            foreach (var s in content.Sagas)
                if (s != null && RelicId(s) == artifactId) return s;
            return null;
        }

        internal static void Apply(VoyageState state, VoyageContent content, VoyageEvent ev, VoyageJob job, Resolution result)
        {
            var dest = content.Location(job.To);
            if (ev.SpendsNews && job.Source != null && job.Source.Tag != NewsTag.Chapter)
                VoyageWorld.ForgetNews(state, job.Source.Tag, job.Source.About);
            var person = content.Person(job.PilotId);
            var crew = content.Rival(job.RivalId);
            // A rival crew's job (its id in the pilot's seat) acts for its own domain and its own chart.
            var actor = content.Rival(job.PilotId);
            var actorDomain = actor != null ? actor.Domain : SquadDomain(state, content);

            foreach (var wr in ev.Writes ?? new List<NewsWrite>())
            {
                if (wr == null) continue;
                // What the write does to the world (DEPTH.md §2).
                if (dest != null)
                {
                    if (wr.PressureDelta != 0f) Pressure(state, dest.Id, wr.PressureDelta);
                    if (wr.BloomDelta != 0)
                    {
                        var place = state.Place(dest.Id);
                        place.Bloom = Math.Clamp(place.Bloom + wr.BloomDelta, 0, 3);
                    }
                    if (wr.HolderToSquad)
                    {
                        var place = state.Place(dest.Id);
                        place.HasHolder = true;
                        place.Holder = actorDomain;
                        if (actor == null) VoyageWorld.ForgetNews(state, NewsTag.Held, dest.Id);
                    }
                    if (wr.ChartsDestination && dest.Uncharted)
                    {
                        var place = state.Place(dest.Id);
                        if (actor != null) place.KnownToOthers = true;
                        else if (!place.Charted)
                        {
                            place.Charted = true;
                            state.Log.Charted++;
                            Say(state, result, Fill(content.Words?.Charted, dest, dest, dest, null));
                        }
                    }
                }
                if (wr.RivalGrudgeDelta != 0 && crew != null)
                {
                    var rs = state.Rival(crew.Id);
                    if (rs != null) rs.Grudge = Math.Clamp(rs.Grudge + wr.RivalGrudgeDelta, 0, 3);
                }
                if (wr.Tag == NewsTag.None) continue;

                var about = AboutFor(state, content, wr, job);
                if (about == null) continue;
                var line = Fill(PickLine(wr.Lines, wr.Line, state.Day, job.To, about.Id), content.Location(job.From), dest, about, person, crew?.Name);
                foreach (var port in PortsFor(state, content, wr.At, job))
                {
                    var ledger = state.Ledger(port.Id);
                    if (ledger.Knows(wr.Tag, about.Id)) continue;
                    ledger.News.Add(new NewsItem { Tag = wr.Tag, About = about.Id, Line = line, Day = state.Day, Rival = crew?.Id ?? string.Empty });
                    if (result != null && !result.NewsLines.Contains(line)) result.NewsLines.Add(line);
                }
            }
        }

        static void Pressure(VoyageState state, string locationId, float delta)
        {
            var place = state.Place(locationId);
            place.Pressure = Math.Clamp(place.Pressure + delta, 0f, 3f);
        }

        static IEnumerable<VoyageLocation> PortsFor(VoyageState state, VoyageContent content, NewsPlace at, VoyageJob job)
        {
            switch (at)
            {
                case NewsPlace.Origin:
                    var origin = NearestPort(content, job.From);
                    if (origin != null) yield return origin;
                    break;
                case NewsPlace.Home:
                    var home = content.Location(state.Home);
                    if (home != null) yield return home;
                    break;
                case NewsPlace.EveryPort:
                    foreach (var l in content.Locations)
                        if (IsPort(l)) yield return l;
                    break;
                default:
                    var dest = NearestPort(content, job.To);
                    if (dest != null) yield return dest;
                    break;
            }
        }

        static VoyageLocation AboutFor(VoyageState state, VoyageContent content, NewsWrite wr, VoyageJob job) => wr.About switch
        {
            NewsAbout.AnotherVaultField => NearestOf(content, job.To, VoyageExperience.VaultField, job.To),
            NewsAbout.AnotherCore => NearestOf(content, job.To, VoyageExperience.Core, job.To),
            NewsAbout.AnotherOf => NearestOf(content, job.To, wr.AboutExperience, job.To),
            NewsAbout.NearestUncharted => NearestUncharted(state, content, job.To),
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

        /// <summary>The nearest place this crew has not charted (a leaf off the charted lanes), or null.</summary>
        public static VoyageLocation NearestUncharted(VoyageState state, VoyageContent content, string from)
        {
            var dist = Distances(content, from);
            VoyageLocation best = null;
            int bestHops = int.MaxValue;
            foreach (var l in content.Locations)
            {
                if (l == null || !l.Uncharted || IsVisible(state, content, l.Id)) continue;
                if (dist.TryGetValue(l.Id, out int hops) && hops < bestHops) { best = l; bestHops = hops; }
            }
            return best;
        }

        // ── The Ark escort on the chart ─────────────────────────────────────

        /// <summary>
        /// Where an Ark escort sails (Docs/Voyages/ARK_ESCORT.md §5): from where the Ark lies to the nearest place it can
        /// berth that is not here, by lanes - a port, or the squad's own harbour, never a rival domain's (VOYAGES.md §6.7
        /// rule 2). Ties go to the location listed first. The route, both ends included, as <see cref="Route"/> gives it;
        /// null at sea, or when nothing qualifies, so an uncharted escort still sails its worlds without a destination.
        /// </summary>
        public static List<string> EscortRoute(VoyageState state, VoyageContent content)
        {
            if (state == null || content?.Locations == null || state.AtSea) return null;
            var here = content.Location(state.At) ?? content.Location(state.Home);
            if (here == null) return null;
            var dist = Distances(content, here.Id);
            VoyageLocation best = null;
            int bestHops = int.MaxValue;
            foreach (var l in content.Locations)
            {
                if (l == null || l.Id == here.Id || !IsEscortBerth(l, state)) continue;
                if (dist.TryGetValue(l.Id, out int hops) && hops < bestHops) { best = l; bestHops = hops; }
            }
            return best == null ? null : Route(content, here.Id, best.Id);
        }

        static bool IsEscortBerth(VoyageLocation l, VoyageState state) =>
            l.Experience == VoyageExperience.Port || (l.Experience == VoyageExperience.Harbour && l.Id == state.Home);

        /// <summary>
        /// An escort brought the Ark in at <paramref name="to"/>: the Ark lies there now, the crew reads that port's news,
        /// and a day passes (news ages, the galaxy ticks, the next board is drawn afresh). False, and nothing changes, at
        /// sea, with a job out, or for a place the content does not know. An escort that is overrun moves nothing: the Ark
        /// limps back to where it set out.
        /// </summary>
        public static bool EscortArrived(VoyageState state, VoyageContent content, string to)
        {
            if (state == null || content == null || state.AtSea || state.HasJob()) return false;
            var l = content.Location(to);
            if (l == null) return false;
            state.At = l.Id;
            if (IsPort(l)) state.LastPort = l.Id;
            state.Day++;
            VoyageWorld.Tick(state, content);
            return true;
        }

        internal static VoyageLocation NearestOf(VoyageContent content, string from, VoyageExperience kind, string notThis)
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
        /// voyage is lost: to the raiders when <paramref name="taken"/> (a hostile rival crew standing there is credited
        /// and promoted, DEPTH.md §3), otherwise into a wreck lying there, which is news at the nearest port, so another
        /// squad (or this one, next time) can salvage it. The squad's job fails and it limps home with nothing aboard.
        /// </summary>
        public static Resolution ArkLost(VoyageState state, VoyageContent content, string where, bool taken,
            IReadOnlyList<string> spilled)
        {
            string wreckLine = content.WreckLine, newsLine = content.WreckNewsLine;
            var result = state.Current != null
                ? Resolve(state, content, JobOutcome.ArkLost)
                : new Resolution { Outcome = JobOutcome.ArkLost };
            var at = content.Location(where);
            state.Log.ArksLost++;
            bool credited = taken && at != null && VoyageRivals.CreditFall(state, content, at.Id, spilled, result);
            if (!taken && !credited && at != null && spilled is { Count: > 0 })
            {
                var wreck = state.WreckAt(at.Id);
                if (wreck == null)
                {
                    wreck = new WreckRecord { At = at.Id, Line = Fill(wreckLine, at, at, at, null), Day = state.Day };
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
            state.Carrying.Clear();
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
            state.Carrying.Clear();
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
        public static void Resume(VoyageState state, VoyageContent content, Domains home) => Resume(state, content, home, 0);

        /// <summary>
        /// <see cref="Resume(VoyageState, VoyageContent, Domains)"/>, and the days away: with <paramref name="nowMs"/>
        /// and a saved time, each day since the record was saved ticks the galaxy once (DEPTH.md §2), up to the world's
        /// most.
        /// </summary>
        public static void Resume(VoyageState state, VoyageContent content, Domains home, long nowMs)
        {
            state.Normalize();
            if (string.IsNullOrEmpty(state.Home) || content.Location(state.Home) == null)
            {
                Embark(state, content, home);
                return;
            }
            // A record from before the galaxy lived (DEPTH.md) has days nobody ticked: they are not ticked now.
            if (state.TickedDay == 0 && state.Day > 0 && state.Places.Count == 0) state.TickedDay = state.Day;
            VoyageWorld.Begin(state, content);
            if (state.AtSea)
            {
                Abandon(state, content);
            }
            else
            {
                if (state.Current != null) Resolve(state, content, JobOutcome.Failed);
                if (content.Location(state.At) == null)
                {
                    state.At = state.Home;
                    state.LastPort = state.Home;
                }
            }
            if (nowMs > 0 && state.SavedAtMs > 0 && nowMs > state.SavedAtMs)
            {
                int days = (int)Math.Min(int.MaxValue, (nowMs - state.SavedAtMs) / 86_400_000L);
                int most = Math.Max(0, content.World?.MaxTicksAway ?? 5);
                int ticks = Math.Min(days, most);
                if (ticks > 0) VoyageWorld.TickDays(state, content, ticks);
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
            /// <summary>The relics among the artifacts: their ids with the authored name and lore to give the record.</summary>
            public readonly List<(string Id, string Name, string Lore)> Relics = new();
        }

        /// <summary>
        /// The Ark is docked at home: everything aboard and every crystal owed is the player's now. Null away from
        /// home or at sea. Idempotent: a second call hands over nothing.
        /// </summary>
        public static Extraction Extract(VoyageState state) => Extract(state, null);

        /// <summary><see cref="Extract(VoyageState)"/>, naming the relics among the finds from <paramref name="content"/>.</summary>
        public static Extraction Extract(VoyageState state, VoyageContent content)
        {
            if (state == null || !state.IsHome()) return null;
            var x = new Extraction();
            x.Artifacts.AddRange(state.Aboard);
            for (int i = 0; i < x.Crystals.Length; i++) x.Crystals[i] = state.OwedOf((Element)i);
            foreach (var id in x.Artifacts)
            {
                var saga = RelicSaga(content, id);
                if (saga != null) x.Relics.Add((id, saga.Relic.Name ?? string.Empty, saga.Relic.Lore ?? string.Empty));
            }
            state.Log.Extracted += x.Artifacts.Count;
            int crystals = 0;
            foreach (var c in x.Crystals) crystals += c;
            state.Log.Crystals += crystals;
            state.Aboard.Clear();
            state.Owed.Clear();
            return x;
        }

        // ── Small helpers ───────────────────────────────────────────────────

        /// <summary>The story keeps its last lines; older ones are let go so the record stays small.</summary>
        public const int StoryLength = 40;

        internal static void Tell(VoyageState state, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            state.Story.Add(new StoryEntry { Day = state.Day, Line = line });
            if (state.Story.Count > StoryLength) state.Story.RemoveRange(0, state.Story.Count - StoryLength);
        }

        /// <summary>A line of the story that a resolution also reports.</summary>
        internal static void Say(VoyageState state, Resolution result, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Tell(state, line);
            result?.StoryLines.Add(line);
        }

        internal static int Mod(int x, int m) => m <= 0 ? 0 : ((x % m) + m) % m;

        /// <summary>A stable hash (FNV-1a): string.GetHashCode is randomised per process, and a board must read the
        /// same on every machine and every run.</summary>
        public static int Hash(int day, string a, string b)
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

        /// <summary>A stable roll in [0, 1) from the day and two words.</summary>
        public static double Roll(int day, string a, string b) => Hash(day, a, b) / 2147483648.0;

        /// <summary>A stable 64-bit hash (FNV-1a) of a word: a relic's seed.</summary>
        public static ulong Hash64(string s)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                if (s != null)
                    foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
                return h;
            }
        }
    }
}
