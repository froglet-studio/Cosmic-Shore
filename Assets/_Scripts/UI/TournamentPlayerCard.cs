using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// <b>Player Data Card</b> — one player's row inside a <see cref="TournamentRoundCard"/>: avatar +
    /// name, the player's <b>Round Score</b> (their result that round), and their domain's <b>Total
    /// Score</b> (cumulative tournament points, as-of that round). Tinted to the player's domain.
    ///
    /// Pure view. In the round-0 preview the round score is hidden (no round played yet); the
    /// round/total roots auto-hide when their value is empty so the same prefab serves both states.
    /// </summary>
    public class TournamentPlayerCard : MonoBehaviour
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

        [Tooltip("Graphics tinted to the player's domain colour (row border / background).")]
        [SerializeField] Graphic[] colorTargets;

        public void Setup(string playerName, Sprite avatar, Color domainColor, string roundScore, string totalScore)
        {
            if (nameText) nameText.text = playerName;
            SetAvatar(avatar);

            if (roundScoreText) roundScoreText.text = roundScore ?? string.Empty;
            if (roundScoreRoot) roundScoreRoot.SetActive(!string.IsNullOrEmpty(roundScore));

            if (totalScoreText) totalScoreText.text = totalScore ?? string.Empty;
            if (totalScoreRoot) totalScoreRoot.SetActive(!string.IsNullOrEmpty(totalScore));

            if (colorTargets != null)
                foreach (var g in colorTargets)
                    if (g) g.color = domainColor;
        }

        void SetAvatar(Sprite sprite)
        {
            if (!avatarImage) return;
            avatarImage.sprite = sprite;
            avatarImage.enabled = sprite != null;
        }
    }
}
