using CosmicShore.Core;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Tournament
{
    /// <summary>
    /// Listens for the start-game-requested event and intercepts tournament launches.
    /// When the selected game mode is Tournament, this launches the tournament flow
    /// via Arcade.LaunchTournament() instead of the normal LaunchArcadeGame().
    ///
    /// Place this component on the same GameObject as the ArcadeGameConfigureModal
    /// or on a persistent manager, and wire the same startGameRequestedEvent.
    /// </summary>
    public class TournamentGameLauncher : MonoBehaviour
    {
        [SerializeField] ArcadeGameConfigSO config;
        [SerializeField] GameDataSO gameData;
        [SerializeField] ScriptableEventNoParam startGameRequestedEvent;

        void OnEnable()
        {
            if (startGameRequestedEvent != null)
                startGameRequestedEvent.OnRaised += HandleStartGameRequested;
        }

        void OnDisable()
        {
            if (startGameRequestedEvent != null)
                startGameRequestedEvent.OnRaised -= HandleStartGameRequested;
        }

        void HandleStartGameRequested()
        {
            if (config == null || config.SelectedGame == null)
                return;

            if (config.SelectedGame.Mode != GameModes.Tournament)
                return;

            // Intercept: launch tournament instead of regular game
            int maxIntensity = config.Intensity > 0 ? config.Intensity : 1;

            Debug.Log($"[TournamentLauncher] Starting tournament with max intensity {maxIntensity}");
            Arcade.Instance.LaunchTournament(maxIntensity);
        }
    }
}
