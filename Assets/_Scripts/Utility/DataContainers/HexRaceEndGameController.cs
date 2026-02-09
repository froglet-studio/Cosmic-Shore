using System.Collections;
using CosmicShore.Core;
using CosmicShore.Game.Analytics;
using CosmicShore.Game.Cinematics;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class HexRaceEndGameController : EndGameCinematicController
    {
        private const float PenaltyScoreBase = 10000f;

        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            gameData.IsLocalDomainWinner(out DomainStats stats);
            float rawScore = stats.Score;

            bool isWin = rawScore < PenaltyScoreBase;
            int displayValue;
            string labelText;
            bool formatAsTime;

            if (isWin)
            {
                displayValue = Mathf.FloorToInt(rawScore); 
                labelText = "RACE TIME";
                formatAsTime = true;
                if (UGSStatsManager.Instance && System.Enum.TryParse(gameData.GameMode.ToString(), out GameModes modeEnum))
                {
                    float bestTime = UGSStatsManager.Instance.GetEvaluatedHighScore(
                        modeEnum, 
                        gameData.SelectedIntensity.Value, 
                        rawScore
                    );
                    if (bestTime <= 0.01f) bestTime = rawScore;
                }
            }
            else
            {
                // LOSS: Show Crystals
                displayValue = Mathf.FloorToInt(rawScore - PenaltyScoreBase);
                labelText = "CRYSTALS LEFT";
                formatAsTime = false;
            }

            string finalLabel = isWin ? cinematic.GetCinematicTextForScore(100) : "OUT OF FUEL";

            yield return view.PlayScoreRevealAnimation(
                finalLabel + $"\n<size=60%>{labelText}</size>",
                displayValue,
                cinematic.scoreRevealSettings,
                formatAsTime 
            );
        }
    }
}