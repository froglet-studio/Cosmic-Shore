using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Game.UI
{
    public class PauseButton : MonoBehaviour
    {
        public void OnClickPauseButton()
        {
            GameManager.PauseGame();
        }

        public void OnClickUnPauseButton()
        {
            GameManager.UnPauseGame();
        }
    }
}