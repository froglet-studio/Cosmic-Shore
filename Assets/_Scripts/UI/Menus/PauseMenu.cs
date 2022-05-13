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

        #region Music Fields
        [SerializeField]
        GameObject musicToogleSprite;
        [SerializeField]
        Vector3 musicSpriteOffset;
        bool isMuted = false;
        Vector3 musicOnPosition = new Vector3();
        Vector3 musicOffPosition = new Vector3();
        Rigidbody2D mRB2;
        #endregion

        #region Gyro Fields
        [SerializeField]
        GameObject gyroToogleSprite;
        [SerializeField]
        Vector3 gyroSpriteOffset;
        bool gyroEnabled = true;
        Vector3 gyroOnPosition = new Vector3();
        Vector3 gyroOffPosition = new Vector3();
        Rigidbody2D gyroRB2;
#endregion

        float xSpeed = 10f;

        

        Vector3 offset = new Vector3(10,0,0);

        // Start is called before the first frame update
        void Start()
        {
            gameManager = GameManager.Instance;

            // Set Music status
            if(PlayerPrefs.GetInt("isMuted") == 0){ isMuted = false; }
            if(PlayerPrefs.GetInt("isMuted") == 1) { isMuted = true; }

            // Set Music toggle sprite movement info
            musicOnPosition = musicToogleSprite.transform.position;
            musicOffPosition = musicToogleSprite.transform.position + offset;
            mRB2 = musicToogleSprite.GetComponent<Rigidbody2D>();

            // Set Gyro status
            if (PlayerPrefs.GetInt("gyroEnabled") == 0) { gyroEnabled = false; }
            if (PlayerPrefs.GetInt("gyroEnabled") == 1) { gyroEnabled = true; }

            // Set Gyro toggle sprite movement info
            gyroOnPosition = gyroToogleSprite.transform.position;
            gyroOffPosition = gyroToogleSprite.transform.position + offset;
            gyroRB2 = gyroToogleSprite.GetComponent<Rigidbody2D>();

        }

        private void Update()
        {
            if (isMuted)
            {
                mRB2.MovePosition(musicOffPosition);
            }
            else
            {
                mRB2.MovePosition(musicOnPosition);
            }
            if (gyroEnabled)
            {
                gyroRB2.MovePosition(gyroOnPosition);
            }
            else
            {
                gyroRB2.MovePosition(gyroOffPosition);
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
