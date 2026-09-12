using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
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
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, "[TestMiniGameEvents] OnMiniGameRoundStarted");
        }

        private void OnMiniGameRoundEnd()
        {
            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, "[TestMiniGameEvents] OnMiniGameRoundEnd");
        }
    }
}