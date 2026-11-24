using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    // The in-game voyage screens' data (Frogma Rev 134 and Rev 137, picked by Shombith 2026-11-24; Docs/MENU_REDESIGN_B.md
    // sections 227 and 230). The run reports what is next door, the rivals and the detour vote in these shapes; the screens
    // (the pause chart, the vote panel, the rival plate, the awards board) read them. Pure data and rules, no Unity, so the
    // headless voyage harness runs their tests. Gameplay fills them (the "Build the four Milestone 2 voyages" thread); until it
    // does, the run reports none and the screens show nothing new.

    /// <summary>The five events that happen next door (Rev 134 WorldEvents). Only a rival wears the threat red.</summary>
    public enum VoyageNeighbourKind
    {
        Wreck = 0,
        Bloom = 1,
        Rival = 2,
        Distress = 3,
        Predators = 4,
    }

    /// <summary>One event in a cell beside the lane, as the pause chart marks it: what it is, which leg cell it sits beside
    /// (1-based), on which side of the lane (a bearing in degrees, 0 = ahead, 90 = port, -90 = starboard), and how long it
    /// lasts. <see cref="Title"/> is the run's own name for it when it has one (a rival crew's name); empty uses the
    /// authored name of its kind.</summary>
    public struct VoyageNeighbourFix
    {
        public VoyageNeighbourKind Kind;
        public string Title;
        public int Cell;
        public float Bearing;
        /// <summary>Seconds before it is gone (a wreck sinks, a bloom fades); 0 or less: no timer.</summary>
        public float SecondsLeft;
        /// <summary>The work verb a detour there would be (a wreck: Stow, a bloom: Gather).</summary>
        public WorkVerb Verb;
    }

    /// <summary>A rival crew sails its own Ark, or none (a runaway crew; Rev 137).</summary>
    public enum VoyageRivalKind
    {
        Ark = 0,
        Runaway = 1,
    }

    /// <summary>What one rival escort is doing (Rev 137 ArkMeet to Rout).</summary>
    public enum VoyageEscortState
    {
        Guarding = 0,
        /// <summary>Hit with petals: harmless for a while (silence, Docs/ElementalAbilitySystem/SILENCE.md).</summary>
        Silenced = 1,
        /// <summary>The crew routed: running with what is left.</summary>
        Running = 2,
        /// <summary>Their Ark broke: turning back to save her.</summary>
        TurningBack = 3,
    }

    public struct VoyageEscortFix
    {
        public VesselClassType Vessel;
        public VoyageEscortState State;
        /// <summary>Seconds of silence left while <see cref="State"/> is Silenced.</summary>
        public float SilencedSeconds;
    }

    /// <summary>What a rival is doing to you right now (the feed's headline and the plate's note follow it).</summary>
    public enum VoyageRivalAct
    {
        /// <summary>In the next cell, not yet in yours.</summary>
        NextDoor = 0,
        /// <summary>In your cell, racing you for the same drift or the same stop.</summary>
        Racing = 1,
        /// <summary>Breaking your Ark's hull or raiding your hold or your drift.</summary>
        Attacking = 2,
        /// <summary>A runaway crew, all three silenced at once: they dropped their haul and ran.</summary>
        Routed = 3,
        /// <summary>A rival Ark's hull broke: her hold burst loose and the crew turns back.</summary>
        Broken = 4,
    }

    /// <summary>One rival crew as the screens show it (Rev 137): its name and kind, its temper, where it is on the chart,
    /// what it is doing, its Ark's hold and hull (a rival Ark) or what it carries (a runaway crew), and its escorts.</summary>
    public sealed class VoyageRivalFix
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        /// <summary>Their Ark's name (a rival Ark); empty for a runaway crew.</summary>
        public string ArkName = string.Empty;
        public VoyageRivalKind Kind;
        public FactionType Faction;
        public RivalTemper Temper;
        public VoyageRivalAct Act;
        /// <summary>Where they are bound (a display name), for "racing you to Glassmere".</summary>
        public string BoundFor = string.Empty;
        /// <summary>The leg cell they are in or beside (1-based) and their bearing from it (see <see cref="VoyageNeighbourFix"/>).</summary>
        public int Cell;
        public float Bearing;
        /// <summary>A rival Ark's hold, or what a runaway crew carries.</summary>
        public int Hold;
        /// <summary>A rival Ark's hull as a share of full (1 for a runaway crew).</summary>
        public float Hull = 1f;
        public readonly List<VoyageEscortFix> Escorts = new();

        /// <summary>Escorts silenced right now.</summary>
        public int Silenced
        {
            get
            {
                int n = 0;
                foreach (var e in Escorts) if (e.State == VoyageEscortState.Silenced) n++;
                return n;
            }
        }

        /// <summary>The shortest silence left among the silenced escorts (0 when none is silenced): how long the hull stays
        /// open, Rev 137 ArkOpen's "silenced for 0:06".</summary>
        public float OpenSeconds
        {
            get
            {
                float best = 0f;
                foreach (var e in Escorts)
                    if (e.State == VoyageEscortState.Silenced && (best <= 0f || e.SilencedSeconds < best)) best = e.SilencedSeconds;
                return best;
            }
        }

        /// <summary>Every escort is silenced at once: a runaway crew routs (Rev 137 Rout).</summary>
        public bool AllSilenced => Escorts.Count > 0 && Silenced == Escorts.Count;
    }

    // ── The detour vote (Rev 134 Vote B) ─────────────────────────────────────

    /// <summary>How a vote ended (the panel's last line says which).</summary>
    public enum VoyageVoteEnd
    {
        Open = 0,
        /// <summary>More presses for one option.</summary>
        Majority = 1,
        /// <summary>Even presses: the host's press won (Rev 134 VoteTie).</summary>
        HostBrokeTie = 2,
        /// <summary>Alone: the one press decided (Rev 134 VoteSolo).</summary>
        Solo = 3,
        /// <summary>No press in time (or a tie the host did not press): the squad presses on.</summary>
        NoPress = 4,
    }

    /// <summary>
    /// The detour vote (Rev 134 Vote B, picked by Shombith 2026-11-24): the pilot who spotted the event asks, two options
    /// (0 detour, 1 press on), five seconds, D-pad left and right. Alone, the one press decides at once; no press in time
    /// presses on. In a party every human seat presses once (and may change it until the vote closes); it closes when every
    /// human has pressed or the time runs out; most presses win; even presses go to the host's press; a tie the host did
    /// not press, or no press at all, presses on. An AI seat votes with its pilot's mood (<see cref="CastAi"/>); its vote
    /// counts like any other. The host runs one; the run sends the presses and the end to every machine.
    /// </summary>
    public sealed class VoyageVote
    {
        public const int Detour = 0, PressOn = 1, None = -1;
        public const float DefaultSeconds = 5f;

        /// <summary>The seat that spotted the event and asks (0-2).</summary>
        public int Asker;
        /// <summary>The host's seat (who breaks a tie).</summary>
        public int Host;
        public float Seconds = DefaultSeconds;
        public float Elapsed;
        /// <summary>What the detour would cost and win, the run's numbers (the panel fills its authored line with them).</summary>
        public float DetourSeconds;
        public VoyageNeighbourKind About;
        public string AboutTitle = string.Empty;

        readonly int[] _ballots;
        readonly bool[] _human;

        public VoyageVote(int seats, IReadOnlyList<bool> human)
        {
            _ballots = new int[Math.Max(1, seats)];
            _human = new bool[_ballots.Length];
            for (int i = 0; i < _ballots.Length; i++)
            {
                _ballots[i] = None;
                _human[i] = human != null && i < human.Count && human[i];
            }
        }

        public int Seats => _ballots.Length;
        public bool IsHuman(int seat) => seat >= 0 && seat < _human.Length && _human[seat];
        public int Ballot(int seat) => seat >= 0 && seat < _ballots.Length ? _ballots[seat] : None;

        public int Humans
        {
            get
            {
                int n = 0;
                foreach (var h in _human) if (h) n++;
                return n;
            }
        }

        /// <summary>One human pilot: the first press decides.</summary>
        public bool Solo => Humans <= 1;

        public float SecondsLeft => Math.Max(0f, Seconds - Elapsed);
        public VoyageVoteEnd End { get; private set; }
        public int Outcome { get; private set; } = None;
        public bool Closed => End != VoyageVoteEnd.Open;

        /// <summary>A human seat presses (again, to change it, while the vote is open). False when it does not count.</summary>
        public bool Cast(int seat, int option)
        {
            if (Closed || !IsHuman(seat) || option is not (Detour or PressOn)) return false;
            _ballots[seat] = option;
            if (Solo) Close(option, VoyageVoteEnd.Solo);
            else if (AllHumansPressed()) Decide();
            return true;
        }

        /// <summary>An AI seat votes (once, with its pilot's mood: a keen pilot takes the detour). Its vote is cast as the
        /// vote opens and counts like a press; it never closes a vote on its own.</summary>
        public bool CastAi(int seat, int option)
        {
            if (Closed || IsHuman(seat) || seat < 0 || seat >= _ballots.Length || option is not (Detour or PressOn)) return false;
            _ballots[seat] = option;
            return true;
        }

        /// <summary>Time passes; at five seconds the vote closes on what was pressed.</summary>
        public void Tick(float dt)
        {
            if (Closed) return;
            Elapsed += Math.Max(0f, dt);
            if (Elapsed >= Seconds) Decide();
        }

        bool AllHumansPressed()
        {
            for (int i = 0; i < _ballots.Length; i++)
                if (_human[i] && _ballots[i] == None) return false;
            return true;
        }

        /// <summary>Count and close: most presses win, even presses go to the host's press, else the squad presses on.</summary>
        void Decide()
        {
            int detour = 0, on = 0;
            foreach (var b in _ballots)
            {
                if (b == Detour) detour++;
                else if (b == PressOn) on++;
            }
            if (detour == 0 && on == 0) Close(PressOn, VoyageVoteEnd.NoPress);
            else if (Solo) Close(detour > 0 ? Detour : PressOn, VoyageVoteEnd.Solo);
            else if (detour != on) Close(detour > on ? Detour : PressOn, VoyageVoteEnd.Majority);
            else if (Ballot(Host) != None) Close(Ballot(Host), VoyageVoteEnd.HostBrokeTie);
            else Close(PressOn, VoyageVoteEnd.NoPress);
        }

        void Close(int outcome, VoyageVoteEnd end)
        {
            Outcome = outcome;
            End = end;
        }

        /// <summary>The vote ended elsewhere (a guest hears the host's end).</summary>
        public void CloseAs(int outcome, VoyageVoteEnd end)
        {
            if (end == VoyageVoteEnd.Open || outcome is not (Detour or PressOn)) return;
            Close(outcome, end);
        }

        /// <summary>A ballot heard from the host (a guest's copy shows every press as it comes).</summary>
        public void Hear(int seat, int option)
        {
            if (seat < 0 || seat >= _ballots.Length || option is not (Detour or PressOn or None)) return;
            _ballots[seat] = option;
        }
    }

    // ── The awards (Rev 134 Awards A) ────────────────────────────────────────

    /// <summary>One award card per seat on the end board (Rev 134 Awards A, Rev 137 Fall's Rival Breaker).</summary>
    public enum VoyageAward
    {
        TopSalvager = 0,
        WallOfTheVoyage = 1,
        RivalBreaker = 2,
        Hunter = 3,
        /// <summary>A seat no other award fits: it sailed with her.</summary>
        Crew = 4,
    }

    /// <summary>What a seat did on the voyage, for its award.</summary>
    public struct VoyageAwardScore
    {
        public int Crystals;
        public int Kills;
        /// <summary>Creatures felled near the Ark: the hull this seat saved.</summary>
        public int Guarded;
        /// <summary>The share of rival hulls this seat broke (0-1, the last blows).</summary>
        public float RivalHull;
    }

    /// <summary>
    /// Gives each seat one award (Rev 134 Awards A): the awards go in this order, each to the seat with the most of it among
    /// the seats still without one, only when that seat has any of it; a seat left over gets <see cref="VoyageAward.Crew"/>.
    /// Ties go to the earlier seat, so every machine gives the same awards.
    /// </summary>
    public static class VoyageAwards
    {
        static readonly VoyageAward[] Order =
            { VoyageAward.RivalBreaker, VoyageAward.TopSalvager, VoyageAward.WallOfTheVoyage, VoyageAward.Hunter };

        public static VoyageAward[] Pick(IReadOnlyList<VoyageAwardScore> seats)
        {
            int n = seats?.Count ?? 0;
            var awards = new VoyageAward[n];
            var given = new bool[n];
            for (int i = 0; i < n; i++) awards[i] = VoyageAward.Crew;
            foreach (var award in Order)
            {
                int best = -1;
                double most = 0;
                for (int i = 0; i < n; i++)
                {
                    if (given[i]) continue;
                    double v = Value(seats[i], award);
                    if (v > most) { most = v; best = i; }
                }
                if (best < 0) continue;
                awards[best] = award;
                given[best] = true;
            }
            return awards;
        }

        public static double Value(in VoyageAwardScore s, VoyageAward award) => award switch
        {
            VoyageAward.TopSalvager => s.Crystals,
            VoyageAward.WallOfTheVoyage => s.Guarded,
            VoyageAward.RivalBreaker => s.RivalHull,
            VoyageAward.Hunter => s.Kills,
            _ => 0,
        };

        /// <summary>A seat's share of all the hull the squad saved (Wall of the voyage, "41% of the Ark's damage stopped").</summary>
        public static int GuardedPercent(IReadOnlyList<VoyageAwardScore> seats, int seat)
        {
            if (seats == null || seat < 0 || seat >= seats.Count) return 0;
            int total = 0;
            foreach (var s in seats) total += Math.Max(0, s.Guarded);
            return total <= 0 ? 0 : (int)Math.Round(100.0 * Math.Max(0, seats[seat].Guarded) / total);
        }
    }

    // ── The pause chart's layout (Rev 134 Pause A) ───────────────────────────

    /// <summary>
    /// Lays the leg on the pause chart (Rev 134 Pause A): the route's places, from the chart's own coordinates, fitted into
    /// the chart's box keeping their shape; the leg's cells spread along that line by length; the Ark at its progress along
    /// it; and a cell's neighbour off to one side of the lane. Pure maths, y down (screen units).
    /// </summary>
    public static class VoyageChartLayout
    {
        public struct Point
        {
            public float X, Y;
            public Point(float x, float y) { X = x; Y = y; }
        }

        /// <summary>Fit <paramref name="places"/> (chart x, chart y; y up) into the box, keeping their shape and centred; a
        /// leg with one place sits in the box's middle.</summary>
        public static List<Point> Fit(IReadOnlyList<Point> places, float boxX, float boxY, float boxW, float boxH)
        {
            var result = new List<Point>(places?.Count ?? 0);
            if (places == null || places.Count == 0) return result;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in places)
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            }
            float w = maxX - minX, h = maxY - minY;
            float scale = w <= 1e-4f && h <= 1e-4f ? 0f
                : Math.Min(w > 1e-4f ? boxW / w : float.MaxValue, h > 1e-4f ? boxH / h : float.MaxValue);
            float cx = (minX + maxX) * 0.5f, cy = (minY + maxY) * 0.5f;
            float mx = boxX + boxW * 0.5f, my = boxY + boxH * 0.5f;
            foreach (var p in places) result.Add(new Point(mx + (p.X - cx) * scale, my - (p.Y - cy) * scale));
            return result;
        }

        /// <summary>The point <paramref name="share"/> (0-1) of the way along the polyline, by length.</summary>
        public static Point Along(IReadOnlyList<Point> line, float share)
        {
            if (line == null || line.Count == 0) return default;
            if (line.Count == 1) return line[0];
            float total = Length(line);
            if (total <= 1e-5f) return line[0];
            float want = Math.Max(0f, Math.Min(1f, share)) * total;
            for (int i = 1; i < line.Count; i++)
            {
                float seg = Dist(line[i - 1], line[i]);
                if (want <= seg || i == line.Count - 1)
                {
                    float t = seg <= 1e-5f ? 0f : Math.Min(1f, want / seg);
                    return new Point(line[i - 1].X + (line[i].X - line[i - 1].X) * t, line[i - 1].Y + (line[i].Y - line[i - 1].Y) * t);
                }
                want -= seg;
            }
            return line[line.Count - 1];
        }

        /// <summary>The direction of travel at <paramref name="share"/> along the polyline (a unit vector; ahead when flat).</summary>
        public static Point Heading(IReadOnlyList<Point> line, float share)
        {
            var a = Along(line, Math.Max(0f, share - 0.01f));
            var b = Along(line, Math.Min(1f, share + 0.01f));
            float dx = b.X - a.X, dy = b.Y - a.Y, d = (float)Math.Sqrt(dx * dx + dy * dy);
            return d <= 1e-5f ? new Point(1f, 0f) : new Point(dx / d, dy / d);
        }

        /// <summary>Where leg cell <paramref name="cell"/> (1-based, of <paramref name="cells"/>) sits: the leg is cut into
        /// that many equal lengths and each cell sits in the middle of its own.</summary>
        public static float CellShare(int cell, int cells) =>
            cells <= 0 ? 0f : Math.Max(0f, Math.Min(1f, (Math.Max(1, Math.Min(cells, cell)) - 0.5f) / cells));

        /// <summary>A neighbour of a cell: <paramref name="distance"/> off its centre at <paramref name="bearing"/> degrees
        /// from the direction of travel (positive to port, which is up on a lane sailing right).</summary>
        public static Point Beside(IReadOnlyList<Point> line, float share, float bearing, float distance)
        {
            var at = Along(line, share);
            var ahead = Heading(line, share);
            double a = bearing * Math.PI / 180.0;
            // Rotate the heading by the bearing; y is down, so port (a left turn) is a negative screen rotation.
            float x = (float)(ahead.X * Math.Cos(a) + ahead.Y * Math.Sin(a));
            float y = (float)(-ahead.X * Math.Sin(a) + ahead.Y * Math.Cos(a));
            return new Point(at.X + x * distance, at.Y + y * distance);
        }

        public static float Length(IReadOnlyList<Point> line)
        {
            float total = 0f;
            for (int i = 1; i < (line?.Count ?? 0); i++) total += Dist(line[i - 1], line[i]);
            return total;
        }

        static float Dist(Point a, Point b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
