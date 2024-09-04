using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class HangarGameplayParameterDisplayGroup : MonoBehaviour
    {
        [SerializeField] List<HangarGameplayParameterDisplay> gameplayParameterDisplays = new();

        /// <summary>
        /// Assigns a list of GameplayerParameters to be displayed in the HangarGameplayParameterDisplayGroup UI
        /// </summary>
        /// <param name="gameplayParameters">The list of GameplayParameters to display in the UI</param>
        public void AssignGameplayParameters(List<GameplayParameter> gameplayParameters)
        {
            for (int i = 0; i < Mathf.Min(gameplayParameters.Count, gameplayParameterDisplays.Count); i++)
            {
                gameplayParameterDisplays[i].AssignGameParameter(gameplayParameters[i]);
            }

            if (gameplayParameters.Count != gameplayParameterDisplays.Count)
            {
                Debug.LogError("HangarGameplayParameterDisplayGroup configuration error: gameplayParameterDisplays.Count is not equal to gameplayParameters.Count.");
            }
        }
    }
}
