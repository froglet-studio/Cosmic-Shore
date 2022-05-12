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
        RectTransform musicToogleSprite;
        [SerializeField]
        Vector3 musicSpriteOffset;

        [SerializeField]
        RectTransform gyroToogleSprite;
        [SerializeField]
        Vector3 gyroSpriteOffset;
        
        // Start is called before the first frame update
        void Start()
        {
            gameManager = GameManager.Instance;

        }

        public void OnMusicToggle(bool on)
        {
            StarWriter.Core.Audio.AudioManager audioManager = Core.Audio.AudioManager.Instance;
            audioManager.ToggleMute();
            if (!on)
            {
                musicToogleSprite.Translate(musicToogleSprite.rect.position.x, musicToogleSprite.rect.position.y, 0);
            }
            if (on)
            {
                musicToogleSprite.Translate(musicSpriteOffset);
            }
            
        }

        public void OnGyroToggle()
        {
            gameManager.OnClickGyroToggleButton();
            if (PlayerPrefs.GetInt("gyroEnabled") == 0) 
            {
                gyroToogleSprite.Translate(gyroToogleSprite.rect.position.x, gyroToogleSprite.rect.position.y, 0);
            }
            if (PlayerPrefs.GetInt("gyroEnabled") == 1)
            {
                gyroToogleSprite.Translate(gyroSpriteOffset);
            }
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
