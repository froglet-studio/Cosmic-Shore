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

            if (regularScoreboardButtons)
                regularScoreboardButtons.SetActive(false);
        }
    }
}
