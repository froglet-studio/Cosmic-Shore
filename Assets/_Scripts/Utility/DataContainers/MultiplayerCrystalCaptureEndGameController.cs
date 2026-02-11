using System.Collections;
using System.Linq;
using CosmicShore.Game.Cinematics;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCrystalCaptureEndGameController : EndGameCinematicController
    {
        protected override IEnumerator PlayScoreRevealSequence(CinematicDefinitionSO cinematic)
        {
            if (!view || !cinematic) yield break;

            view.ShowScoreRevealPanel();
            view.HideContinueButton();
    
            var localPlayerName = gameData.LocalPlayer?.Vessel?.VesselStatus?.PlayerName;
            var localStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == localPlayerName);

            if (localStats == null) yield break;

            bool isWinner = gameData.IsLocalDomainWinner(out _);
            string headerText = isWinner ? "VICTORY" : "DEFEAT";
            int crystalsCollected = (int)localStats.Score;

            // [Visual Note] Screen overlay: Header text translates upward while the crystal count integer increments rapidly from 0 to target value.
            yield return view.PlayScoreRevealAnimation(
                headerText + "\n<size=60%>CRYSTALS COLLECTED</size>",
                crystalsCollected,
                cinematic.scoreRevealSettings,
                false
            );
        }
    }
}