using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tandava (Assets/_Scripts/Controller/Arcade/TANDAVA.md): every pilot is on ONE domain and the opponent is the
    /// swarm, so the winner is not a metric race. The match ends on the swarm's <see cref="TandavaOutcome"/>, which
    /// <see cref="TandavaController"/> publishes into this rule on every peer (<see cref="Publish"/>): wiped out,
    /// shattered, starved, its dance broken or held off until the clock ran out, the pilots' domain wins; the cycle
    /// completed (the Sea Lion's last feast), nobody does (<see cref="Domains.Blue"/>, the platform's "no winner"
    /// sentinel). A pilot's own score is the members they culled (<see cref="ScoringMetric.LifeformsKilled"/> -
    /// attributed kills only, so a starved tadpole scores nobody) plus <see cref="haloPoints"/> for each halo ring they
    /// broke (counted, server-side, in <see cref="IRoundStats.SwitchesThreaded"/> - a halo ring IS a switch, threaded).
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Tandava", fileName = "TandavaScoringRule")]
    public class TandavaScoringRuleSO : ScoringRuleSO
    {
        [Header("Tandava")]
        [Tooltip("Points per halo ring a pilot breaks, on top of one per member culled. A ring is worth a pack of culls: " +
                 "it is threaded past the attendants, against the drum.")]
        [FormerlySerializedAs("flamePoints")]
        [Min(0)] public int haloPoints = 25;

        [System.NonSerialized] TandavaOutcome _outcome;
        [System.NonSerialized] int _formReached, _formCount;
        [System.NonSerialized] string _formName = "";

        public TandavaOutcome Outcome => _outcome;
        /// <summary>The form the match ended in (1-based) and how many there are.</summary>
        public int FormReached => _formReached;
        public int FormCount => _formCount;

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
            o is TandavaOutcome.Wiped or TandavaOutcome.Starved or TandavaOutcome.Shattered or TandavaOutcome.DanceBroken
                or TandavaOutcome.HeldOff;

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
                stats.Score = stats.LifeformsKilled + haloPoints * stats.SwitchesThreaded;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var rows = gameData.RoundStatsList.OrderByDescending(s => s.Score).Select(s => new ScoreResultBuilder.Row(
                s.Name, s.Domain, s.Score,
                s.SwitchesThreaded > 0 ? $"{s.LifeformsKilled} culled, {s.SwitchesThreaded} halo rings" : $"{s.LifeformsKilled} culled",
                null)).ToList();
            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            string label = _outcome switch
            {
                TandavaOutcome.Wiped => "THE SWARM IS GONE",
                TandavaOutcome.Starved => $"THE {_formName.ToUpperInvariant()} STARVED",
                TandavaOutcome.Shattered => $"THE {_formName.ToUpperInvariant()} IS SHATTERED",
                TandavaOutcome.DanceBroken => "THE DANCE IS BROKEN",
                TandavaOutcome.HeldOff => $"HELD OFF AS THE {_formName.ToUpperInvariant()}",
                // only the Sea Lion's feast completes the cycle, so the form IS the story
                TandavaOutcome.Completed => $"THE {_formName.ToUpperInvariant()} HAS FED - THE CYCLE IS COMPLETE",
                _ => "",
            };
            return new ScoreReveal(PilotsWon(_outcome) ? "VICTORY" : "DEFEAT", label, (int)localStats.Score, false);
        }
    }
}
