using CosmicShore.Utility.Singleton;

namespace CosmicShore.Integrations.Firebase.Controller
{
    public class AnalyticsEvents : Singleton<AnalyticsEvents>
    {
        public delegate void MiniGameStart(GameModes mode, ShipTypes ship, int playerCount, int intensity);

        public delegate void MiniGameEnd(GameModes mode, ShipTypes ship, int playerCount, int intensity, int highScore);
    }
}