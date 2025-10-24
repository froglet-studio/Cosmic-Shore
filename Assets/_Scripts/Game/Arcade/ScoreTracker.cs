namespace CosmicShore.Game.Arcade
{
    public class ScoreTracker : BaseScoreTracker
    {
        // Normal (Offline / Single Player) scoring logic
        void OnEnable()
        {
            SubscribeEvents();
        }

        void OnDisable()
        {
            UnsubscribeEvents();
        }

        protected override void CalculateWinnerAndInvokeEvent()
        {
            SortAndInvokeResults();
        }
    }
}