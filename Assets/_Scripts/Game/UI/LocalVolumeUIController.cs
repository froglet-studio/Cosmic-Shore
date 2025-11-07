using CosmicShore.SOAP;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.Serialization;

namespace CosmicShore.Game.UI
{
    public class LocalVolumeUIController : MonoBehaviour
    {
        [FormerlySerializedAs("miniGameData")] [SerializeField] GameDataSO gameData;
        [SerializeField] VolumeUI volumeUI;

        private bool _active;
        private bool _running;

        void OnEnable()
        {
            gameData.OnMiniGameTurnStarted.OnRaised += MiniGameTurnStart;
            gameData.OnMiniGameTurnEnd.OnRaised += GameTurnEnd;
        }

        void OnDisable()
        {
            gameData.OnMiniGameTurnStarted.OnRaised -= MiniGameTurnStart;
            gameData.OnMiniGameTurnEnd.OnRaised -= GameTurnEnd;
            _active = false;
            _running = false;
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