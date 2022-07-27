using UnityEngine;
using StarWriter.Core;

namespace StarWriter.Core.UI
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

