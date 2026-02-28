namespace CosmicShore.Data
{
    /// <summary>
    /// Sub-states for the Menu_Main scene lifecycle, driven by
    /// <see cref="Core.MainMenuController"/>.
    ///
    /// These track the menu's internal readiness progression while the
    /// top-level <see cref="ApplicationState"/> remains <c>MainMenu</c>.
    /// </summary>
    [System.Serializable]
    public enum MainMenuState
    {
        /// <summary>Scene loaded but not yet initialized.</summary>
        None = 0,

        /// <summary>Game data configured, waiting for network vessel spawn.</summary>
        Initializing = 1,

        /// <summary>Autopilot vessel spawned, menu fully interactive.</summary>
        Ready = 2,

        /// <summary>Player selected a game mode — transitioning out of menu.</summary>
        LaunchingGame = 3,

        /// <summary>
        /// Local player is in freestyle mode — controlling their vessel directly
        /// while remaining in the Menu_Main scene. Other players may independently
        /// be in Ready or Freestyle state on their own clients.
        /// </summary>
        Freestyle = 4,
    }
}
