namespace CosmicShore.Data
{
    /// <summary>
    /// The measured milestones of a boot, in the order the bar counts them. A status raised on the
    /// <c>Event_BootStatusRequest</c> channel names the step that has just STARTED
    /// (<see cref="BootStatusRequest.Step"/>), which is also the news that every step before it is done:
    /// <c>BootLoadProgress</c> turns that into the splash bar's percentage. <see cref="None"/> is a status
    /// that is no milestone (a notice, a hide, a retry).
    /// </summary>
    [System.Serializable]
    public enum BootStep
    {
        None            = 0,
        Connecting      = 1,
        SigningIn       = 2,
        LoadingProfile  = 3,
        JoiningLobby    = 4,
        CreatingSession = 5,
        HostReady       = 6,
    }
}
