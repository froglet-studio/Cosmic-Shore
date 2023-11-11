using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore.Core.UI
{
    public class PauseButton : MonoBehaviour
    {

        GameManager gameManager;

        // Start is called before the first frame update
        void Start()
        {
            gameManager = GameManager.Instance;
        }

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

