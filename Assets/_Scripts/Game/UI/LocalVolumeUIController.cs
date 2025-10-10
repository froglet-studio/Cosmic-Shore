using CosmicShore.SOAP;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace CosmicShore.Game.UI
{
    public class LocalVolumeUIController : MonoBehaviour
    {
        [SerializeField] MiniGameDataSO miniGameData;
        [SerializeField] VolumeUI volumeUI;

        private bool _active;
        private bool _running;

        void OnEnable()
        {
            miniGameData.OnGameStarted += OnMiniGameStart;
            miniGameData.OnMiniGameTurnEnd += OnMiniGameTurnEnd;
        }

        void OnDisable()
        {
            miniGameData.OnGameStarted -= OnMiniGameStart;
            miniGameData.OnMiniGameTurnEnd -= OnMiniGameTurnEnd;
            _active = false;
            _running = false;
        }

        private void OnMiniGameStart()
        {
            _active = true;
            if (!_running) RunVolumeUpdater().Forget();
        }

        private void OnMiniGameTurnEnd()
        {
            _active = false;
        }

        private async UniTaskVoid RunVolumeUpdater()
        {
            _running = true;
            while (_active && this)
            {
                if (volumeUI && miniGameData)
                {
                    var teamVolumes = miniGameData.GetTeamVolumes();
                    volumeUI.UpdateVolumes(teamVolumes);
                }

                await UniTask.Delay(500, DelayType.UnscaledDeltaTime); // update every 0.5s
            }
            _running = false;
        }
    }
}