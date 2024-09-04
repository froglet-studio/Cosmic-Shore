using UnityEngine;
using TMPro;

namespace CosmicShore.Game.UI
{
    public class CurrentScore : MonoBehaviour
    {
        [SerializeField] TMP_Text currentScoreText;

        void Update()
        {
            //teamStats here needs to be equivalent to teamStats in the current statManager instance
            var teamStats = StatsManager.Instance.TeamStats;

            // Check to see if the stats manager has created entries yet. If not treat volume remaining as 0.
            var greenVolume = teamStats.ContainsKey(Teams.Jade) ? teamStats[Teams.Jade].VolumeRemaining : 0f;
            var redVolume = teamStats.ContainsKey(Teams.Ruby) ? teamStats[Teams.Ruby].VolumeRemaining : 0f;

            currentScoreText.text = (greenVolume - redVolume).ToString("F0");
        }
    }
}