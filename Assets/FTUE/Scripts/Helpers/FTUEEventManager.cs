using System;
using CosmicShore.Core;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Central hub for FTUE-related events.
    /// </summary>
    public static class FTUEEventManager
    {
        /// <summary>
        /// Fired when the player clicks �Next� on any FTUE step.
        /// </summary>
        public static event Action OnNextPressed;
        public static void RaiseNextPressed() => OnNextPressed?.Invoke();

        /// <summary>
        /// Fired when the player enters a game mode.
        /// Is only fired if the user has not completed the FTUE.
        /// </summary>
        public static event Action<GameModes> OnGameModeStarted;
        public static void RaiseGameModeStarted(GameModes mode)
            => OnGameModeStarted?.Invoke(mode);

        /// <summary>
        /// Fired two times. Once when a user enters the game for the first time.
        /// Second, when the user starts Phase 3 of the FTUE.
        /// We can add more here in the future.
        /// </summary>
        public static event Action InitializeFTUE;
        public static void OnInitializeFTUECalled()
            => InitializeFTUE?.Invoke();

    }
}
