using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Sparrow Tag: single-player dogfight — score points by colliding with AI Sparrow ships.
    /// Turn ends via TimeBasedTurnMonitor configured in the scene.
    /// Scoring via JoustCollisions ScoringMode on the ScoreTracker.
    /// </summary>
    public class SparrowTagController : SinglePlayerMiniGameControllerBase
    {
        [Header("Sparrow Tag")]
        [SerializeField] [Range(0f, 1f)] float _aiSkillLevel = 0.5f;

        protected override bool HasEndGame => true;
        protected override bool ShouldResetPlayersOnTurnEnd => true;

        protected override void SetupNewRound()
        {
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewRound();
        }

        protected override void OnCountdownTimerEnded()
        {
            ConfigureAIOpponents();
            base.OnCountdownTimerEnded();
        }

        void ConfigureAIOpponents()
        {
            foreach (var player in gameData.Players)
            {
                if (!player.IsInitializedAsAI) continue;
                player.Vessel.VesselStatus.AIPilot.ConfigureForGameMode(gameData, shouldSeekPlayers: true, _aiSkillLevel);
            }
        }
    }
}
