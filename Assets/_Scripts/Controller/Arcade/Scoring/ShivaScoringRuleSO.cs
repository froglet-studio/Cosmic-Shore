using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tandava, redesigned as Shiva's Cell (Assets/_Scripts/Controller/Arcade/TANDAVA.md §0): the squad is ONE domain and
    /// its opponent is the god, so the winner is not a metric race. The match ends on the director's
    /// <see cref="ShivaOutcome"/>, which <see cref="ShivaController"/> publishes here on every peer: the third shell broken,
    /// the squad's domain wins; a form's drum run out, nobody does (<see cref="Domains.Blue"/>, the platform's "no winner").
    /// A pilot's score is the rings they threaded (<see cref="IRoundStats.SwitchesThreaded"/>, counted server-side) times
    /// <see cref="ringPoints"/>, plus the elemental crystals they collected. Every word on the result screen is a field.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Scoring Rules/Shiva", fileName = "ShivaScoringRule")]
    public class ShivaScoringRuleSO : ScoringRuleSO
    {
        [Header("Shiva")]
        [Tooltip("Points per ring a pilot threads (the squad's progress is shared; the threading is a pilot's own).")]
        [Min(0)] public int ringPoints = 10;
        [Tooltip("Points per elemental crystal a pilot collected.")]
        [Min(0)] public int crystalPoints = 1;

        [Header("Text")]
        [Tooltip("The result screen's headline when the squad wins.")]
        public string victoryTitle = "VICTORY";
        [Tooltip("The result screen's headline when the drum runs out.")]
        public string defeatTitle = "DEFEAT";
        [Tooltip("The line under the headline when the squad wins.")]
        public string wonLabel = "THE TANDAVA IS BROKEN";
        [Tooltip("The line under the headline when the drum runs out. {form} = the form it ran out in.")]
        public string lostLabel = "THE DRUM STOPS IN {form}";
        [Tooltip("A pilot's row on the results: {rings} rings threaded, {crystals} crystals.")]
        public string rowDetail = "{rings} rings, {crystals} crystals";

        [System.NonSerialized] ShivaOutcome _outcome;
        [System.NonSerialized] int _formReached;
        [System.NonSerialized] string _formName = "";

        public ShivaOutcome Outcome => _outcome;
        /// <summary>The form the match ended in (1-based).</summary>
        public int FormReached => _formReached;

        /// <summary>The controller's replicated outcome, on every peer. <paramref name="formReached"/> is 1-based.</summary>
        public void Publish(ShivaOutcome outcome, int formReached, string formName)
        {
            _outcome = outcome;
            _formReached = formReached;
            _formName = formName ?? "";
        }

        public void ResetOutcome() => Publish(ShivaOutcome.Running, 0, "");

        public override bool IsObjectiveReached(GameDataSO gameData, out Domains winner)
        {
            winner = ResolveWinner(gameData);
            return _outcome != ShivaOutcome.Running;
        }

        public override Domains ResolveWinner(GameDataSO gameData) =>
            _outcome == ShivaOutcome.Won ? TandavaScoringRuleSO.PilotDomain(gameData) : Domains.Blue;

        public override void AssignScores(GameDataSO gameData, Domains winner, float finishTime)
        {
            foreach (var stats in gameData.RoundStatsList)
                stats.Score = ringPoints * stats.SwitchesThreaded + crystalPoints * stats.ElementalCrystalsCollected;
        }

        public override List<ScoreResult> BuildResults(GameDataSO gameData)
        {
            var rows = gameData.RoundStatsList.OrderByDescending(s => s.Score).Select(s => new ScoreResultBuilder.Row(
                s.Name, s.Domain, s.Score,
                TemplateText.Fill(rowDetail, "rings", s.SwitchesThreaded.ToString(), "crystals", s.ElementalCrystalsCollected.ToString()),
                null)).ToList();
            return ScoreResultBuilder.BuildRanked(rows);
        }

        public override ScoreReveal BuildReveal(GameDataSO gameData, IRoundStats localStats, bool didWin)
        {
            bool won = _outcome == ShivaOutcome.Won;
            string label = won ? wonLabel : _outcome == ShivaOutcome.Lost ? TemplateText.Fill(lostLabel, "form", _formName.ToUpperInvariant()) : "";
            return new ScoreReveal(won ? victoryTitle : defeatTitle, label, (int)localStats.Score, false);
        }
    }
}
