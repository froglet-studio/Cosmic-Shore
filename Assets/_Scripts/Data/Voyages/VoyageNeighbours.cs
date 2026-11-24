using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// What happens next door on a named voyage's leg (Frogma Rev 134 WorldEvents, Event, Detour; Shombith 2026-11-24:
    /// "nothing happening in neighbouring cells"): which cells have an event beside the lane, what it is, on which side,
    /// and how long it lasts once the Ark is there. Seeded by the day and the voyage, so every machine in a party plans the
    /// same leg. The squad votes on each one (<see cref="VoyageVote"/>); a detour holds the Ark there while the squad takes
    /// it. Pure, so the headless voyage harness tests it.
    /// </summary>
    public static class VoyageNeighbours
    {
        /// <summary>
        /// The events beside a leg of <paramref name="cells"/> cells: at most one a cell, never beside the last (the
        /// voyage's own stop), each cell with <paramref name="chance"/> (0-1). A wreck (stow what it spilled) is likelier
        /// on rougher water, a bloom (gather it) on calmer. Each lasts <paramref name="seconds"/>, give or take a quarter.
        /// </summary>
        public static List<VoyageNeighbourFix> Plan(int seed, int cells, int difficulty, float chance, float seconds)
        {
            var plan = new List<VoyageNeighbourFix>();
            if (cells <= 1 || chance <= 0f) return plan;
            var rng = new Random(seed);
            float rough = (VoyageDifficulty.Clamp(difficulty) - 1) / 8f;   // 0 at difficulty 1, 1 at 9
            for (int cell = 1; cell < cells; cell++)
            {
                double roll = rng.NextDouble();
                double kindRoll = rng.NextDouble();
                double side = rng.NextDouble();
                double length = rng.NextDouble();
                if (roll >= chance) continue;
                var kind = kindRoll < 0.35 + 0.4 * rough ? VoyageNeighbourKind.Wreck : VoyageNeighbourKind.Bloom;
                plan.Add(new VoyageNeighbourFix
                {
                    Kind = kind,
                    Cell = cell,
                    Bearing = side < 0.5 ? 90f : -90f,
                    SecondsLeft = Math.Max(10f, seconds * (float)(0.75 + 0.5 * length)),
                    Verb = VerbOf(kind),
                });
            }
            return plan;
        }

        /// <summary>The work a detour to <paramref name="kind"/> would be (the vote panel and the detour toast name it).</summary>
        public static WorkVerb VerbOf(VoyageNeighbourKind kind) => kind switch
        {
            VoyageNeighbourKind.Wreck => WorkVerb.Stow,
            VoyageNeighbourKind.Bloom => WorkVerb.Gather,
            VoyageNeighbourKind.Distress => WorkVerb.Arrive,
            VoyageNeighbourKind.Predators => WorkVerb.Cull,
            VoyageNeighbourKind.Rival => WorkVerb.Meet,
            _ => WorkVerb.Arrive,
        };

        /// <summary>How an AI seat votes (it votes as the vote opens): a pilot keen on the voyage takes the detour, any
        /// other presses on (Shombith 2026-11-24, the mood effects).</summary>
        public static int AiBallot(PilotMood mood) => mood == PilotMood.Keen ? VoyageVote.Detour : VoyageVote.PressOn;

        /// <summary>The seat that asks: the first keen seat, else <paramref name="fallback"/>.</summary>
        public static int Asker(IReadOnlyList<PilotMood> moods, int fallback)
        {
            if (moods != null)
                for (int i = 0; i < moods.Count; i++)
                    if (moods[i] == PilotMood.Keen) return i;
            return fallback;
        }
    }
}
