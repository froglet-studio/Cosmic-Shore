using CosmicShore.Soap;

namespace CosmicShore.Game.Arcade.Scoring
{
    /// <summary>
    /// Awards points each time the local player lands a joust (ship-to-ship) collision.
    /// Used by Sparrow Tag mode.
    /// </summary>
    public class JoustCollisionsScoring : BaseScoring
    {
        IRoundStats _localStats;

        public JoustCollisionsScoring(IScoreTracker tracker, GameDataSO gameData, float multiplier)
            : base(tracker, gameData, multiplier) { }

        public override void Subscribe()
        {
            if (GameData.TryGetLocalPlayerStats(out _, out _localStats))
                _localStats.OnJoustCollisionChanged += OnJoustCollision;
        }

        public override void Unsubscribe()
        {
            if (_localStats != null)
                _localStats.OnJoustCollisionChanged -= OnJoustCollision;
        }

        void OnJoustCollision(IRoundStats stats)
        {
            Score += scoreMultiplier;
        }
    }
}
