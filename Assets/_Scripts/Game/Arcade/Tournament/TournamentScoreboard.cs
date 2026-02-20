using System.Collections.Generic;
using System.Text;
using CosmicShore.Core;
using CosmicShore.Soap;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.Arcade.Tournament
{
    /// <summary>
    /// Tournament-specific scoreboard overlay that appears after each game
    /// in a tournament series. Shows the round result, running game-win tally,
    /// upcoming schedule, and a "Next Game" / "View Results" button.
    ///
    /// Attach this component in each multiplayer game scene that participates
    /// in tournaments. It caches round results from the TournamentManager and
    /// displays them when the end-game screen is shown (after the cinematic).
    /// </summary>
    public class TournamentScoreboard : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;

        [Header("Root Panel")]
        [SerializeField] GameObject tournamentPanel;

        [Header("Round Result")]
        [SerializeField] TMP_Text roundHeaderText;
        [SerializeField] TMP_Text roundResultText;

        [Header("Game Win Tally")]
        [SerializeField] TMP_Text tallyText;

        [Header("Schedule")]
        [SerializeField] TMP_Text scheduleText;

        [Header("Final Result")]
        [SerializeField] GameObject finalResultPanel;
        [SerializeField] TMP_Text finalResultHeaderText;
        [SerializeField] TMP_Text finalResultDetailText;

        [Header("Buttons")]
        [SerializeField] Button nextGameButton;
        [SerializeField] TMP_Text nextGameButtonText;
        [SerializeField] Button returnToMenuButton;

        TournamentManager _tournament;

        // Cached results — populated when TournamentManager fires events,
        // but only displayed when the end-game screen is shown.
        TournamentRoundResult? _pendingRoundResult;
        TournamentFinalResult? _pendingFinalResult;

        void OnEnable()
        {
            _tournament = TournamentManager.Instance;
            if (_tournament == null) return;

            _tournament.OnRoundComplete += HandleRoundComplete;
            _tournament.OnTournamentComplete += HandleTournamentComplete;

            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += HandleShowGameEndScreen;

            if (nextGameButton)
                nextGameButton.onClick.AddListener(OnNextGameClicked);
            if (returnToMenuButton)
                returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);

            HideAll();
        }

        void OnDisable()
        {
            if (_tournament != null)
            {
                _tournament.OnRoundComplete -= HandleRoundComplete;
                _tournament.OnTournamentComplete -= HandleTournamentComplete;
            }

            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= HandleShowGameEndScreen;

            if (nextGameButton)
                nextGameButton.onClick.RemoveListener(OnNextGameClicked);
            if (returnToMenuButton)
                returnToMenuButton.onClick.RemoveListener(OnReturnToMenuClicked);
        }

        void HideAll()
        {
            if (tournamentPanel) tournamentPanel.SetActive(false);
            if (finalResultPanel) finalResultPanel.SetActive(false);
        }

        /// <summary>
        /// Called by TournamentManager when a round ends. Caches the result
        /// but does NOT show the panel yet (cinematic may still be playing).
        /// </summary>
        void HandleRoundComplete(TournamentRoundResult result)
        {
            _pendingRoundResult = result;
        }

        /// <summary>
        /// Called by TournamentManager when the tournament is over. Caches the result.
        /// </summary>
        void HandleTournamentComplete(TournamentFinalResult result)
        {
            _pendingFinalResult = result;
        }

        /// <summary>
        /// Called when the end-game screen is shown (after cinematic).
        /// Now we display the tournament scoreboard.
        /// </summary>
        void HandleShowGameEndScreen()
        {
            if (_tournament == null || (!_tournament.IsTournamentActive && _pendingFinalResult == null))
                return;

            if (_pendingFinalResult.HasValue)
                DisplayTournamentComplete(_pendingFinalResult.Value);
            else if (_pendingRoundResult.HasValue)
                DisplayRoundComplete(_pendingRoundResult.Value);

            _pendingRoundResult = null;
            _pendingFinalResult = null;
        }

        void DisplayRoundComplete(TournamentRoundResult result)
        {
            if (tournamentPanel) tournamentPanel.SetActive(true);
            if (finalResultPanel) finalResultPanel.SetActive(false);

            if (roundHeaderText)
                roundHeaderText.text = $"GAME {result.GameIndex + 1} OF {TournamentManager.TotalGames}";

            if (roundResultText)
            {
                string gameName = _tournament.GetGameDisplayName(result.GameMode);
                roundResultText.text = $"{gameName} (Intensity {result.Intensity})\nWinner: <b>{result.WinnerName}</b> ({result.WinnerDomain})";
            }

            if (tallyText)
                tallyText.text = BuildTallyString();

            if (scheduleText)
                scheduleText.text = BuildScheduleString();

            bool hasMoreGames = _tournament.CurrentGameIndex < TournamentManager.TotalGames;
            if (nextGameButton)
            {
                nextGameButton.gameObject.SetActive(hasMoreGames);
                if (nextGameButtonText)
                {
                    int nextIdx = _tournament.CurrentGameIndex;
                    if (nextIdx < TournamentManager.TotalGames)
                    {
                        string nextGameName = _tournament.GetGameDisplayName(_tournament.ScheduledGames[nextIdx]);
                        nextGameButtonText.text = $"Next: {nextGameName}";
                    }
                }
            }

            if (returnToMenuButton)
                returnToMenuButton.gameObject.SetActive(!hasMoreGames);
        }

        void DisplayTournamentComplete(TournamentFinalResult result)
        {
            if (tournamentPanel) tournamentPanel.SetActive(true);
            if (finalResultPanel) finalResultPanel.SetActive(true);

            bool localWon = false;
            if (gameData != null && gameData.LocalPlayer != null)
                localWon = gameData.LocalPlayer.Domain == result.WinnerDomain;

            if (finalResultHeaderText)
                finalResultHeaderText.text = localWon ? "TOURNAMENT VICTORY!" : "TOURNAMENT OVER";

            if (finalResultDetailText)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"<b>{result.WinnerDomain}</b> wins with {result.WinnerWins} game{(result.WinnerWins != 1 ? "s" : "")}!\n");

                sb.AppendLine("<b>Final Standings:</b>");
                foreach (var kvp in result.DomainWins)
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value} win{(kvp.Value != 1 ? "s" : "")}");

                sb.AppendLine("\n<b>Round-by-Round:</b>");
                foreach (var round in result.RoundResults)
                {
                    string gameName = _tournament != null
                        ? _tournament.GetGameDisplayName(round.GameMode)
                        : round.GameMode.ToString();
                    sb.AppendLine($"  Game {round.GameIndex + 1}: {gameName} \u2014 {round.WinnerName} ({round.WinnerDomain})");
                }

                finalResultDetailText.text = sb.ToString();
            }

            if (tallyText)
                tallyText.text = BuildTallyString();

            if (roundHeaderText)
                roundHeaderText.text = "TOURNAMENT COMPLETE";
            if (roundResultText)
                roundResultText.text = "";

            if (nextGameButton)
                nextGameButton.gameObject.SetActive(false);
            if (returnToMenuButton)
                returnToMenuButton.gameObject.SetActive(true);
        }

        void OnNextGameClicked()
        {
            HideAll();
            _tournament?.AdvanceToNextGame();
        }

        void OnReturnToMenuClicked()
        {
            HideAll();
            _tournament?.CancelTournament();

            var gameManager = FindAnyObjectByType<GameManager>();
            if (gameManager != null)
                gameManager.ReturnToMainMenu();
        }

        string BuildTallyString()
        {
            if (_tournament == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine("<b>Game Wins:</b>");
            foreach (var kvp in _tournament.DomainWins)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");

            return sb.ToString();
        }

        string BuildScheduleString()
        {
            if (_tournament == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine("<b>Schedule:</b>");
            for (int i = 0; i < TournamentManager.TotalGames; i++)
            {
                string gameName = _tournament.GetGameDisplayName(_tournament.ScheduledGames[i]);
                string prefix;
                if (i < _tournament.CurrentGameIndex)
                    prefix = "\u2713"; // checkmark
                else if (i == _tournament.CurrentGameIndex)
                    prefix = "\u25B6"; // play arrow
                else
                    prefix = "\u25CB"; // circle

                sb.AppendLine($"  {prefix} Game {i + 1}: {gameName} (I{_tournament.ScheduledIntensities[i]})");
            }

            return sb.ToString();
        }
    }
}
