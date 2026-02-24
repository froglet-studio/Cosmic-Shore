using CosmicShore.Game.Cinematics;
using CosmicShore.Game.UI.Party;
using CosmicShore.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Arcade.Party
{
    /// <summary>
    /// Overrides the end-game cinematic flow for party mode.
    /// After each round: cinematic → score reveal → party pause panel (Ready button).
    /// After the final round: cinematic → score reveal → party pause panel (Next button → PartyScoreboard).
    /// XP is only awarded on the final round.
    /// </summary>
    public class PartyEndGameHandler : EndGameCinematicController
    {
        [Header("Party")]
        [SerializeField] PartyGameController partyController;
        [SerializeField] PartyPausePanel partyPausePanel;

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

            // Score reveal animation
            yield return StartCoroutine(PlayScoreRevealSequence(cinematic));

            // Show XP only on the final round
            if (IsFinalRound && view)
                view.ShowXPEarned();

            // Show continue button, wait for tap
            if (view)
            {
                view.ShowContinueButton();
                yield return new WaitUntil(() => !view.IsContinueButtonActive());
            }

            // Reset game state and clean up cinematic UI
            ResetGameForNewRound();

            if (view)
            {
                view.HideXPEarned();
                view.HideScoreRevealPanel();
            }

            // Show party pause panel — it will display either "Ready" (mid-party)
            // or "Next" (final round) based on the phase set by PartyGameController
            if (partyPausePanel)
                partyPausePanel.ForceShow();

            runningRoutine = null;
            isRunning = false;
        }
    }
}
