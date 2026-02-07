using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerHexRaceEndGameController : HexRaceEndGameController
    {
        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();

            // Get LOCAL PLAYER'S score
            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localPlayerName);
            
            if (localStats == null)
            {
                UnityEngine.Debug.LogError("[MP HexRace EndGame] Could not find local player stats!");
                yield break;
            }

            // Determine winner (lowest score)
            var sortedStats = gameData.RoundStatsList.OrderBy(s => s.Score).ToList();
            var winnerStats = sortedStats[0];
            bool isLocalPlayerWinner = localStats.Name == winnerStats.Name;

            // Get LOCAL player's score
            float rawScore = localStats.Score;
            bool localPlayerFinished = rawScore < 10000f;
            
            var headerText = isLocalPlayerWinner ? "VICTORY" : "DEFEAT";

            // FIX: Show time for anyone who finished, crystals left for those who didn't
            string label;
            int value;
            bool formatAsTime;

            if (localPlayerFinished)
            {
                // Player finished - show their completion time
                label = isLocalPlayerWinner ? "WINNER TIME" : "YOUR TIME";
                value = (int)rawScore;
                formatAsTime = true;
            }
            else
            {
                // Player didn't finish - show crystals remaining
                label = "CRYSTALS LEFT";
                value = (int)(rawScore - 10000f);
                formatAsTime = false;
            }

            yield return view.PlayScoreRevealAnimation(
                headerText + $"\n<size=60%>{label}</size>",
                value,
                cinematic.scoreRevealSettings,
                formatAsTime
            );
        }
    }
}