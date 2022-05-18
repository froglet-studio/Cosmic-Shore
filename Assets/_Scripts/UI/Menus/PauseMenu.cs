using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using StarWriter.Core;
using StarWriter.Core.Audio;


namespace StarWriter.UI
{
    public class PauseMenu : MonoBehaviour
    {
        GameManager gameManager;

        #region Music Fields
        [SerializeField]
        GameObject musicToogleSprite;
        [SerializeField]
        RectTransform musicRectTransform;
        [SerializeField]
        Vector2 musicSpriteOffset = new Vector2(100,0);
        bool isMuted = false;
        Vector2 musicOnPosition; // = new Vector3();
        Vector2 musicOffPosition; // = new Vector3();
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

        float HorizontalSpeed = 2;
        #endregion



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
            Debug.Log("Music currentStatus is " + currentStatus);
            
            AudioManager.Instance.ToggleMute();
            isMuted = !currentStatus;
            if (!isMuted)
            {
                //MoveToggle(mRB2, musicOnPosition);
                ToggleSprite(musicRectTransform,musicOffPosition,musicOnPosition);
                Debug.Log("Music is ON");
            }
            else if (!isMuted)
            {
                ToggleSprite(musicRectTransform, musicOnPosition, musicOffPosition);
                Debug.Log("Music is OFF");
            }

        }

        public void OnGyroToggle(bool currentStatus)
        {
            gameManager.OnClickGyroToggleButton();
            gyroEnabled = !currentStatus;
        }

        public void OnTutorialButton()
        {
            gameManager.OnClickTutorialToggleButton();
        }

        public void OnClickRestartButton()
        {
            gameManager.OnClickPlayButton();
        }

        public void OnClickResumeButton()
        {
            gameManager.OnClickResumeButton();
            transform.GetComponentInParent<GameMenu>().OnClickUnpauseGame();
        }

        public void ToggleSprite(RectTransform rectTrans, Vector2 startPos, Vector2 endPos)
        {
            Vector2 position = rectTrans.anchoredPosition;

            position.x += HorizontalSpeed * Time.deltaTime;

            if (position.x > startPos.x)
                position.x = endPos.x;

            rectTrans.anchoredPosition = position;
        }

    }
}