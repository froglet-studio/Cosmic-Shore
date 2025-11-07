using System.Globalization;
using TMPro;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class DuellCellStatsRowUIController : MonoBehaviour
    {
        [SerializeField]
        TMP_Text volumeCreatedText;
        
        [SerializeField]
        TMP_Text hostileVolumeDestroyedText;
        
        [SerializeField]
        TMP_Text friendlyVolumeDestroyedText;
        
        [SerializeField]
        TMP_Text scoreText;

        /// <summary>
        /// This is a temporary solution. Data should not be stored here, rather should be stored in GameData,
        /// for each round we need to create separate stats data and store them as a collection.
        /// </summary>
        public DuelCellStatsRoundUIController.StatsRowData Data { get; set; } = new();
        
        public void UpdateRow()
        {
            volumeCreatedText.text = Data.VolumeCreated.ToString(CultureInfo.CurrentCulture);
            hostileVolumeDestroyedText.text = Data.HostileVolumeDestroyed.ToString(CultureInfo.CurrentCulture);
            friendlyVolumeDestroyedText.text = Data.FriendlyVolumeDestroyed.ToString(CultureInfo.CurrentCulture);
            scoreText.text = Data.Score.ToString(CultureInfo.CurrentCulture);
        }
        
        [ContextMenu("CleanupUI")]
        public void CleanupUI()
        {
            volumeCreatedText.text = "";
            hostileVolumeDestroyedText.text = "";
            friendlyVolumeDestroyedText.text = "";
            scoreText.text = "";
        }
    }
}