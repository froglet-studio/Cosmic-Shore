using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md): every pilot is on ONE domain and the opponent is the
    /// swarm, so the winner is not a metric race. The match ends on the swarm's <see cref="TandavaOutcome"/>, which
    /// <see cref="TandavaController"/> publishes into this rule on every peer (<see cref="Publish"/>): wiped out,
    /// starved or broken, the pilots' domain wins; escaped, nobody does (<see cref="Domains.Blue"/>, the platform's
    /// "no winner" sentinel), and the loss is scored by the form it escaped as. A pilot's own score is the members they
    /// culled (<see cref="ScoringMetric.LifeformsKilled"/> - attributed kills only, so a starved tadpole scores nobody).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Tandava", fileName = "TandavaScoringRule")]
    public class TandavaScoringRuleSO : ScoringRuleSO
    {
        [System.NonSerialized] TandavaOutcome _outcome;
        [System.NonSerialized] int _formReached, _formCount;
        [System.NonSerialized] string _formName = "";

        public TandavaOutcome Outcome => _outcome;

        /// <summary>The controller's replicated outcome, written on every peer (server: as it happens; clients: with
        /// the final-scores snapshot). <paramref name="formReached"/> is 1-based.</summary>
        public void Publish(TandavaOutcome outcome, int formReached, int formCount, string formName)
        {
            _outcome = outcome;
            _formReached = formReached;
            _formCount = formCount;
            _formName = formName ?? "";
        }

        /// <summary>Back to a fresh match (replay, a new scene).</summary>
        public void ResetOutcome() => Publish(TandavaOutcome.Running, 0, 0, "");

        public static bool PilotsWon(TandavaOutcome o) =>
            o is TandavaOutcome.Wiped or TandavaOutcome.Starved or TandavaOutcome.Broken;

        /// <summary>The one domain the pilots fly (the first non-Blue domain on the roster).</summary>
        public static Domains PilotDomain(GameDataSO gameData)
        {
            var list = gameData != null ? gameData.RoundStatsList : null;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].Domain != Domains.Blue) return list[i].Domain;
            return Domains.Blue;
        }

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = PilotsWon(_outcome) ? PilotDomain(gameData) : Domains.Blue;
            return _outcome != TandavaOutcome.Running;
        }

        public override Domains ResolveWinner(GameDataSO gameData) =>
            PilotsWon(_outcome) ? PilotDomain(gameData) : Domains.Blue;

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = stats.LifeformsKilled;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var rows = gameData.RoundStatsList.OrderByDescending(s => s.Score).Select(s => new ScoreResultBuilder.Row(
                s.Name, s.Domain, s.Score, $"{(int)s.Score} culled", null)).ToList();
            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            string label = _outcome switch
            {
                TandavaOutcome.Wiped => "THE SWARM IS GONE",
                TandavaOutcome.Starved => "STARVED BEFORE THE MEMBRANE",
                TandavaOutcome.Broken => $"THE {_formName.ToUpperInvariant()} IS BROKEN",
                TandavaOutcome.Escaped => $"ESCAPED AS THE {_formName.ToUpperInvariant()} ({_formReached} OF {_formCount})",
                _ => "",
            };
            return new ScoreReveal(PilotsWon(_outcome) ? "VICTORY" : "DEFEAT", label, (int)localStats.Score, false);
        }
    }
}
