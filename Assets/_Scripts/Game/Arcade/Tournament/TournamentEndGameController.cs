using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Tournament
{
    /// <summary>
    /// Place this component in any game scene that participates in tournaments.
    /// When a tournament is active, it hides the normal "Play Again" / return-to-menu
    /// buttons so the player navigates via the TournamentScoreboard UI instead.
    /// </summary>
    public class TournamentEndGameController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameDataSO gameData;

        [Header("Regular Scoreboard (to hide during tournament)")]
        [Tooltip("The entire regular scoreboard panel (hidden so tournament scoreboard takes over)")]
        [SerializeField] GameObject regularScoreboardPanel;
        [Tooltip("Optional: buttons container if separate from panel")]
        [SerializeField] GameObject regularScoreboardButtons;

        void OnEnable()
        {
            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised += HandleShowGameEndScreen;
        }

        void OnDisable()
        {
            if (gameData?.OnShowGameEndScreen != null)
                gameData.OnShowGameEndScreen.OnRaised -= HandleShowGameEndScreen;
        }

        void HandleShowGameEndScreen()
        {
            if (TournamentManager.Instance == null || !TournamentManager.Instance.IsTournamentActive)
                return;

            if (regularScoreboardPanel)
                regularScoreboardPanel.SetActive(false);
            if (regularScoreboardButtons)
                regularScoreboardButtons.SetActive(false);
        }
    }
}
