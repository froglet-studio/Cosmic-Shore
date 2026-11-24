using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// Crystal Run, the Milestone 1 crew voyage (Docs/Voyages/CRYSTAL_RUN.md; Shombith, 2026-11-16): steal a goal of
    /// elemental crystals between the three of you while the Ark sails a leg of a fixed number of cells, some hostile and
    /// some calm, and bring them home. Pure rules, like <see cref="VoyageRules"/>: the board's always-offered work, the
    /// leg fitted to its cells, what was stolen and by whom, what is owed at the stop, and the next run when the squad
    /// ventures further. <c>VoyageRun</c> asks; these answer, so every rule runs in an edit-mode test.
    /// </summary>
    public static class VoyageRaid
    {
        /// <summary>The four elements a stolen crystal can be, in the order the HUD and the scoreboard list them.</summary>
        public static readonly Element[] Elements = { Element.Space, Element.Time, Element.Charge, Element.Mass };

        /// <summary>True when <paramref name="element"/> is one a Crystal Run counts (any elemental crystal does; an
        /// omni crystal does not).</summary>
        public static bool Counts(Element element) =>
            element is Element.Charge or Element.Mass or Element.Space or Element.Time;

        // ── The board ───────────────────────────────────────────────────────

        /// <summary>
        /// The work every board offers here whoever is seated (<see cref="JobTemplate.Always"/>), one job per template,
        /// credited to the first seated pilot the crew knows (any pilot can bring it). A template with nowhere to go
        /// from here offers nothing.
        /// </summary>
        public static List<VoyageJob> AlwaysOffered(VoyageState state, VoyageContent content, VoyageLocation here,
            IReadOnlyList<string> seatedPilotIds, List<string> usedTo)
        {
            var jobs = new List<VoyageJob>();
            if (state == null || content?.Jobs == null || here == null) return jobs;
            var person = Bringer(content, seatedPilotIds);
            foreach (var t in content.Jobs)
            {
                if (t == null || t.Id == VoyageRules.ReturnJobId) continue;
                // A built voyage type is offered as its named voyages wherever it sails from (Milestone 2), even when its
                // template is otherwise brought only by news (Hold the Core: job.defence.aid).
                bool onBoard = content.VoyageType(t.Id) is { OnBoard: true } && HasNamed(content, t.Id);
                if (!t.Always && !onBoard) continue;
                // A type with named voyages is offered only as one of those (Milestone 2): its own title names the type,
                // and one a port keeps the board near six options; the day picks which sails from here.
                var job = HasNamed(content, t.Id)
                    ? NamedOfTheDay(state, content, t.Id, here, person)
                    : Draw(state, content, t, here, person, usedTo ?? new List<string>(), 0);
                if (job != null) jobs.Add(job);
            }
            return jobs;
        }

        /// <summary>True when <paramref name="content"/> names at least one voyage of type <paramref name="type"/>.</summary>
        public static bool HasNamed(VoyageContent content, string type)
        {
            if (content?.Voyages == null || string.IsNullOrEmpty(type)) return false;
            foreach (var v in content.Voyages)
                if (v != null && v.Type == type) return true;
            return false;
        }

        /// <summary>
        /// The job that sails named voyage <paramref name="v"/> from <paramref name="here"/> (Docs/Voyages/MILESTONE_2.md
        /// §2): its type's rules, the voyage's own name as the title, its difficulty (1-9) and its cells as the leg.
        /// Null when the voyage starts somewhere else, ends here, is the escort card's (not a job), or its place is unknown.
        /// </summary>
        public static VoyageJob DrawNamed(VoyageState state, VoyageContent content, NamedVoyage v, VoyageLocation here,
            CrewPerson person)
        {
            if (state == null || content == null || v == null || here == null || v.Type == NamedVoyage.EscortType) return null;
            if (!string.IsNullOrEmpty(v.From) && v.From != here.Id) return null;
            if (string.IsNullOrEmpty(v.To) || v.To == here.Id || content.Location(v.To) == null) return null;
            var t = content.Job(v.Type);
            if (t == null) return null;
            var job = VoyageRules.Make(state, content, t, person ?? new CrewPerson { PilotId = string.Empty, Name = string.Empty },
                here, v.To, null, null);
            Name(job, v, t);
            return job;
        }

        /// <summary>Give <paramref name="job"/> (made from <paramref name="t"/>) named voyage <paramref name="v"/>'s
        /// title, difficulty, cells and goal.</summary>
        static void Name(VoyageJob job, NamedVoyage v, JobTemplate t)
        {
            job.VoyageId = v.Id;
            if (!string.IsNullOrEmpty(v.Rival)) job.RivalId = v.Rival;   // a Bounty hunts its named crew
            if (!string.IsNullOrEmpty(v.Name)) job.Title = v.Name;
            if (!string.IsNullOrEmpty(v.Line)) job.Summary = v.Line;
            job.Difficulty = VoyageDifficulty.Clamp(v.Difficulty);
            job.Intensity = VoyageDifficulty.IntensityOf(job.Difficulty);   // the board's pricing follows the difficulty
            job.Cells = VoyageSea.RunCells(v.Cells);
            if (t.GoalPerCell > 0f) job.Goal = t.GoalPerCell * job.Cells;
        }

        /// <summary>The one named voyage of <paramref name="type"/> a port offers today: of those that sail from
        /// <paramref name="here"/>, the one the day picks (seeded, so every machine in a party sees the same board).</summary>
        static VoyageJob NamedOfTheDay(VoyageState state, VoyageContent content, string type, VoyageLocation here,
            CrewPerson person)
        {
            var open = new List<VoyageJob>();
            foreach (var v in content.Voyages ?? new List<NamedVoyage>())
            {
                if (v == null || v.Type != type) continue;
                var job = DrawNamed(state, content, v, here, person);
                if (job != null) open.Add(job);
            }
            if (open.Count == 0) return null;
            int pick = VoyageRules.Mod(VoyageRules.Hash(state.Day, type, here.Id), open.Count);
            return open[pick];
        }

        /// <summary>The next named voyage of <paramref name="type"/> after <paramref name="doneId"/> that sails from
        /// <paramref name="here"/>, in the content's order (wrapping), skipping <paramref name="not"/>. Null when none.</summary>
        static VoyageJob NextNamed(VoyageState state, VoyageContent content, string type, string doneId, VoyageLocation here,
            CrewPerson person, string not, bool includeDone)
        {
            var list = content.Voyages ?? new List<NamedVoyage>();
            int start = list.FindIndex(x => x != null && x.Id == doneId);
            for (int k = includeDone ? 0 : 1; k <= list.Count; k++)
            {
                var v = list[(Math.Max(0, start) + k) % Math.Max(1, list.Count)];
                if (v == null || v.Type != type || v.To == not) continue;
                if (!includeDone && v.Id == doneId) continue;
                var job = DrawNamed(state, content, v, here, person);
                if (job != null) return job;
            }
            return null;
        }

        /// <summary>The crew person a seated pilot is (the first the crew knows), else a stand-in carrying the first
        /// seated pilot's id, so the job still has someone to credit.</summary>
        static CrewPerson Bringer(VoyageContent content, IReadOnlyList<string> seatedPilotIds)
        {
            string first = null;
            if (seatedPilotIds != null)
                foreach (var id in seatedPilotIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    first ??= id;
                    var person = content.Person(id);
                    if (person != null) return person;
                }
            return new CrewPerson { PilotId = first ?? string.Empty, Name = string.Empty, Faction = FactionType.None };
        }

        static VoyageJob Draw(VoyageState state, VoyageContent content, JobTemplate t, VoyageLocation here, CrewPerson person,
            List<string> usedTo, int venture)
        {
            var to = VoyageRules.StandingDestination(state, content, t, here, person, usedTo, forRival: false);
            if (to == null) return null;
            var job = VoyageRules.Make(state, content, t, person, here, to.Id, null, null);
            job.Venture = Math.Max(0, venture);
            job.Goal = GoalFor(t, job.Venture);
            job.Cells = CellsFor(t, job.Venture);
            return job;
        }

        /// <summary>The goal of the <paramref name="venture"/>-th further run (0 is the first).</summary>
        public static float GoalFor(JobTemplate t, int venture) =>
            t == null ? 0f : t.Goal + Math.Max(0f, t.VentureGoal) * Math.Max(0, venture);

        /// <summary>The cells of the <paramref name="venture"/>-th further run (0: the lanes decide).</summary>
        public static int CellsFor(JobTemplate t, int venture) =>
            t == null || t.Cells <= 0 ? 0 : t.Cells + Math.Max(0, t.VentureCells) * Math.Max(0, venture);

        // ── Venture further ─────────────────────────────────────────────────

        /// <summary>
        /// Venture further after <paramref name="done"/> (resolved at the stop the squad is at): the same work from here
        /// to the next place of its kind, with the goal raised and one more cell (the template's
        /// <see cref="JobTemplate.VentureGoal"/> and <see cref="JobTemplate.VentureCells"/>). What was stolen stays owed
        /// and at stake. Null when the job is not one that ventures, the squad is at sea, or there is nowhere further.
        /// </summary>
        public static VoyageJob Venture(VoyageState state, VoyageContent content, VoyageJob done)
        {
            if (state == null || content == null || done == null || state.AtSea || done.IsReturn) return null;
            var t = content.Job(done.TemplateId);
            var here = content.Location(state.At);
            if (t == null || here == null) return null;
            var person = content.Person(done.PilotId)
                         ?? new CrewPerson { PilotId = done.PilotId ?? string.Empty, Name = string.Empty, Faction = t.Faction };
            // A named voyage ventures on to the next voyage of its type that sails from here (never back where it set out).
            if (done.IsNamed)
                return NextNamed(state, content, done.TemplateId, done.VoyageId, here, person, done.From, includeDone: false);
            // Never straight back to where this run set out from, while anywhere else will do.
            var usedTo = new List<string>();
            if (!string.IsNullOrEmpty(done.From)) usedTo.Add(done.From);
            return Draw(state, content, t, here, person, usedTo, done.Venture + 1)
                   ?? Draw(state, content, t, here, person, new List<string>(), done.Venture + 1);
        }

        /// <summary>
        /// Try again after a missed goal: the same work at the same size (its goal and cells, <see cref="VoyageJob.Venture"/>
        /// unchanged) from where the run stopped, to the next place of its kind. Null when there is none, at sea, or for a
        /// way home.
        /// </summary>
        public static VoyageJob Retry(VoyageState state, VoyageContent content, VoyageJob missed)
        {
            if (state == null || content == null || missed == null || state.AtSea || missed.IsReturn) return null;
            var t = content.Job(missed.TemplateId);
            var here = content.Location(state.At);
            if (t == null || here == null) return null;
            var person = content.Person(missed.PilotId)
                         ?? new CrewPerson { PilotId = missed.PilotId ?? string.Empty, Name = string.Empty, Faction = t.Faction };
            // A named voyage tries again as the same voyage when it sails from here, else the next of its type.
            if (missed.IsNamed)
                return NextNamed(state, content, missed.TemplateId, missed.VoyageId, here, person, null, includeDone: true);
            var usedTo = new List<string>();
            if (!string.IsNullOrEmpty(missed.From)) usedTo.Add(missed.From);
            return Draw(state, content, t, here, person, usedTo, missed.Venture)
                   ?? Draw(state, content, t, here, person, new List<string>(), missed.Venture);
        }

        // ── The leg ─────────────────────────────────────────────────────────

        /// <summary>
        /// A corridor (<see cref="VoyageRules.Corridor(VoyageContent,string,string,int,int,VoyageState)"/>) fitted to
        /// exactly <paramref name="count"/> cells, the destination's own world kept last: a longer one loses its first
        /// open-sea cells, a shorter one gains open-sea cells at its start, drawn from the pool by the day and
        /// <paramref name="key"/> (never the cell beside it). Unchanged when <paramref name="count"/> is 0 or the corridor
        /// is empty.
        /// </summary>
        public static List<int> FitCorridor(List<int> cells, int count, int poolSize, int day, string key)
        {
            if (cells == null || cells.Count == 0 || count <= 0) return cells;
            var fitted = new List<int>(cells);
            if (fitted.Count > count) fitted.RemoveRange(0, fitted.Count - count);
            for (int k = 0; fitted.Count < count; k++)
            {
                int pick = VoyageRules.Mod(VoyageRules.Hash(day, key ?? string.Empty, "fit" + k), Math.Max(1, poolSize));
                if (poolSize > 1 && pick == fitted[0]) pick = (pick + 1) % poolSize;
                fitted.Insert(0, pick);
            }
            return fitted;
        }

        /// <summary>
        /// The leg's named worlds (<see cref="JobTemplate.CellWorlds"/>) laid over a fitted corridor: cell i stands the
        /// pool world called <c>named[i]</c>. A name the pool does not have, an empty name, and every cell past the list
        /// keep the world already there. <paramref name="pool"/> is the corridor's worlds in route-index order.
        /// </summary>
        public static List<int> NameCells(List<int> cells, IReadOnlyList<string> named, IReadOnlyList<string> pool)
        {
            if (cells == null || named == null || named.Count == 0 || pool == null || pool.Count == 0) return cells;
            var named0 = new List<int>(cells);
            for (int i = 0; i < named0.Count && i < named.Count; i++)
            {
                if (string.IsNullOrEmpty(named[i])) continue;
                for (int w = 0; w < pool.Count; w++)
                    if (string.Equals(pool[w], named[i], StringComparison.OrdinalIgnoreCase)) { named0[i] = w; break; }
            }
            return named0;
        }

        /// <summary>Cell <paramref name="index"/>'s entry of a per-cell list (cells past the end take the last entry), or
        /// <paramref name="fallback"/> when the list is empty or the index is below 0.</summary>
        public static float PerCell(IReadOnlyList<float> values, int index, float fallback) =>
            values == null || values.Count == 0 || index < 0 ? fallback : values[Math.Min(index, values.Count - 1)];

        /// <summary>A fitted pacing with the leg's own life (<see cref="JobTemplate.CellLife"/>) over it: cell i takes
        /// <c>life[i]</c>; cells past the list keep the lane's pacing.</summary>
        public static List<float> LayCellLife(List<float> scales, IReadOnlyList<float> life)
        {
            if (scales == null || life == null || life.Count == 0) return scales;
            var laid = new List<float>(scales);
            for (int i = 0; i < laid.Count && i < life.Count; i++)
                if (life[i] > 0f) laid[i] = life[i];
            return laid;
        }

        /// <summary>A corridor's pacing (<see cref="VoyageRules.CorridorProfile"/>) fitted the way
        /// <see cref="FitCorridor"/> fits its cells: trimmed from the start, padded at the start with the first scale.</summary>
        public static List<float> FitProfile(List<float> scales, int count)
        {
            if (scales == null || count <= 0) return scales;
            var fitted = new List<float>(scales);
            if (fitted.Count > count) fitted.RemoveRange(0, fitted.Count - count);
            float pad = fitted.Count > 0 ? fitted[0] : 1f;
            while (fitted.Count < count) fitted.Insert(0, pad);
            return fitted;
        }

        // ── The scoreboard ──────────────────────────────────────────────────

        /// <summary>A seat's score on the end-of-voyage board: each crystal, kill and creature felled near the Ark worth
        /// its authored weight (VoyageToyDefinitionSO). Never below 0.</summary>
        public static int Score(int crystals, int kills, int guarded, int perCrystal, int perKill, int perGuard) =>
            Math.Max(0, crystals * perCrystal + kills * perKill + guarded * perGuard);

        // ── The stop ────────────────────────────────────────────────────────

        /// <summary>
        /// The run reached its stop: the stolen crystals are owed (at stake until the Ark docks home, lost with it), and
        /// the job resolves, succeeded when the goal was met (its pay owed too) and failed when not, paying only what was
        /// stolen. Null when no Crystal Run is under way.
        /// </summary>
        public static VoyageRules.Resolution Arrive(VoyageState state, VoyageContent content, VoyageRaidTally tally) =>
            Arrive(state, content, tally, forfeitOnMiss: false);

        /// <summary>As <see cref="Arrive(VoyageState, VoyageContent, VoyageRaidTally)"/>; with
        /// <paramref name="forfeitOnMiss"/>, a run short of its goal owes nothing of its haul (Shombith 2026-11-17: missing the
        /// goal has to mean something). What earlier legs owe is untouched.</summary>
        public static VoyageRules.Resolution Arrive(VoyageState state, VoyageContent content, VoyageRaidTally tally,
            bool forfeitOnMiss)
        {
            var job = state?.Current;
            if (job == null || !job.CountsUnderway) return null;
            var ok = tally != null && tally.Total >= job.Goal;
            if (ok || !forfeitOnMiss) Owe(state, tally);
            return VoyageRules.Resolve(state, content, ok ? JobOutcome.Succeeded : JobOutcome.Failed);
        }

        /// <summary><paramref name="amount"/> crystals of <paramref name="element"/> are owed: at stake until the Ark docks
        /// home (the escort's pay, Docs/Voyages/CRYSTAL_RUN.md §5).</summary>
        public static void Owe(VoyageState state, Element element, int amount) => state?.AddOwed(element, amount);

        /// <summary>Every crystal in <paramref name="tally"/> is owed, by its element.</summary>
        public static void Owe(VoyageState state, VoyageRaidTally tally)
        {
            if (state == null || tally == null) return;
            foreach (var e in Elements)
            {
                int n = tally.Of(e);
                if (n > 0) state.AddOwed(e, n);
            }
        }
    }

    /// <summary>What a Crystal Run stole, by seat and by element, counted from the moment the Ark set sail.</summary>
    [Serializable]
    public sealed class VoyageRaidTally
    {
        /// <summary>One seat's take.</summary>
        [Serializable]
        public sealed class Seat
        {
            public string PlayerName;
            /// <summary>The seat's vessel class (an int: <c>VesselClassType</c>), for a party friend's scoreboard row.</summary>
            public int Vessel;
            /// <summary>Crystals by element (index = (int)Element).</summary>
            public int[] Crystals = new int[(int)Element.Omni + 1];
            /// <summary>Crystals taken but not banked yet, by element (index = (int)Element): at risk until the seat flies
            /// them to the Ark (<see cref="VoyageRaidTally.Carry"/>, <see cref="VoyageRaidTally.Bank"/>).</summary>
            public int[] Carried = new int[(int)Element.Omni + 1];
            public int Kills;
            /// <summary>Creatures felled within the guard radius of the Ark: hull the seat saved.</summary>
            public int Guarded;

            /// <summary>Every crystal this seat carries and has not banked.</summary>
            public int CarriedTotal
            {
                get
                {
                    int n = 0;
                    foreach (var e in VoyageRaid.Elements) n += Carried[(int)e];
                    return n;
                }
            }

            public int Total
            {
                get
                {
                    int n = 0;
                    foreach (var e in VoyageRaid.Elements) n += Crystals[(int)e];
                    return n;
                }
            }
        }

        public readonly List<Seat> Seats = new();

        /// <summary>The seat <paramref name="playerName"/> flies, added the first time it is asked for.</summary>
        public Seat SeatOf(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return null;
            foreach (var s in Seats)
                if (s.PlayerName == playerName) return s;
            var seat = new Seat { PlayerName = playerName };
            Seats.Add(seat);
            return seat;
        }

        /// <summary>A crystal of <paramref name="element"/> came to <paramref name="playerName"/>. False (and nothing
        /// counted) for an omni crystal, or a crystal nobody of the squad took.</summary>
        public bool Add(string playerName, Element element)
        {
            if (!VoyageRaid.Counts(element)) return false;
            var seat = SeatOf(playerName);
            if (seat == null) return false;
            seat.Crystals[(int)element]++;
            return true;
        }

        /// <summary>A crystal of <paramref name="element"/> came to <paramref name="playerName"/>, who now CARRIES it: it
        /// counts only once banked at the Ark, and a hit can spill it (P6, Shombith 2026-11-16). False as for
        /// <see cref="Add"/>.</summary>
        public bool Carry(string playerName, Element element)
        {
            if (!VoyageRaid.Counts(element)) return false;
            var seat = SeatOf(playerName);
            if (seat == null) return false;
            seat.Carried[(int)element]++;
            return true;
        }

        /// <summary>The seat flew to the Ark: what it carries is banked (it counts). Returns how many were banked.</summary>
        public int Bank(string playerName)
        {
            var seat = Find(playerName);
            if (seat == null) return 0;
            int n = 0;
            foreach (var e in VoyageRaid.Elements)
            {
                int i = (int)e;
                n += seat.Carried[i];
                seat.Crystals[i] += seat.Carried[i];
                seat.Carried[i] = 0;
            }
            return n;
        }

        /// <summary>The seat was hit: everything it carries is lost from the tally, returned by element (index =
        /// (int)Element) so the run can scatter it back into the sea. All zeros when it carried nothing.</summary>
        public int[] Spill(string playerName)
        {
            var lost = new int[(int)Element.Omni + 1];
            var seat = Find(playerName);
            if (seat == null) return lost;
            foreach (var e in VoyageRaid.Elements)
            {
                int i = (int)e;
                lost[i] = seat.Carried[i];
                seat.Carried[i] = 0;
            }
            return lost;
        }

        /// <summary>
        /// One banked crystal lost from the hold (the Ark's hull is below its leak line, Shombith 2026-11-16): taken from
        /// the seat with the most banked, of that seat's most plentiful element, so no seat is emptied first by order.
        /// Returns the element lost, or <see cref="Element.None"/> when nothing is banked.
        /// </summary>
        public Element Leak()
        {
            Seat most = null;
            int mostTotal = 0;
            foreach (var s in Seats)
            {
                int t = s?.Total ?? 0;
                if (t > mostTotal) { most = s; mostTotal = t; }
            }
            if (most == null) return Element.None;
            var best = Element.None;
            int bestCount = 0;
            foreach (var e in VoyageRaid.Elements)
                if (most.Crystals[(int)e] > bestCount) { best = e; bestCount = most.Crystals[(int)e]; }
            if (best == Element.None) return Element.None;
            most.Crystals[(int)best]--;
            return best;
        }

        /// <summary>What <paramref name="playerName"/> carries unbanked (0 for a seat never seen).</summary>
        public int CarriedBy(string playerName) => Find(playerName)?.CarriedTotal ?? 0;

        /// <summary>Every crystal the squad carries and has not banked.</summary>
        public int CarriedTotal
        {
            get
            {
                int n = 0;
                foreach (var s in Seats) n += s.CarriedTotal;
                return n;
            }
        }

        Seat Find(string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return null;
            foreach (var s in Seats)
                if (s.PlayerName == playerName) return s;
            return null;
        }

        /// <summary>A creature fell to <paramref name="playerName"/>.</summary>
        public void Kill(string playerName, bool nearArk = false)
        {
            var seat = SeatOf(playerName);
            if (seat == null) return;
            seat.Kills++;
            if (nearArk) seat.Guarded++;
        }

        /// <summary>Every crystal the squad stole and banked (carried crystals count once banked).</summary>
        public int Total
        {
            get
            {
                int n = 0;
                foreach (var s in Seats) n += s.Total;
                return n;
            }
        }

        /// <summary>The squad's crystals of <paramref name="element"/>.</summary>
        public int Of(Element element)
        {
            int n = 0;
            foreach (var s in Seats) n += s.Crystals[(int)element];
            return n;
        }

        public void Clear() => Seats.Clear();

        /// <summary>Adds what <paramref name="other"/> holds (banked crystals, kills, guarded) to this tally, seat by seat:
        /// a voyage's record across its legs. Carried crystals are never added (they are lost unbanked at a stop).</summary>
        public void Absorb(VoyageRaidTally other)
        {
            if (other == null || ReferenceEquals(other, this)) return;
            foreach (var s in other.Seats)
            {
                if (s == null || string.IsNullOrEmpty(s.PlayerName)) continue;
                var seat = SeatOf(s.PlayerName);
                if (s.Vessel != 0) seat.Vessel = s.Vessel;
                for (int e = 0; e < seat.Crystals.Length && s.Crystals != null && e < s.Crystals.Length; e++)
                    seat.Crystals[e] += s.Crystals[e];
                seat.Kills += s.Kills;
                seat.Guarded += s.Guarded;
            }
        }

        /// <summary>A deep copy of every seat (the host's count, sent to the party).</summary>
        public List<Seat> Snapshot()
        {
            var copy = new List<Seat>(Seats.Count);
            foreach (var s in Seats)
            {
                if (s == null) continue;
                var c = new Seat { PlayerName = s.PlayerName, Vessel = s.Vessel, Kills = s.Kills, Guarded = s.Guarded };
                for (int e = 0; e < c.Crystals.Length && s.Crystals != null && e < s.Crystals.Length; e++) c.Crystals[e] = s.Crystals[e];
                copy.Add(c);
            }
            return copy;
        }

        /// <summary>Take the host's count as this machine's (a party friend): every seat replaced by
        /// <paramref name="seats"/>.</summary>
        public void Replace(List<Seat> seats)
        {
            // What a seat CARRIES is this machine's own (it is banked here, then reported): the host's count never
            // carries it, so it survives the replace.
            var carried = new List<Seat>();
            foreach (var s in Seats)
                if (s != null && s.CarriedTotal > 0) carried.Add(s);
            Seats.Clear();
            if (seats != null)
                foreach (var s in seats)
                {
                    if (s == null || string.IsNullOrEmpty(s.PlayerName)) continue;
                    var c = new Seat { PlayerName = s.PlayerName, Vessel = s.Vessel, Kills = s.Kills, Guarded = s.Guarded };
                    for (int e = 0; e < c.Crystals.Length && s.Crystals != null && e < s.Crystals.Length; e++) c.Crystals[e] = s.Crystals[e];
                    Seats.Add(c);
                }
            foreach (var old in carried)
            {
                Seat into = null;
                foreach (var c in Seats)
                    if (c.PlayerName == old.PlayerName) { into = c; break; }
                if (into == null)
                {
                    into = new Seat { PlayerName = old.PlayerName, Vessel = old.Vessel };
                    Seats.Add(into);
                }
                for (int e = 0; e < into.Carried.Length && e < old.Carried.Length; e++) into.Carried[e] = old.Carried[e];
            }
        }
    }
}
