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
                if (t == null || !t.Always || t.Id == VoyageRules.ReturnJobId) continue;
                var job = Draw(state, content, t, here, person, usedTo ?? new List<string>(), 0);
                if (job != null) jobs.Add(job);
            }
            return jobs;
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
            // Never straight back to where this run set out from, while anywhere else will do.
            var usedTo = new List<string>();
            if (!string.IsNullOrEmpty(done.From)) usedTo.Add(done.From);
            return Draw(state, content, t, here, person, usedTo, done.Venture + 1)
                   ?? Draw(state, content, t, here, person, new List<string>(), done.Venture + 1);
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
        public static VoyageRules.Resolution Arrive(VoyageState state, VoyageContent content, VoyageRaidTally tally)
        {
            var job = state?.Current;
            if (job == null || !job.CountsUnderway) return null;
            Owe(state, tally);
            var ok = tally != null && tally.Total >= job.Goal;
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
            /// <summary>Crystals by element (index = (int)Element).</summary>
            public int[] Crystals = new int[(int)Element.Omni + 1];
            public int Kills;
            /// <summary>Creatures felled within the guard radius of the Ark: hull the seat saved.</summary>
            public int Guarded;

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

        /// <summary>A creature fell to <paramref name="playerName"/>.</summary>
        public void Kill(string playerName, bool nearArk = false)
        {
            var seat = SeatOf(playerName);
            if (seat == null) return;
            seat.Kills++;
            if (nearArk) seat.Guarded++;
        }

        /// <summary>Every crystal the squad stole.</summary>
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
    }
}
