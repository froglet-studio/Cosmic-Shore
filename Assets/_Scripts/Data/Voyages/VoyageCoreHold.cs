using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// What holding a core asks at one difficulty (Hold the Core, Docs/Voyages/MILESTONE_2.md §6; the Voyage Codex's
    /// ladder): how long the core must stay yours, how many waves come at it, and how many hulls of the other domain's
    /// crew come to retake it in the second wave. The sea's own danger (breach line, life) is <see cref="VoyageSeaDanger"/>'s.
    /// </summary>
    [Serializable]
    public struct VoyageCoreHoldPlan
    {
        /// <summary>Seconds the core must be yours (the clock runs only while it is).</summary>
        public float HoldSeconds;
        /// <summary>Waves of the other domain's fauna, spread over the hold (1-3).</summary>
        public int Waves;
        /// <summary>Hulls of the other domain's crew that come to retake the core with the second wave (0: none).</summary>
        public int RetakeHulls;
        /// <summary>Seconds the core may stay theirs, once it was yours, before the hold is lost.</summary>
        public float TurnedSeconds;

        static VoyageCoreHoldPlan P(float hold, int waves, int retake) =>
            new() { HoldSeconds = hold, Waves = waves, RetakeHulls = retake, TurnedSeconds = 15f };

        /// <summary>Difficulty 1 to 9 (the Codex: 1-2 hold 45 s, one wave, no rival; 3-4 hold 60 s, two waves, a crew
        /// retakes in wave 2; 5-6 hold 75 s; 7-9 hold 90 s, three waves, a strength-3 crew).</summary>
        public static VoyageCoreHoldPlan[] Defaults() => new[]
        {
            P(45f, 1, 0), P(45f, 1, 0),
            P(60f, 2, 1), P(60f, 2, 1),
            P(75f, 2, 2), P(75f, 2, 2),
            P(90f, 3, 3), P(90f, 3, 3), P(90f, 3, 3),
        };

        /// <summary>The row for <paramref name="difficulty"/> (clamped to 1-9; a short table repeats its last row).</summary>
        public static VoyageCoreHoldPlan At(IReadOnlyList<VoyageCoreHoldPlan> table, int difficulty)
        {
            if (table == null || table.Count == 0) table = Defaults();
            int i = Math.Min(table.Count, VoyageDifficulty.Clamp(difficulty)) - 1;
            var p = table[i];
            p.HoldSeconds = Math.Max(1f, p.HoldSeconds);
            p.Waves = Math.Max(1, Math.Min(3, p.Waves));
            p.RetakeHulls = Math.Max(0, p.RetakeHulls);
            p.TurnedSeconds = Math.Max(1f, p.TurnedSeconds);
            return p;
        }
    }

    /// <summary>
    /// The hold clock at a core (pure; the run feeds it the core's control volumes every frame). The clock runs only
    /// while the squad's domain holds more of the nucleus than any other; once the core has been yours, it turning theirs
    /// starts a countdown, and the hold is lost if it stays theirs for <see cref="VoyageCoreHoldPlan.TurnedSeconds"/>.
    /// Wave 1 comes at the anchor; wave k when the clock passes (k - 1) / waves of the hold.
    /// </summary>
    public sealed class VoyageCoreHold
    {
        public enum Outcome { Holding = 0, Held = 1, Turned = 2 }

        public VoyageCoreHoldPlan Plan { get; }
        /// <summary>Seconds the core has been yours so far.</summary>
        public float Held { get; private set; }
        /// <summary>Seconds the core has been theirs since it last was yours (0 while it is yours or never was).</summary>
        public float Turned { get; private set; }
        /// <summary>Your share of the nucleus against the strongest other domain, 0-1 (0 when the nucleus is empty).</summary>
        public float Share { get; private set; }
        /// <summary>The core is yours now.</summary>
        public bool Yours { get; private set; }
        /// <summary>The core has been yours at least once.</summary>
        public bool EverYours { get; private set; }
        /// <summary>The wave the hold is in, from 1 (0 before the first tick).</summary>
        public int Wave { get; private set; }
        public Outcome State { get; private set; }

        public VoyageCoreHold(VoyageCoreHoldPlan plan) => Plan = plan;

        /// <summary>Seconds of the hold left.</summary>
        public float Left => Math.Max(0f, Plan.HoldSeconds - Held);

        /// <summary>Seconds left before a turned core is lost (0 while it is not turning).</summary>
        public float TurnedLeft => Turned > 0f ? Math.Max(0f, Plan.TurnedSeconds - Turned) : 0f;

        /// <summary>The wave the clock has reached at <paramref name="held"/> seconds held.</summary>
        public static int WaveAt(in VoyageCoreHoldPlan plan, float held)
        {
            int waves = Math.Max(1, plan.Waves);
            float hold = Math.Max(1f, plan.HoldSeconds);
            int w = 1 + (int)Math.Floor(Math.Max(0f, held) / hold * waves);
            return Math.Min(waves, w);
        }

        /// <summary>
        /// Advance by <paramref name="dt"/> seconds with <paramref name="yours"/> of the nucleus the squad's and
        /// <paramref name="theirs"/> the strongest other domain's. Returns the wave that began on this tick (0: none).
        /// </summary>
        public int Tick(float dt, float yours, float theirs)
        {
            if (State != Outcome.Holding) return 0;
            dt = Math.Max(0f, dt);
            yours = Math.Max(0f, yours);
            theirs = Math.Max(0f, theirs);
            float total = yours + theirs;
            Share = total > 0f ? yours / total : 0f;
            Yours = yours > theirs;
            if (Yours)
            {
                EverYours = true;
                Turned = 0f;
                Held = Math.Min(Plan.HoldSeconds, Held + dt);
            }
            else if (EverYours && theirs > yours)
            {
                Turned += dt;
                if (Turned >= Plan.TurnedSeconds)
                {
                    State = Outcome.Turned;
                    return 0;
                }
            }
            int wave = WaveAt(Plan, Held);
            int began = wave > Wave ? wave : 0;
            Wave = Math.Max(Wave, wave);
            if (Held >= Plan.HoldSeconds) State = Outcome.Held;
            return began;
        }
    }
}
