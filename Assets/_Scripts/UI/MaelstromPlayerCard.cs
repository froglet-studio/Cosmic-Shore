using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// <b>Player Data Card</b> - one player's row inside a <see cref="MaelstromRoundCard"/>: avatar +
    /// name, the player's <b>Round Score</b> (their mode-formatted result that round, e.g. "01:24:30"),
    /// the <b>points</b> their domain took from the round ("+5") and the <b>Total Score</b> (their
    /// domain's cumulative tournament points, as-of that round). The ring around the avatar is tinted to
    /// the player's domain colour the round card passes in (the theme's per-domain UI accent).
    ///
    /// <para>Style B (Docs/MENU_REDESIGN_B.md section 27): the round card carries the ROUND / TOTAL
    /// column heads, so the row writes bare values - no "Round Score :" prefixes.</para>
    /// </summary>
    public class MaelstromPlayerCard : MonoBehaviour
    {
        [SerializeField] Image avatarImage;
        [SerializeField] TMP_Text nameText;

        [Header("Round Score (this round's result)")]
        [SerializeField] TMP_Text roundScoreText;
        [Tooltip("Optional root hidden when there's no round score (the preview card).")]
        [SerializeField] GameObject roundScoreRoot;

        [Header("Points (what the round paid the domain)")]
        [Tooltip("\"+5\" in the ROUND column. Optional.")]
        [SerializeField] TMP_Text pointsText;

        [Header("Total Score (domain cumulative)")]
        [SerializeField] TMP_Text totalScoreText;
        [Tooltip("Optional root hidden when there's no total to show.")]
        [SerializeField] GameObject totalScoreRoot;

        [Header("Domain colour")]
        [Tooltip("The image whose colour changes to the player's domain colour (the avatar ring).")]
        [SerializeField] Image domainBackground;

        public void Setup(string playerName, Sprite avatar, Domains domain, string roundScore, string totalScore,
                          Color domainColor)
        {
            if (nameText) nameText.text = playerName;
            SetAvatar(avatar);

            bool hasRound = !string.IsNullOrEmpty(roundScore);
            if (roundScoreText) roundScoreText.text = hasRound ? roundScore : string.Empty;
            if (roundScoreRoot) roundScoreRoot.SetActive(hasRound);

            bool hasTotal = !string.IsNullOrEmpty(totalScore);
            if (totalScoreText) totalScoreText.text = hasTotal ? totalScore : "-";
            if (totalScoreRoot) totalScoreRoot.SetActive(hasTotal);

            if (pointsText) pointsText.text = string.Empty;
            if (domainBackground) domainBackground.color = domainColor;
        }

        /// <summary>The points the player's domain took from this round; negative hides the value.</summary>
        public void SetPoints(int points)
        {
            if (pointsText) pointsText.text = points >= 0 ? $"+{points}" : string.Empty;
        }

        void SetAvatar(Sprite sprite)
        {
            if (!avatarImage) return;
            avatarImage.sprite = sprite;
            avatarImage.enabled = sprite != null;
        }
    }
}
