using CosmicShore.Integrations.Instrumentation.Interfaces;

namespace CosmicShore._Scripts.Integrations.Instrumentation.Firebase
{
    public class CSArcadeDataCollectorFirebase : IArcadeAnalyzable
    {
        private readonly IArcadeAnalyzable _arcadeDataCollectorFirebase = new CSArcadeDataCollectorFirebase();
        public void InitSDK()
        {
            
        }

        public void LogEventStartArcadeGame()
        {
            _arcadeDataCollectorFirebase.LogEventCompleteArcadeGame();
        }

        public void LogEventCompleteArcadeGame()
        {
            
        }
    }
}