namespace CosmicShore.Game.UI
{
    public enum GameFeedType
    {
        Generic,
        PlayerJoined,
        PlayerReady,
        PlayerDisconnected,
        JoustHit
    }

    [System.Serializable]
    public struct GameFeedPayload
    {
        public string Message;
        public Domains Domain;
        public GameFeedType Type;

        public GameFeedPayload(string message, Domains domain, GameFeedType type = GameFeedType.Generic)
        {
            Message = message;
            Domain = domain;
            Type = type;
        }
    }
}
