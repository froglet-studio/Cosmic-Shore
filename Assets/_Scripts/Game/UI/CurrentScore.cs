using UnityEngine;
using TMPro;
using CosmicShore.Core;
using CosmicShore.Soap;
using UnityEngine.Serialization;

namespace CosmicShore.Game.UI
{
    public class CurrentScore : MonoBehaviour
    {
        [SerializeField] TMP_Text currentScoreText;

        [FormerlySerializedAs("miniGameData")] [SerializeField] GameDataSO gameData;

        void Update()
        {
            // Iterate directly instead of sorting + LINQ per frame
            var statsList = gameData.RoundStatsList;
            float greenVolume = 0f;
            float redVolume = 0f;
            for (int i = 0, count = statsList.Count; i < count; i++)
            {
                var rs = statsList[i];
                if (rs.Domain == Domains.Jade) greenVolume = rs.VolumeRemaining;
                else if (rs.Domain == Domains.Ruby) redVolume = rs.VolumeRemaining;
            }

            currentScoreText.text = (greenVolume - redVolume).ToString("F0");

            
            /*//teamStats here needs to be equivalent to teamStats in the current statManager instance
            var teamStats = StatsManager.Instance.TeamStats;

            // Check to see if the stats manager has created entries yet. If not treat volume remaining as 0.
            var greenVolume = teamStats.ContainsKey(Teams.Jade) ? teamStats[Teams.Jade].VolumeRemaining : 0f;
            var redVolume = teamStats.ContainsKey(Teams.Ruby) ? teamStats[Teams.Ruby].VolumeRemaining : 0f;

            currentScoreText.text = (greenVolume - redVolume).ToString("F0");*/
        }
    }
}