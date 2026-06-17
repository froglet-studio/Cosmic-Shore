using System.Collections;
using System.Text;
using CosmicShore.Utility;
using Obvious.Soap;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// UI view for the Tournament scene, which serves two roles (the persistent
    /// <see cref="TournamentController"/> decides which via its phase):
    ///
    ///   • <b>Lobby / intro</b> (fresh start) — shows the game lineup; on the host, advances into
    ///     the first game (auto after <see cref="autoStartDelaySeconds"/> or via <see cref="hostStartButton"/>).
    ///   • <b>Summary</b> (after the last game's Continue) — shows all results, with host-only
    ///     <see cref="playAgainButton"/> (restart the whole tournament) and <see cref="mainMenuButton"/>
    ///     (take the party back to the lava-lamp menu).
    ///
    /// Runs on every peer; only the host drives transitions — clients follow the Single load.
    /// All buttons are host-only.
    /// </summary>
    public class TournamentSceneView : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;
        [SerializeField] TournamentDataSO tournamentData;

        [Header("Shared")]
        [SerializeField] TMP_Text titleText;

        [Header("Lobby (intro)")]
        [Tooltip("Optional root for the lobby/intro layout — toggled on for a fresh start.")]
        [SerializeField] GameObject lobbyRoot;
        [Tooltip("Shows the ordered game lineup.")]
        [SerializeField] TMP_Text lineupText;
        [Tooltip("Optional host-only Start button. If wired, the host taps it to begin; otherwise " +
                 "the host auto-starts after autoStartDelaySeconds.")]
        [SerializeField] GameObject hostStartButton;
        [Tooltip("Seconds the lobby is shown before the host auto-advances to game 1 (no Start button).")]
        [SerializeField, Min(0f)] float autoStartDelaySeconds = 3f;

        [Header("Summary (results)")]
        [Tooltip("Optional root for the results layout — toggled on after the last game.")]
        [SerializeField] GameObject summaryRoot;
        [Tooltip("Displays the final standings + per-game results.")]
        [SerializeField] TMP_Text resultsText;
        [Tooltip("Host-only. Restarts the whole tournament. Wire onClick → OnPlayAgainPressed().")]
        [SerializeField] GameObject playAgainButton;
        [Tooltip("Host-only. Returns the party to the lava-lamp menu. Wire onClick → OnMainMenuPressed().")]
        [SerializeField] GameObject mainMenuButton;
        [Tooltip("The shared main-menu SOAP event (same asset the Scoreboard's Main Menu uses, " +
                 "that SceneLoader listens to). Raised by Main Menu to return everyone to Menu_Main.")]
        [SerializeField] ScriptableEventNoParam onClickToMainMenu;

        bool IsHost => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        bool HasStartButtonFlow => hostStartButton != null;

        void Start()
        {
            // Lift the loading splash that the Single load left opaque (no vessel here to raise
            // OnClientReady). SceneLoader.FadeFromSplashOnReady was armed by LaunchGame.
            if (gameData != null)
                gameData.InvokeClientReady();

            bool summary = TournamentController.Instance != null && TournamentController.Instance.IsShowingSummary;
            if (summary)
                ShowSummary();
            else
                ShowLobby();
        }

        // ── Lobby ────────────────────────────────────────────────────────────────

        void ShowLobby()
        {
            if (lobbyRoot) lobbyRoot.SetActive(true);
            if (summaryRoot) summaryRoot.SetActive(false);
            if (playAgainButton) playAgainButton.SetActive(false);
            if (mainMenuButton) mainMenuButton.SetActive(false);

            RenderLineup();

            if (hostStartButton) hostStartButton.SetActive(IsHost && HasStartButtonFlow);
            if (IsHost && !HasStartButtonFlow)
                StartCoroutine(AutoStartRoutine());
        }

        IEnumerator AutoStartRoutine()
        {
            yield return new WaitForSecondsRealtime(autoStartDelaySeconds);
            BeginFirstGame();
        }

        void RenderLineup()
        {
            if (tournamentData == null) return;
            if (titleText) titleText.text = tournamentData.ModeName.ToUpperInvariant();
            if (lineupText == null) return;

            // The lineup is RANDOM (a random pool game + random intensity each round), so show the
            // pool + the race rule, not a fixed "Game 1/2/3" order.
            var sb = new StringBuilder();
            sb.AppendLine($"First domain to {tournamentData.WinTarget} wins!");
            sb.AppendLine();
            sb.AppendLine("<b>Random rotation</b>");
            for (int i = 0; i < tournamentData.GameQueue.Count; i++)
            {
                var game = tournamentData.GameQueue[i];
                if (game != null) sb.AppendLine($"• {game.DisplayName}");
            }
            lineupText.text = sb.ToString().TrimEnd();
        }

        /// <summary>Host entry point — wire <see cref="hostStartButton"/>'s onClick here if using a button.</summary>
        public void OnHostStartPressed() => BeginFirstGame();

        void BeginFirstGame()
        {
            if (!IsHost) return;
            if (TournamentController.Instance != null)
                TournamentController.Instance.BeginFirstGame();
            else
                CSDebug.LogError("[TournamentSceneView] TournamentController.Instance is null — cannot begin the tournament.");
        }

        // ── Summary (results) ──────────────────────────────────────────────────────

        void ShowSummary()
        {
            if (summaryRoot) summaryRoot.SetActive(true);
            if (lobbyRoot) lobbyRoot.SetActive(false);
            if (hostStartButton) hostStartButton.SetActive(false);

            RenderSummary();

            // Buttons are host-only; clients just follow the host's choice.
            if (playAgainButton) playAgainButton.SetActive(IsHost);
            if (mainMenuButton) mainMenuButton.SetActive(IsHost);
        }

        void RenderSummary()
        {
            if (tournamentData == null) return;
            if (titleText) titleText.text = $"{tournamentData.ModeName.ToUpperInvariant()} RESULTS";
            if (resultsText) resultsText.text = TournamentStandingsFormatter.FormatFinal(tournamentData);
        }

        /// <summary>Host-only. Wire the summary Play Again button's onClick here. Restarts the tournament.</summary>
        public void OnPlayAgainPressed()
        {
            if (!IsHost) return;
            if (TournamentController.Instance != null)
                TournamentController.Instance.RestartTournament();
            else
                CSDebug.LogError("[TournamentSceneView] TournamentController.Instance is null — cannot restart.");
        }

        /// <summary>Host-only. Wire the summary Main Menu button's onClick here. Returns everyone to Menu_Main.</summary>
        public void OnMainMenuPressed()
        {
            if (!IsHost) return;
            if (onClickToMainMenu != null)
                onClickToMainMenu.Raise();   // → SceneLoader.ReturnToMainMenu (host loads, clients follow)
            else
                CSDebug.LogError("[TournamentSceneView] onClickToMainMenu event not wired — cannot return to menu.");
        }
    }
}
