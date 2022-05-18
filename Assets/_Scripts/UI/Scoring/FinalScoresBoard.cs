using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using Cinemachine;

namespace StarWriter.Core
{
    public class FinalScoresBoard : MonoBehaviour
    {
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI highScoreText;
        public GameObject replayButton;

        public CinemachineVirtualCamera endSceneCamera;
        
        void Start()
        {           
            scoreText.text = "Score: " + PlayerPrefs.GetFloat("Score").ToString();
            highScoreText.text = "High Score: " + PlayerPrefs.GetFloat("High Score").ToString();
        }

        public void OnEndCameraPositionReached()
        {
            scoreText.gameObject.SetActive(true);
            highScoreText.gameObject.SetActive(true);
            replayButton.SetActive(true);
        }

        public void OnReplayGameButtonPressed()
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.name);
            Debug.Log("play again");
        }
    }
}


