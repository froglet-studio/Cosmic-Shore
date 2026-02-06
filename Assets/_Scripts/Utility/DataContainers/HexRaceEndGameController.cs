using System.Collections;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class HexRaceEndGameController : EndGameCinematicController
    {
        [Header("Hex Race Settings")]
        [SerializeField] float penaltyScoreBase = 10000f;

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            // [Visual Note] Get Raw Score (Time OR Penalty+Crystals)
            gameData.IsLocalDomainWinner(out DomainStats stats);
            float rawScore = stats.Score;

            bool isWin = rawScore < penaltyScoreBase;
            int displayValue;
            string labelText;
            bool formatAsTime;

            if (isWin)
            {
                // [Visual Note] WIN CASE: Display Time
                // Score IS the time.
                displayValue = Mathf.FloorToInt(rawScore); 
                labelText = "RACE TIME";
                formatAsTime = true;
                
                // Fetch Personal Best Time from UGS
                float bestTime = UGSStatsManager.Instance ? UGSStatsManager.Instance.GetBestRaceTime() : 0f;
                
                // If bestTime is 0, it means no previous record, so current is best
                if (bestTime <= 0.01f) bestTime = rawScore;
                
                // Update UI HighScore text manually if View allows, 
                // otherwise View uses 'displayValue' for both fields in generic implementation.
            }
            else
            {
                // [Visual Note] LOSS CASE: Display Crystals Remaining
                // Raw = 10005 -> Remaining = 5
                displayValue = Mathf.FloorToInt(rawScore - penaltyScoreBase);
                labelText = "CRYSTALS LEFT";
                formatAsTime = false;
            }

            // [Visual Note] Override the cinematic text logic
            string finalLabel = isWin ? cinematic.GetCinematicTextForScore(100) : "OUT OF FUEL";

            yield return view.PlayScoreRevealAnimation(
                finalLabel + $"\n<size=60%>{labelText}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime // Pass the formatting flag
            );
        }
    }
}