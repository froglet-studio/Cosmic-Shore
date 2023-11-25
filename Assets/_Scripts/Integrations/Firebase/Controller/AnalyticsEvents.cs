using CosmicShore.Utility.Singleton;

namespace CosmicShore.Integrations.Firebase.Controller
{
    public class AnalyticsEvents : Singleton<AnalyticsEvents>
    {
        public delegate void MiniGameStart(MiniGames mode, ShipTypes ship, int playerCount, int intensity);

        public delegate void MiniGameEnd(MiniGames mode, ShipTypes ship, int playerCount, int intensity, int highScore);
    }
}