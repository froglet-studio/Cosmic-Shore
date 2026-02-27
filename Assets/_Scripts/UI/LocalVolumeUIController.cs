using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace CosmicShore.UI
{
    public class LocalVolumeUIController : MonoBehaviour
    {
        [Inject] GameDataSO gameData;
        [SerializeField] VolumeUI volumeUI;

        private bool _active;
        private bool _running;

        void OnEnable() => SubscribeToEvents();
        void Start() => SubscribeToEvents();

        void OnDisable()
        {
            UnsubscribeFromEvents();
            _active = false;
            _running = false;
        }

        void SubscribeToEvents()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= MiniGameTurnStart;
            gameData.OnMiniGameTurnEnd.OnRaised -= GameTurnEnd;
            gameData.OnMiniGameTurnStarted.OnRaised += MiniGameTurnStart;
            gameData.OnMiniGameTurnEnd.OnRaised += GameTurnEnd;
        }

        void UnsubscribeFromEvents()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnStarted.OnRaised -= MiniGameTurnStart;
            gameData.OnMiniGameTurnEnd.OnRaised -= GameTurnEnd;
        }

        private void MiniGameTurnStart()
        {
            _active = true;
            if (!_running) RunVolumeUpdater().Forget();
        }

        private void GameTurnEnd()
        {
            _active = false;
        }

        private async UniTaskVoid RunVolumeUpdater()
        {
            _running = true;
            while (_active && this)
            {
                if (volumeUI && gameData)
                {
                    var teamVolumes = gameData.GetTeamVolumes();
                    volumeUI.UpdateVolumes(teamVolumes);
                }

                await UniTask.Delay(500, DelayType.UnscaledDeltaTime); // update every 0.5s
            }
            _running = false;
        }
    }
}