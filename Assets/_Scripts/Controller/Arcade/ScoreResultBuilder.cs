using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Assembles a ranked <see cref="ScoreResult"/> list from raw per-player rows.
    /// Centralizes the sort + rank so every mode — and both the server and each client —
    /// produces an identically ordered list. Each mode supplies its own per-player
    /// <see cref="Row.Secondary"/> string, keeping per-mode formatting with the mode (SRP).
    /// See Docs/ScoringSystem/REFACTOR.md R10.
    /// </summary>
    public static class ScoreResultBuilder
    {
        /// <summary>Raw per-player input row, before sorting/ranking.</summary>
        public readonly struct Row
        {
            public readonly string Name;
            public readonly Domains Domain;
            public readonly float Score;
            public readonly string Secondary;

            public Row(string name, Domains domain, float score, string secondary)
            {
                Name = name;
                Domain = domain;
                Score = score;
                Secondary = secondary;
            }
        }

        /// <summary>
        /// Orders rows (golf rules: ascending = better; otherwise descending) and assigns
        /// 1-based ranks. OrderBy is stable, so equal scores keep their input order — which
        /// is how teammates on the same losing domain tie.
        /// </summary>
        public static List<ScoreResult> Build(IReadOnlyList<Row> rows, bool golfRules)
        {
            var ordered = golfRules
                ? rows.OrderBy(r => r.Score)
                : rows.OrderByDescending(r => r.Score);

            var results = new List<ScoreResult>(rows.Count);
            int rank = 1;
            foreach (var r in ordered)
                results.Add(new ScoreResult(rank++, r.Name, r.Domain, r.Score, r.Secondary));
            return results;
        }
    }
}
