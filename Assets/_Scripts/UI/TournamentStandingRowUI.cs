using CosmicShore.Data;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Single row in the tournament standings table.
    /// Displays rank, player name, domain, and cumulative points.
    /// </summary>
    public class TournamentStandingRowUI : MonoBehaviour
    {
        [SerializeField] TMP_Text rankText;
        [SerializeField] TMP_Text playerNameText;
        [SerializeField] TMP_Text totalPointsText;
        [SerializeField] TMP_Text pointsBreakdownText;

        public void Initialize(int rank, TournamentStanding standing)
        {
            if (rankText) rankText.text = $"#{rank}";
            if (playerNameText) playerNameText.text = standing.PlayerName;
            if (totalPointsText) totalPointsText.text = $"{standing.TotalPoints} pts";

            if (pointsBreakdownText && standing.PointsPerRound != null)
                pointsBreakdownText.text = string.Join(" | ", standing.PointsPerRound);
        }
    }
}
