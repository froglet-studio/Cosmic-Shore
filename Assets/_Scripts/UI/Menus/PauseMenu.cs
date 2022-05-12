using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;


namespace StarWriter.UI
{
    public class PauseMenu : MonoBehaviour
    {
        GameManager gameManager;

        [SerializeField]
        Sprite musicToogleSprite;
        [SerializeField]
        Vector3 musicSpriteOffset;
        bool isMuted = false;

        [SerializeField]
        Sprite gyroToogleSprite;
        [SerializeField]
        Vector3 gyroSpriteOffset;
        bool gyroEnabled = true;

        float xSpeed = 10f;

        Vector2 musicOnPosition = new Vector2();
        Vector2 musicOffPosition = new Vector2();

        // Start is called before the first frame update
        void Start()
        {
            gameManager = GameManager.Instance;
            if(PlayerPrefs.GetInt("isMuted") == 0){ isMuted = false; }
            if(PlayerPrefs.GetInt("isMuted") == 1) { isMuted = true; }
            if (PlayerPrefs.GetInt("gyroEnabled") == 0) { gyroEnabled = false; }
            if (PlayerPrefs.GetInt("gyroEnabled") == 1) { gyroEnabled = true; }

            musicOnPosition = new Vector2(musicToogleSprite.rect.xMax, 0);
            musicOffPosition = new Vector2(musicToogleSprite.rect.xMin, 0);

        }

        private void Update()
        {
            if (isMuted)
            {
                //musicToogleSprite.rect.
            }
            else
            {
                //Spite moves to original position
            }
            if (gyroEnabled)
            {
                //Move Sprite 
            }
            else
            {
                //Spite moves to original position
            }
        }

        public void OnMusicToggle(bool currentStatus)
        {
            StarWriter.Core.Audio.AudioManager audioManager = Core.Audio.AudioManager.Instance;
            audioManager.ToggleMute();
            isMuted = !currentStatus;
            
        }

        public void OnGyroToggle(bool currentStatus)
        {
            gameManager.OnClickGyroToggleButton();
            gyroEnabled = !currentStatus;
        }

        public void OnTutorialButtonPressed()
        {
            gameManager.OnClickTutorialToggleButton();
        }

        public void OnRestartButtonPressed()
        {
            gameManager.OnReplayButtonPressed();
        }

        public void OnResumeButtonPressed()
        {
            gameManager.OnResumeButtonPressed();
            transform.GetComponentInParent<GameMenu>().UnpauseGame();
        }

    }
}
