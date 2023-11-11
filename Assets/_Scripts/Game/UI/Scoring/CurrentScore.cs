using UnityEngine;
using TMPro;

public class CurrentScore : MonoBehaviour
{
    [SerializeField] TMP_Text currentScoreText;

    void Update()
    {
        //teamStats here needs to be equivalent to teamStats in the current statManager instance
        var teamStats = StatsManager.Instance.teamStats;

        // Check to see if the stats manager has created entries yet. If not treat volume remaining as 0.
        var greenVolume = teamStats.ContainsKey(Teams.Green) ? teamStats[Teams.Green].volumeRemaining : 0f;
        var redVolume = teamStats.ContainsKey(Teams.Red) ? teamStats[Teams.Red].volumeRemaining : 0f; 

        currentScoreText.text = (greenVolume - redVolume).ToString("F0");
    }
}