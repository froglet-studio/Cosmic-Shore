using System.Globalization;
using TMPro;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public class DuellCellStatsRowUIController : MonoBehaviour
    {
        [SerializeField] TMP_Text playerNameText;

        // Ordered EXACTLY as you want them:
        [SerializeField] TMP_Text prismsCreatedText;
        [SerializeField] TMP_Text volumeCreatedText;

        [SerializeField] TMP_Text prismsDestroyedText;
        [SerializeField] TMP_Text volumeDestroyedText;

        [SerializeField] TMP_Text hostilePrismsDestroyedText;
        [SerializeField] TMP_Text hostileVolumeDestroyedText;

        [SerializeField] TMP_Text friendlyPrismsDestroyedText;
        [SerializeField] TMP_Text friendlyVolumeDestroyedText;

        [SerializeField] TMP_Text prismsRemainingText;
        [SerializeField] TMP_Text volumeRemainingText;

        [SerializeField] TMP_Text volumeRestoredText;
        [SerializeField] TMP_Text volumeStolenText;

        [SerializeField] TMP_Text scoreText;

        /// <summary>
        /// Temporary — real implementation should come from GameData.
        /// </summary>
        public DuelCellStatsRoundUIController.StatsRowData Data { get; set; } = new();


        public void UpdateRow()
        {
            playerNameText.text = Data.PlayerName;

            prismsCreatedText.text = Data.BlocksCreated.ToString(CultureInfo.InvariantCulture);
            volumeCreatedText.text = Data.VolumeCreated.ToString(CultureInfo.InvariantCulture);

            prismsDestroyedText.text = Data.BlocksDestroyed.ToString(CultureInfo.InvariantCulture);
            volumeDestroyedText.text = Data.TotalVolumeDestroyed.ToString(CultureInfo.InvariantCulture);

            hostilePrismsDestroyedText.text = Data.HostilePrismsDestroyed.ToString(CultureInfo.InvariantCulture);
            hostileVolumeDestroyedText.text = Data.HostileVolumeDestroyed.ToString(CultureInfo.InvariantCulture);

            friendlyPrismsDestroyedText.text = Data.FriendlyPrismsDestroyed.ToString(CultureInfo.InvariantCulture);
            friendlyVolumeDestroyedText.text = Data.FriendlyVolumeDestroyed.ToString(CultureInfo.InvariantCulture);

            prismsRemainingText.text = Data.PrismsRemaining.ToString(CultureInfo.InvariantCulture);
            volumeRemainingText.text = Data.VolumeRemaining.ToString(CultureInfo.InvariantCulture);

            volumeRestoredText.text = Data.VolumeRestored.ToString(CultureInfo.InvariantCulture);
            volumeStolenText.text = Data.VolumeStolen.ToString(CultureInfo.InvariantCulture);

            scoreText.text = Data.Score.ToString(CultureInfo.InvariantCulture);
        }


        [ContextMenu("CleanupUI")]
        public void CleanupUI()
        {
            playerNameText.text =
            prismsCreatedText.text =
            volumeCreatedText.text =
            prismsDestroyedText.text =
            volumeDestroyedText.text =
            hostilePrismsDestroyedText.text =
            hostileVolumeDestroyedText.text =
            friendlyPrismsDestroyedText.text =
            friendlyVolumeDestroyedText.text =
            prismsRemainingText.text =
            volumeRemainingText.text =
            volumeRestoredText.text =
            volumeStolenText.text =
            scoreText.text = "";
        }
    }
}
