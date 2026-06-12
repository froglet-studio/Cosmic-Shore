namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Placeholder for the Unity Gaming Services Multiplayer session contract
    /// (<c>Unity.Services.Multiplayer.ISession</c>) until the services phase ports the
    /// real session layer. The surface mirrors the scalar subset of the UGS interface
    /// (id / join code / host flag / player counts) so the eventual implementation slots
    /// in without call-site churn; ported game code (e.g. <c>GameDataSO.ActiveSession</c>)
    /// currently only stores and hands around the reference.
    /// </summary>
    public interface ISession
    {
        /// <summary>Unique session id.</summary>
        string Id { get; }

        /// <summary>Human-shareable join code.</summary>
        string Code { get; }

        /// <summary>True when the local client is the session host.</summary>
        bool IsHost { get; }

        /// <summary>Maximum number of players the session admits.</summary>
        int MaxPlayers { get; }

        /// <summary>Number of players currently in the session.</summary>
        int PlayerCount { get; }
    }
}
