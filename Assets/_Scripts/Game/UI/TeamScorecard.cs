using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    /// <summary>
    /// UI component for a single team's score card on the end-game scoreboard.
    /// Displays team name, team score, domain-colored background, and up to 2 player entries.
    /// Attach to each TeamScorecard GameObject in the MultiplayerView hierarchy.
    /// </summary>
    public class TeamScorecard : MonoBehaviour
    {
        [Header("Team Header")]
        [SerializeField] private TMP_Text teamNameText;
        [SerializeField] private TMP_Text teamScoreText;
        [SerializeField] private Image teamNameBG;

        [Header("Player Entries")]
        [SerializeField] private PlayerScoreEntry player1Entry;
        [SerializeField] private PlayerScoreEntry player2Entry;

        /// <summary>
        /// Populates the team scorecard with team and player data.
        /// </summary>
        /// <param name="teamName">Display name for the domain (e.g. "JADE", "RUBY").</param>
        /// <param name="teamScore">Formatted team score string.</param>
        /// <param name="domainColor">Color for the team name background.</param>
        /// <param name="players">Player data for this team (name, formatted score, optional avatar).</param>
        public void Populate(string teamName, string teamScore, Color domainColor, List<PlayerDisplayData> players)
        {
            if (teamNameText) teamNameText.text = teamName;
            if (teamScoreText) teamScoreText.text = teamScore;
            if (teamNameBG) teamNameBG.color = domainColor;

            // Player 1
            if (player1Entry)
            {
                if (players is { Count: > 0 })
                {
                    player1Entry.Show(true);
                    player1Entry.Populate(players[0].Name, players[0].Score, players[0].Avatar);
                }
                else
                {
                    player1Entry.Show(false);
                }
            }

            // Player 2
            if (player2Entry)
            {
                if (players is { Count: > 1 })
                {
                    player2Entry.Show(true);
                    player2Entry.Populate(players[1].Name, players[1].Score, players[1].Avatar);
                }
                else
                {
                    player2Entry.Show(false);
                }
            }
        }

        public void Show(bool visible) => gameObject.SetActive(visible);
    }

    /// <summary>
    /// Simple data container for passing player display info to TeamScorecard.
    /// </summary>
    public struct PlayerDisplayData
    {
        public string Name;
        public string Score;
        public Sprite Avatar;
    }
}
