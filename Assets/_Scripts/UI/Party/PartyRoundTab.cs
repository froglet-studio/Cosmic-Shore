using System.Collections.Generic;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// UI component for a single round tab in the Party Pause Panel.
    /// Displays game state, winner name, ready count, and per-player scores.
    /// Attached to the PartyPausePanelDataPrefab and instantiated per round.
    /// </summary>
    public class PartyRoundTab : MonoBehaviour
    {
        [Header("Game Details (Left Side)")]
        [Tooltip("Header text showing 'Round N' or 'Round N: GameName'")]
        [SerializeField] TMP_Text gameStateText;
        [Tooltip("The 'WINNER :' label — hidden until round is complete")]
        [SerializeField] TMP_Text winnerLabelText;
        [Tooltip("Displays the winner's username")]
        [SerializeField] TMP_Text winnerNameText;
        [Tooltip("Shows 'PLAYERS READY X/Y' during waiting phases")]
        [SerializeField] TMP_Text readyCountText;

        [Header("Player Scoreboard (Right Side)")]
        [Tooltip("Username texts for each player slot (3 max)")]
        [SerializeField] List<TMP_Text> playerNameTexts = new();
        [Tooltip("Score texts for each player slot")]
        [SerializeField] List<TMP_Text> playerScoreTexts = new();
        [Tooltip("Position texts (1ST, 2ND, 3RD) for each player slot")]
        [SerializeField] List<TMP_Text> playerPositionTexts = new();
        [Tooltip("Profile icon images for each player slot")]
        [SerializeField] List<Image> playerProfileIcons = new();

        [Header("Visual")]
        [Tooltip("Background image used for active/completed/pending highlight")]
        [SerializeField] Image roundHighlight;
        [SerializeField] Color activeRoundColor = new(0.2f, 0.6f, 0.9f, 1f);
        [SerializeField] Color completedRoundColor = new(0.0f, 0.8f, 0.4f, 1f);
        [SerializeField] Color pendingRoundColor = new(0.3f, 0.3f, 0.3f, 0.5f);

        int _roundIndex;
        bool _isCompleted;
        int _totalPlayers;
        int _readyCount;

        public int RoundIndex => _roundIndex;
        public bool IsCompleted => _isCompleted;

        public void Initialize(int roundIndex)
        {
            _roundIndex = roundIndex;
            _isCompleted = false;
            _readyCount = 0;
            _totalPlayers = 0;

            if (gameStateText) gameStateText.text = $"Round {roundIndex + 1}";
            if (winnerLabelText) winnerLabelText.gameObject.SetActive(false);
            if (winnerNameText)
            {
                winnerNameText.text = "";
                winnerNameText.gameObject.SetActive(false);
            }
            if (readyCountText) readyCountText.text = "";

            SetHighlight(pendingRoundColor);
            ClearPlayerDetails();
        }

        /// <summary>
        /// Set as the currently active round (being played or about to be played).
        /// </summary>
        public void SetActive(bool active)
        {
            if (!_isCompleted)
                SetHighlight(active ? activeRoundColor : pendingRoundColor);
        }

        /// <summary>
        /// Update the game state text (e.g., "Randomizing game...", "Crystal Capture").
        /// </summary>
        public void SetGameStateText(string text)
        {
            if (gameStateText) gameStateText.text = text;
        }

        /// <summary>
        /// Initialize player names before a round starts.
        /// </summary>
        public void SetPlayerNames(IReadOnlyList<PartyPlayerState> players)
        {
            _totalPlayers = players.Count;
            _readyCount = 0;

            for (int i = 0; i < playerNameTexts.Count; i++)
            {
                if (i < players.Count)
                {
                    if (playerNameTexts[i]) playerNameTexts[i].text = players[i].PlayerName;
                    if (i < playerScoreTexts.Count && playerScoreTexts[i])
                        playerScoreTexts[i].text = "";
                    if (i < playerPositionTexts.Count && playerPositionTexts[i])
                        playerPositionTexts[i].text = "";
                    if (i < playerProfileIcons.Count && playerProfileIcons[i])
                        playerProfileIcons[i].gameObject.SetActive(true);
                }
                else
                {
                    if (playerNameTexts[i]) playerNameTexts[i].text = "";
                    if (i < playerScoreTexts.Count && playerScoreTexts[i])
                        playerScoreTexts[i].text = "";
                    if (i < playerPositionTexts.Count && playerPositionTexts[i])
                        playerPositionTexts[i].text = "";
                    if (i < playerProfileIcons.Count && playerProfileIcons[i])
                        playerProfileIcons[i].gameObject.SetActive(false);
                }
            }

            UpdateReadyCountText();
        }

        /// <summary>
        /// Set the ready count directly from the server-authoritative count.
        /// </summary>
        public void SetReadyCount(int readyCount, int totalCount)
        {
            _readyCount = readyCount;
            _totalPlayers = totalCount;
            UpdateReadyCountText();
        }

        /// <summary>
        /// Reset ready count when transitioning between ready phases.
        /// </summary>
        public void ResetReadyCount()
        {
            _readyCount = 0;
            UpdateReadyCountText();
        }

        /// <summary>
        /// Display the round result — shows winner, scores, and positions.
        /// </summary>
        public void SetRoundResult(PartyRoundResult result)
        {
            _isCompleted = true;
            SetHighlight(completedRoundColor);

            // Show winner section
            if (winnerLabelText) winnerLabelText.gameObject.SetActive(true);
            if (winnerNameText)
            {
                winnerNameText.gameObject.SetActive(true);
                winnerNameText.text = result.WinnerName;
            }

            // Hide ready count post-round
            if (readyCountText) readyCountText.text = "";

            string modeName = PartyGameController.GetMiniGameDisplayName(result.MiniGameMode);
            if (gameStateText) gameStateText.text = $"Round {_roundIndex + 1}: {modeName}";

            // Show player scores and positions
            for (int i = 0; i < playerNameTexts.Count; i++)
            {
                if (i < result.PlayerScores.Count)
                {
                    var ps = result.PlayerScores[i];
                    if (playerNameTexts[i]) playerNameTexts[i].text = ps.PlayerName;
                    if (i < playerScoreTexts.Count && playerScoreTexts[i])
                        playerScoreTexts[i].text = FormatScore(ps.Score, result.MiniGameMode);
                    if (i < playerPositionTexts.Count && playerPositionTexts[i])
                        playerPositionTexts[i].text = GetPositionSuffix(i + 1);
                }
                else
                {
                    if (playerNameTexts[i]) playerNameTexts[i].text = "";
                    if (i < playerScoreTexts.Count && playerScoreTexts[i])
                        playerScoreTexts[i].text = "";
                    if (i < playerPositionTexts.Count && playerPositionTexts[i])
                        playerPositionTexts[i].text = "";
                }
            }
        }

        void ClearPlayerDetails()
        {
            foreach (var t in playerNameTexts)
                if (t) t.text = "";
            foreach (var t in playerScoreTexts)
                if (t) t.text = "";
            foreach (var t in playerPositionTexts)
                if (t) t.text = "";
        }

        void UpdateReadyCountText()
        {
            if (readyCountText)
                readyCountText.text = _totalPlayers > 0
                    ? $"PLAYERS READY {_readyCount}/{_totalPlayers}"
                    : "";
        }

        void SetHighlight(Color color)
        {
            if (roundHighlight) roundHighlight.color = color;
        }

        static string GetPositionSuffix(int position)
        {
            return position switch
            {
                1 => "1ST",
                2 => "2ND",
                3 => "3RD",
                _ => $"{position}TH",
            };
        }

        static string FormatScore(float score, GameModes mode)
        {
            // Golf-scored modes (lower = better) — show time
            if (mode is GameModes.HexRace or GameModes.MultiplayerJoust)
            {
                if (score >= 10000f) return "DNF";
                int minutes = (int)(score / 60f);
                int seconds = (int)(score % 60f);
                int ms = (int)((score * 100f) % 100f);
                return minutes > 0 ? $"{minutes}:{seconds:D2}.{ms:D2}" : $"{seconds}.{ms:D2}s";
            }

            // Standard scoring — show integer
            return ((int)score).ToString();
        }
    }
}
