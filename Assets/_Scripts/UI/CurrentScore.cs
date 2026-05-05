using UnityEngine;
using TMPro;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Reflex.Attributes;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    public class CurrentScore : MonoBehaviour
    {
        [SerializeField] TMP_Text currentScoreText;

        [Inject] GameDataSO gameData;

        void Update()
        {
            float greenVolume = 0f;
            float redVolume = 0f;
            var stats = gameData.RoundStatsList;
            for (int i = 0; i < stats.Count; i++)
            {
                var rs = stats[i];
                if (rs.Domain == Domains.Jade)       greenVolume = rs.VolumeRemaining;
                else if (rs.Domain == Domains.Ruby)  redVolume   = rs.VolumeRemaining;
            }
            currentScoreText.text = (greenVolume - redVolume).ToString("F0");
        }
    }
}