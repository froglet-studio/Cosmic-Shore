using CosmicShore.Game.Cinematics;
using CosmicShore.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade.Party
{
    /// <summary>
    /// Overrides the end-game cinematic flow for party mode.
    /// Runs the cinematic but skips the scoreboard — the party pause panel
    /// handles result display instead.
    /// At the final round, XP is shown in the cinematic.
    /// </summary>
    public class PartyEndGameHandler : EndGameCinematicController
    {
        [Header("Party")]
        [SerializeField] PartyGameController partyController;

        bool IsFinalRound => partyController != null &&
                             partyController.CurrentRound + 1 >= partyController.TotalRounds;

        protected override void OnWinnerCalculated()
        {
            if (isRunning) return;
            isRunning = true;

            // Only award XP on the final round
            if (IsFinalRound)
            {
                var xpService = Game.XP.XPRewardService.Instance;
                if (xpService != null)
                {
                    int xp = xpService.AwardXP();
                    CSDebug.Log($"[PartyEndGame] Final round XP awarded: {xp}");
                }
            }

            var localPlayer = gameData.LocalPlayer;
            if (localPlayer?.Vessel?.VesselStatus != null)
                cachedBoostMultiplier = localPlayer.Vessel.VesselStatus.BoostMultiplier;

            var cinematic = ResolveCinematicForThisScene();
            runningRoutine = StartCoroutine(RunPartyEndGameSequence(cinematic));
        }

        System.Collections.IEnumerator RunPartyEndGameSequence(CinematicDefinitionSO cinematic)
        {
            localPlayerWon = DetermineLocalPlayerWon();

            // Run victory lap if configured
            if (cinematic && cinematic.enableVictoryLap)
                yield return StartCoroutine(RunVictoryLap(cinematic));

            // Set local vessel to AI during cinematic
            if (cinematic && cinematic.setLocalVesselToAI)
                SetLocalVesselAI(true, cinematic.aiCinematicBehavior);

            // Camera sequence
            if (cinematic && cinematic.cameraSetups is { Count: > 0 })
                yield return StartCoroutine(RunCameraSequence(cinematic));
            else
            {
                var delay = cinematic ? cinematic.delayBeforeEndScreen : 0.1f;
                yield return new WaitForSeconds(delay);
            }

            // Score reveal
            yield return StartCoroutine(PlayScoreRevealSequence(cinematic));

            // Only show XP on the final round
            if (IsFinalRound && view)
                view.ShowXPEarned();

            // Skip the continue button and connecting panel for non-final rounds
            // The party controller handles the transition
            if (!IsFinalRound)
            {
                // Brief pause then reset
                yield return new WaitForSeconds(1f);
                ResetGameForNewRound();

                if (view)
                {
                    view.HideXPEarned();
                    view.HideScoreRevealPanel();
                }

                // Do NOT invoke OnShowGameEndScreen — the party controller
                // handles showing the party panel instead of the normal scoreboard
            }
            else
            {
                // Final round — show continue button and then the final scoreboard
                if (view)
                {
                    view.ShowContinueButton();
                    yield return new WaitUntil(() => !view.IsContinueButtonActive());
                }

                if (view && cinematic)
                {
                    view.ShowConnectingPanel();
                    yield return new WaitForSeconds(cinematic.connectingPanelDuration);
                    ResetGameForNewRound();
                }

                if (view)
                {
                    view.HideXPEarned();
                    view.HideScoreRevealPanel();
                }

                // For final round, show the full scoreboard
                gameData.InvokeShowGameEndScreen();
            }

            runningRoutine = null;
            isRunning = false;
        }
    }
}
