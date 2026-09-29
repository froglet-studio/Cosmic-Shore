using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// <b>Player Data Card</b> - one player's row inside a <see cref="MaelstromRoundCard"/>: avatar +
    /// name, the player's <b>Round Score</b> (their result that round) and <b>Total Score</b> (their
    /// domain's cumulative tournament points, as-of that round). The background image is tinted to the
    /// player's domain colour the round card passes in (the theme's per-domain UI accent).
    /// </summary>
    public class MaelstromPlayerCard : MonoBehaviour
    {
        [SerializeField] Image avatarImage;
        [SerializeField] TMP_Text nameText;

        [Header("Round Score (this round's result)")]
        [SerializeField] TMP_Text roundScoreText;
        [Tooltip("Optional root hidden when there's no round score (the preview card).")]
        [SerializeField] GameObject roundScoreRoot;

        [Header("Total Score (domain cumulative)")]
        [SerializeField] TMP_Text totalScoreText;
        [Tooltip("Optional root hidden when there's no total to show.")]
        [SerializeField] GameObject totalScoreRoot;

        [Header("Domain colour")]
        [Tooltip("The background image whose colour changes to the player's domain colour.")]
        [SerializeField] Image domainBackground;

        public void Setup(string playerName, Sprite avatar, Domains domain, string roundScore, string totalScore,
                          Color domainColor)
        {
            if (nameText) nameText.text = playerName;
            SetAvatar(avatar);

            // Cards emit the FULL labelled text ("Round Score : 6") - don't add a separate static label.
            bool hasRound = !string.IsNullOrEmpty(roundScore);
            if (roundScoreText) roundScoreText.text = hasRound ? $"Round Score : {roundScore}" : string.Empty;
            if (roundScoreRoot) roundScoreRoot.SetActive(hasRound);

            bool hasTotal = !string.IsNullOrEmpty(totalScore);
            if (totalScoreText) totalScoreText.text = hasTotal ? $"Total Score : {totalScore}" : string.Empty;
            if (totalScoreRoot) totalScoreRoot.SetActive(hasTotal);

            if (domainBackground) domainBackground.color = domainColor;
        }

        void SetAvatar(Sprite sprite)
        {
            if (!avatarImage) return;
            avatarImage.sprite = sprite;
            avatarImage.enabled = sprite != null;
        }
    }
}
