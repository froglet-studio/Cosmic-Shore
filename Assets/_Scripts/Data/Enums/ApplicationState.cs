namespace CosmicShore.Data
{
    /// <summary>
    /// Top-level application phases. Driven exclusively by
    /// <see cref="Core.ApplicationStateMachine"/> (single-writer pattern).
    /// </summary>
    [System.Serializable]
    public enum ApplicationState
    {
        None            = 0,
        Bootstrapping   = 1,
        Authenticating  = 2,
        MainMenu        = 3,
        LoadingGame     = 4,
        InGame          = 5,
        GameOver        = 6,
        Paused          = 7,
        Disconnected    = 8,
        ShuttingDown    = 9,
    }
}
