using System.Collections.Generic;
using CosmicShore.Game.Arcade.Party;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI.Party
{
    /// <summary>
    /// UI component for a single round tab in the Party Pause Panel.
    /// Displays game state text, winner name, and per-player scores.
    /// Designed as a prefab that gets instantiated for each round.
    /// </summary>
    public class PartyRoundTab : MonoBehaviour
    {
        [Header("Header Section")]
        [SerializeField] TMP_Text gameStateText;
        [SerializeField] TMP_Text winnerNameText;
        [SerializeField] GameObject winnerNameContainer;

        [Header("Player Details")]
        [SerializeField] List<TMP_Text> playerNameTexts = new();
        [SerializeField] List<TMP_Text> playerScoreTexts = new();
        [SerializeField] List<Image> playerReadyIndicators = new();

        [Header("Visual")]
        [SerializeField] Image roundHighlight;
        [SerializeField] Color activeRoundColor = new(0.2f, 0.6f, 0.9f, 1f);
        [SerializeField] Color completedRoundColor = new(0.0f, 0.8f, 0.4f, 1f);
        [SerializeField] Color pendingRoundColor = new(0.3f, 0.3f, 0.3f, 0.5f);

        int _roundIndex;
        bool _isCompleted;

        public void Initialize(int roundIndex)
        {
            _roundIndex = roundIndex;
            _isCompleted = false;

            if (gameStateText) gameStateText.text = $"Round {roundIndex + 1}";
            if (winnerNameContainer) winnerNameContainer.SetActive(false);
            if (winnerNameText) winnerNameText.text = "";

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
            for (int i = 0; i < playerNameTexts.Count; i++)
            {
                if (i < players.Count)
                {
                    if (playerNameTexts[i]) playerNameTexts[i].text = players[i].PlayerName;
                    if (i < playerScoreTexts.Count && playerScoreTexts[i])
                        playerScoreTexts[i].text = "";
                    if (i < playerReadyIndicators.Count && playerReadyIndicators[i])
                        playerReadyIndicators[i].gameObject.SetActive(true);
                }
                else
                {
                    if (playerNameTexts[i]) playerNameTexts[i].text = "";
                    if (i < playerScoreTexts.Count && playerScoreTexts[i])
                        playerScoreTexts[i].text = "";
                    if (i < playerReadyIndicators.Count && playerReadyIndicators[i])
                        playerReadyIndicators[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Update ready indicator for a specific player.
        /// </summary>
        public void SetPlayerReady(string playerName, bool isReady)
        {
            for (int i = 0; i < playerNameTexts.Count; i++)
            {
                if (playerNameTexts[i] && playerNameTexts[i].text == playerName)
                {
                    if (i < playerReadyIndicators.Count && playerReadyIndicators[i])
                    {
                        playerReadyIndicators[i].color = isReady
                            ? new Color(0f, 0.8f, 0.4f)
                            : new Color(0.5f, 0.5f, 0.5f);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Display the round result — replaces player names with scores.
        /// </summary>
        public void SetRoundResult(PartyRoundResult result)
        {
            _isCompleted = true;
            SetHighlight(completedRoundColor);

            if (winnerNameContainer) winnerNameContainer.SetActive(true);
            if (winnerNameText) winnerNameText.text = result.WinnerName;

            string modeName = PartyGameController.GetMiniGameDisplayName(result.MiniGameMode);
            if (gameStateText) gameStateText.text = $"Round {_roundIndex + 1}: {modeName}";

            // Show scores instead of just names
            for (int i = 0; i < playerNameTexts.Count; i++)
            {
                if (i < result.PlayerScores.Count)
                {
                    var ps = result.PlayerScores[i];
                    if (playerNameTexts[i]) playerNameTexts[i].text = ps.PlayerName;
                    if (i < playerScoreTexts.Count && playerScoreTexts[i])
                        playerScoreTexts[i].text = FormatScore(ps.Score, result.MiniGameMode);

                    // Hide ready indicators post-round
                    if (i < playerReadyIndicators.Count && playerReadyIndicators[i])
                        playerReadyIndicators[i].gameObject.SetActive(false);
                }
                else
                {
                    if (playerNameTexts[i]) playerNameTexts[i].text = "";
                    if (i < playerScoreTexts.Count && playerScoreTexts[i])
                        playerScoreTexts[i].text = "";
                }
            }
        }

        void ClearPlayerDetails()
        {
            foreach (var t in playerNameTexts)
                if (t) t.text = "";
            foreach (var t in playerScoreTexts)
                if (t) t.text = "";
            foreach (var img in playerReadyIndicators)
                if (img) img.gameObject.SetActive(false);
        }

        void SetHighlight(Color color)
        {
            if (roundHighlight) roundHighlight.color = color;
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
