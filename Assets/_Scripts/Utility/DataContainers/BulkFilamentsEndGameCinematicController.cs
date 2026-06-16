using System.Collections;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Utility
{
    public class BulkFilamentsEndGameCinematicController : EndGameCinematicController
    {
        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();
            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.ScoreReveal);

            gameData.IsLocalDomainWinner(out DomainStats stats);
            int displayScore = Mathf.Max(0, Mathf.RoundToInt(stats.Score));
            string displayText = cinematic.GetCinematicTextForScore(displayScore);

            yield return view.PlayScoreRevealAnimation(
                displayText,
                displayScore,
                cinematic.scoreRevealSettings,
                formatAsTime: true
            );
        }
    }
}
