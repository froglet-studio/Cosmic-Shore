using CosmicShore.Soap;
using CosmicShore.Utility.ClassExtensions;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class TestMiniGameEvents : MonoBehaviour
    {
        [SerializeField]
        GameDataSO gameData;
        
        private void OnEnable()
        {
            gameData.OnMiniGameRoundStarted.OnRaised += OnMiniGameRoundStarted;
            gameData.OnMiniGameRoundEnd.OnRaised += OnMiniGameRoundEnd;
        }

        private void OnDisable()
        { 
            gameData.OnMiniGameRoundStarted.OnRaised -= OnMiniGameRoundStarted;
            gameData.OnMiniGameRoundEnd.OnRaised -= OnMiniGameRoundEnd;
        }

        private void OnMiniGameRoundStarted()
        {
            DebugExtensions.LogColored("OnMiniGameRoundStarted", Color.cyan);
        }

        private void OnMiniGameRoundEnd()
        {
            DebugExtensions.LogColored("OnMiniGameRoundEnd", Color.cyan);
        }
    }
}