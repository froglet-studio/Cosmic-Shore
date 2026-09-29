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
    /// </summary>
    public class MaelstromSummaryPlayerCard : MonoBehaviour
    {
        [SerializeField] Image avatarImage;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text totalScoreText;

        [Header("Domain colour")]
        [Tooltip("Background image tinted to the player's domain colour.")]
        [SerializeField] Image domainBackground;

        CanvasGroup _cg;
        Sequence _seq;

        public void Setup(string playerName, Sprite avatar, Domains domain, int totalScore, Color domainColor)
        {
            if (nameText) nameText.text = playerName;
            SetAvatar(avatar);
            if (totalScoreText) totalScoreText.text = $"Total Score : {totalScore}";
            if (domainBackground) domainBackground.color = domainColor;
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
