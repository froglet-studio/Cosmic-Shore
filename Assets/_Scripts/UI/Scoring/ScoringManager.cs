using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoringManager : MonoBehaviour
{
    [SerializeField]
    float score = 0f;

    public TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        IntensitySystem.onPlayerIntensityOverflow += AddExcessIntensityToScore;
    }

    private void OnDisable()
    {
        IntensitySystem.onPlayerIntensityOverflow -= AddExcessIntensityToScore;
    }

    public void AddExcessIntensityToScore(string uuid, float amount) // TODO Needs to be private... put events on mutons and trails to handle
    {
        if (uuid == "admin") { score += amount; }

        UpdateScoreBoard(score);
    }
    public void UpdateScoreBoard(float value)
    {
        scoreText.text = "Score: " + value.ToString();

    }

    private void OnDestroy()
    {
        //Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetFloat("High Score") < score)
            PlayerPrefs.SetFloat("High Score", score);
  
    }
}
