using System;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Game.Arcade
{
    public class MultiplayerFreestyleController : MiniGameControllerBase
    {
        protected override void Start()
        {
        }
        
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;
            miniGameData.OnMiniGameTurnEnd += EndTurn;
            InitializeAfterDelay().Forget();
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
                return;
            miniGameData.OnMiniGameTurnEnd += EndTurn;
        }
        
        private async UniTaskVoid InitializeAfterDelay()
        {
            try
            {
                // Delay for 2 seconds (scaled time by default)
                await UniTask.Delay(1000, DelayType.UnscaledDeltaTime);
                Initialize();
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected override void OnCountdownTimerEnded()
        {
            miniGameData.SetPlayersActiveForMultiplayer(active: true);
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            miniGameData.StartNewGame();
        }
    }
}