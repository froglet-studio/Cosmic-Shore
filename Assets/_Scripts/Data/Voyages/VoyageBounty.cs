using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// What a Bounty asks at one difficulty (Docs/Voyages/MILESTONE_2.md section 7; the Voyage Codex's ladder; Frogma Rev
    /// 137): how long the hunted crew stays before it makes port, how many crystals it carries (what spills when it is
    /// beaten), how far a rival Ark's hull must break before her crew turns back, and how many of her escorts must be
    /// silenced at once before her hull is open. The sea's own danger is <see cref="VoyageSeaDanger"/>'s.
    /// </summary>
    [Serializable]
    public struct VoyageBountyPlan
    {
        /// <summary>Seconds from the meeting before the crew makes port and the warrant goes unserved.</summary>
        public float EscapeSeconds;
        /// <summary>Crystals the crew carries: a rival Ark's hold, or a runaway crew's haul. All of it spills when beaten.</summary>
        public int Hold;
        /// <summary>A rival Ark's hull share (0-1) at or under which she breaks: her hold bursts and the crew turns back.</summary>
        public float BreakAt;
        /// <summary>Escorts that must be silenced at the same moment before a rival Ark's hull is open (silenced escorts
        /// cannot wall her). Capped at the escorts she has.</summary>
        public int OpenAt;

        static VoyageBountyPlan P(float escape, int hold, float breakAt, int openAt) =>
            new() { EscapeSeconds = escape, Hold = hold, BreakAt = breakAt, OpenAt = openAt };

        /// <summary>Difficulty 1 to 9 (the Codex: 1-2 a young crew that runs early; 3-4 they fight; 5-6 they turn on the
        /// Ark; 7-9 every escort must be silenced to open her, and she breaks late).</summary>
        public static VoyageBountyPlan[] Defaults() => new[]
        {
            P(110f, 12, 0.5f, 2), P(105f, 14, 0.5f, 2),
            P(100f, 18, 0.5f, 2), P(95f, 20, 0.5f, 2),
            P(90f, 24, 0.45f, 2), P(90f, 26, 0.45f, 2),
            P(85f, 30, 0.4f, 3), P(80f, 32, 0.4f, 3), P(75f, 36, 0.35f, 3),
        };

        /// <summary>The row for <paramref name="difficulty"/> (clamped to 1-9; a short table repeats its last row).</summary>
        public static VoyageBountyPlan At(IReadOnlyList<VoyageBountyPlan> table, int difficulty)
        {
            if (table == null || table.Count == 0) table = Defaults();
            int i = Math.Min(table.Count, VoyageDifficulty.Clamp(difficulty)) - 1;
            var p = table[i];
            p.EscapeSeconds = Math.Max(10f, p.EscapeSeconds);
            p.Hold = Math.Max(0, p.Hold);
            p.BreakAt = Math.Max(0f, Math.Min(0.95f, p.BreakAt));
            p.OpenAt = Math.Max(1, p.OpenAt);
            return p;
        }
    }

    /// <summary>
    /// The fight with a hunted crew (pure; the run feeds it every frame). Frogma Rev 137's rules, approved by Shombith
    /// 2026-11-24:
    /// <list type="bullet">
    /// <item>A runaway crew (no Ark) routs when every escort is silenced at the same moment: it drops its haul and runs.</item>
    /// <item>A rival Ark is walled by her escorts; silenced escorts cannot wall. Her hull is open while at least
    /// <see cref="VoyageBountyPlan.OpenAt"/> of them are silenced at once. When her hull breaks to
    /// <see cref="VoyageBountyPlan.BreakAt"/> her hold bursts and the crew turns back.</item>
    /// <item>Either way, the crew makes port when the clock runs out, and the warrant goes unserved.</item>
    /// </list>
    /// </summary>
    public sealed class VoyageBountyFight
    {
        public enum Outcome { Fighting = 0, Routed = 1, Broken = 2, Escaped = 3 }

        public VoyageBountyPlan Plan { get; }
        /// <summary>The crew sails its own Ark (else a runaway crew).</summary>
        public bool SailsArk { get; }
        public Outcome State { get; private set; }
        public float Elapsed { get; private set; }
        /// <summary>Escorts silenced on the last tick, and how many there are.</summary>
        public int Silenced { get; private set; }
        public int Escorts { get; private set; }
        /// <summary>A rival Ark's hull share on the last tick (1 for a runaway crew).</summary>
        public float Hull { get; private set; } = 1f;

        public VoyageBountyFight(VoyageBountyPlan plan, bool sailsArk)
        {
            Plan = plan;
            SailsArk = sailsArk;
        }

        /// <summary>Seconds before the crew makes port.</summary>
        public float Left => Math.Max(0f, Plan.EscapeSeconds - Elapsed);

        /// <summary>True when the warrant was served (routed or broken).</summary>
        public bool Beaten => State is Outcome.Routed or Outcome.Broken;

        /// <summary>Escorts that must be silenced at once to open a rival Ark with <paramref name="escorts"/> escorts.</summary>
        public static int OpenNeeded(in VoyageBountyPlan plan, int escorts) => Math.Max(1, Math.Min(Math.Max(1, escorts), plan.OpenAt));

        /// <summary>A rival Ark's hull is open now (no wall): enough escorts are silenced at once. Never for a runaway crew.</summary>
        public bool Open => SailsArk && State == Outcome.Fighting && Silenced >= OpenNeeded(Plan, Escorts);

        /// <summary>
        /// Advance by <paramref name="dt"/> seconds. <paramref name="silenced"/> holds each escort's silence left now
        /// (more than 0: silenced; an escort that is gone from the sea counts as silenced, it walls nothing);
        /// <paramref name="hull"/> is a rival Ark's hull share (ignored for a runaway crew). Returns true on the tick the
        /// fight is decided.
        /// </summary>
        public bool Tick(float dt, IReadOnlyList<float> silenced, float hull)
        {
            if (State != Outcome.Fighting) return false;
            Elapsed += Math.Max(0f, dt);
            Escorts = silenced?.Count ?? 0;
            int n = 0;
            if (silenced != null)
                foreach (var s in silenced) if (s > 0f) n++;
            Silenced = n;
            Hull = SailsArk ? Math.Max(0f, Math.Min(1f, hull)) : 1f;

            if (SailsArk && Hull <= Plan.BreakAt) State = Outcome.Broken;
            else if (!SailsArk && Escorts > 0 && Silenced == Escorts) State = Outcome.Routed;
            else if (Elapsed >= Plan.EscapeSeconds) State = Outcome.Escaped;
            return State != Outcome.Fighting;
        }

        /// <summary>
        /// The screens' view of this crew (Rev 137's plate and toasts): its act, hold and hull, and one row per escort.
        /// <paramref name="hulls"/> and <paramref name="silenced"/> are the escorts in order.
        /// </summary>
        public void Fill(VoyageRivalFix fix, RivalCrew crew, IReadOnlyList<VesselClassType> hulls, IReadOnlyList<float> silenced)
        {
            if (fix == null) return;
            fix.Id = crew?.Id ?? string.Empty;
            fix.Name = crew?.Name ?? string.Empty;
            fix.ArkName = SailsArk ? crew?.ArkName ?? string.Empty : string.Empty;
            fix.Kind = SailsArk ? VoyageRivalKind.Ark : VoyageRivalKind.Runaway;
            fix.Faction = crew?.Faction ?? FactionType.None;
            fix.Temper = crew?.Temper ?? default;
            fix.Act = State switch
            {
                Outcome.Routed => VoyageRivalAct.Routed,
                Outcome.Broken => VoyageRivalAct.Broken,
                _ => VoyageRivalAct.Racing,
            };
            fix.Hold = Beaten ? 0 : Plan.Hold;
            fix.Hull = Hull;
            fix.Escorts.Clear();
            int count = hulls?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                float left = silenced != null && i < silenced.Count ? silenced[i] : 0f;
                var state = State switch
                {
                    Outcome.Routed or Outcome.Escaped => VoyageEscortState.Running,
                    Outcome.Broken => VoyageEscortState.TurningBack,
                    _ => left > 0f ? VoyageEscortState.Silenced : VoyageEscortState.Guarding,
                };
                fix.Escorts.Add(new VoyageEscortFix { Vessel = hulls[i], State = state, SilencedSeconds = Math.Max(0f, left) });
            }
        }
    }

    /// <summary>Who struck the hunted crew (Rev 137 Fall's Rival Breaker): blows by player name, and each one's share.</summary>
    public sealed class VoyageRivalBlows
    {
        readonly Dictionary<string, int> _by = new();
        public int Total { get; private set; }

        public void Add(string player, int blows = 1)
        {
            if (string.IsNullOrEmpty(player) || blows <= 0) return;
            _by.TryGetValue(player, out int n);
            _by[player] = n + blows;
            Total += blows;
        }

        /// <summary><paramref name="player"/>'s share of every blow, 0-1 (0 with none struck).</summary>
        public float Share(string player) =>
            Total > 0 && !string.IsNullOrEmpty(player) && _by.TryGetValue(player, out int n) ? (float)n / Total : 0f;

        public void Clear()
        {
            _by.Clear();
            Total = 0;
        }
    }
}
