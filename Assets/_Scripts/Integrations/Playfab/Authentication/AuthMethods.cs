namespace CosmicShore.Integrations.PlayFab.EventModels
{
    /// <summary>
    /// Authentication methods
    /// Authentication methods references: https://api.playfab.com/documentation/client#Authentication
    /// </summary>
    public enum AuthMethods
    {
        Default,
        Anonymous,
        PlayFabLogin,
        EmailLogin,
        Register
    }
}