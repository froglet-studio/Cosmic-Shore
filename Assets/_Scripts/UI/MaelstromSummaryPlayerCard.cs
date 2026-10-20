using CosmicShore.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// <b>Summary Player Card</b> (MaelstromSummaryScoreCardContainer) - one player's row on the
    /// Maelstrom results screen: avatar, name, the player's <b>Total Score</b> (their domain's cumulative
    /// tournament points). The background tints to the player's domain colour the view passes in (the
    /// theme's per-domain UI accent, same as the in-round Player Data Card). The authored "stats"
    /// placeholder text is left untouched. Pops in with a staggered entrance.
    ///
    /// <para>Style B (Docs/MENU_REDESIGN_B.md section 27): the domain's rank ("#1"), a domain pill,
    /// and the total as a bare number under its own "Total score" label.</para>
    /// </summary>
    public class MaelstromSummaryPlayerCard : MonoBehaviour
    {
        [SerializeField] Image avatarImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text totalScoreText;

        [Header("Domain colour")]
        [Tooltip("Background image tinted to the player's domain colour.")]
        [SerializeField] Image domainBackground;

        [Header("Style B (optional)")]
        [Tooltip("\"#1\": the player's domain's place in the final standings.")]
        [SerializeField] TMP_Text rankText;
        [Tooltip("The card's frame, in the domain colour.")]
        [SerializeField] Image frameBorder;
        [SerializeField] Image pillBackground;
        [SerializeField] Image pillMark;
        [SerializeField] TMP_Text domainText;
        [SerializeField, Range(0f, 1f)] float pillAlpha = 0.14f;

        CanvasGroup _cg;
        Sequence _seq;

        public void Setup(string playerName, Sprite avatar, Domains domain, int totalScore, Color domainColor)
        {
            if (nameText) nameText.text = playerName;
            SetAvatar(avatar);
            if (totalScoreText) totalScoreText.text = totalScore.ToString();
            if (domainBackground) domainBackground.color = domainColor;
            if (frameBorder) frameBorder.color = domainColor;
            if (rankText) rankText.color = domainColor;
            if (pillMark) pillMark.color = domainColor;
            if (pillBackground) pillBackground.color = new Color(domainColor.r, domainColor.g, domainColor.b, pillAlpha);
            if (domainText)
            {
                domainText.text = domain.ToString().ToUpperInvariant();
                domainText.color = domainColor;
            }
        }

        /// <summary>The player's domain's place in the final standings (1 = winner).</summary>
        public void SetRank(int rank)
        {
            if (rankText) rankText.text = rank > 0 ? $"#{rank}" : string.Empty;
        }

        /// <summary>
        /// Pops the card in (fade + scale-up with overshoot), staggered by index. Animates to the card's
        /// CURRENT localScale, so the view can set the first card to 1 and the rest to 0.9 beforehand.
        /// </summary>
        public void PlayEntrance(int staggerIndex)
        {
            EnsureCanvasGroup();
            _seq?.Kill();

            Vector3 target = transform.localScale;
            _cg.alpha = 0f;
            transform.localScale = target * 0.85f;

            float delay = staggerIndex * 0.07f;
            _seq = DOTween.Sequence().SetUpdate(true);
            _seq.Append(transform.DOScale(target, 0.35f).SetEase(Ease.OutBack).SetDelay(delay));
            _seq.Join(_cg.DOFade(1f, 0.3f).SetDelay(delay));
        }

        void EnsureCanvasGroup()
        {
            if (_cg) return;
            _cg = GetComponent<CanvasGroup>();
            if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        void SetAvatar(Sprite sprite)
        {
            if (!avatarImage) return;
            avatarImage.sprite = sprite;
            avatarImage.enabled = sprite != null;
        }

        void OnDestroy() => _seq?.Kill();
    }
}
