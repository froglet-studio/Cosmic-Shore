using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// End-game scoreboard row for a single player.
    /// Displays avatar, username, formatted score, and optional "+N" crystal reward.
    /// Background tints to the player's domain color.
    ///
    /// Score-number animation is delegated to <see cref="ScoreNumberAnimator"/> and
    /// the entrance to <see cref="CardEntranceAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PlayerScoreCard : MonoBehaviour
    {
        [Header("Main Fields")]
        [Tooltip("Player avatar / profile icon")]
        [SerializeField] private Image playerAvatarImage;
        [Tooltip("Player display name")]
        [SerializeField] private TMP_Text playerNameText;
        [Tooltip("Primary score display (time / crystals / points)")]
        [SerializeField] private TMP_Text playerScoreText;

        [Header("Domain Tint")]
        [Tooltip("Optional background image tinted with domain color (falls back to domainIndicatorImage if unset)")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Optional small indicator image (legacy)")]
        [SerializeField] private Image domainIndicatorImage;
        [Tooltip("Alpha applied to the background tint (0-1). 1 = solid, 0.2 = subtle tint")]
        [Range(0f, 1f)]
        [SerializeField] private float backgroundTintAlpha = 0.35f;

        [Header("Extra Data Panels")]
        [Tooltip("Root of DataPanels - hidden if no extra stats to show")]
        [SerializeField] private GameObject dataPanelsRoot;
        [Tooltip("Optional secondary data text (e.g. crystals collected, clean streak)")]
        [SerializeField] private TMP_Text secondaryStatText;
        [Tooltip("Optional '+N' crystal reward text shown only for winners")]
        [SerializeField] private GameObject crystalRewardRoot;
        [SerializeField] private TMP_Text crystalRewardText;

        [Header("Animation (optional - falls back to defaults)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private Sequence _entranceSeq;
        private ScoreNumberAnimator _scoreAnimator;

        private ScoreNumberAnimator ScoreAnimator =>
            _scoreAnimator ??= new ScoreNumberAnimator(playerScoreText, animSettings);

        private void Awake()
        {
            EnsureCanvasGroup();
        }

        void EnsureCanvasGroup()
        {
            if (_canvasGroup) return;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// Sets up the card with basic player info. Accepts a formatted score string directly
        /// so the caller controls display (time, crystals, etc).
        /// </summary>
        public void Setup(string playerName, string formattedScore, Color domainColor, int staggerIndex = 0)
        {
            if (playerNameText) playerNameText.text = playerName;
            if (playerScoreText) playerScoreText.text = formattedScore;

            ApplyDomainColor(domainColor);
            HideCrystalReward();
            HideSecondaryStat();
            PlayEntrance(staggerIndex);
        }

        /// <summary>
        /// Back-compat overload for integer-score callers. Displays "{value}" as the score.
        /// </summary>
        public void Setup(string playerName, int initialScore, Color domainColor, bool isLocalPlayer, int staggerIndex = 0)
        {
            ScoreAnimator.SetImmediate(initialScore);
            Setup(playerName, initialScore.ToString(), domainColor, staggerIndex);
        }

        public void SetAvatar(Sprite avatarSprite)
        {
            if (!playerAvatarImage) return;
            if (avatarSprite != null)
            {
                playerAvatarImage.sprite = avatarSprite;
                playerAvatarImage.enabled = true;
            }
            else
            {
                playerAvatarImage.enabled = false;
            }
        }

        /// <summary>
        /// Shows a "+N" crystal reward on this card (e.g. for the winning player).
        /// </summary>
        public void ShowCrystalReward(int crystalCount)
        {
            if (!crystalRewardRoot || !crystalRewardText) return;
            crystalRewardText.text = $"+{crystalCount}";
            crystalRewardRoot.SetActive(true);
            RefreshDataPanelsRoot();
        }

        public void HideCrystalReward()
        {
            if (crystalRewardRoot) crystalRewardRoot.SetActive(false);
            RefreshDataPanelsRoot();
        }

        /// <summary>
        /// Optional secondary stat line (e.g. "Jousts: 3" or "Crystals: 12").
        /// </summary>
        public void ShowSecondaryStat(string statText)
        {
            if (secondaryStatText)
            {
                secondaryStatText.text = statText;
                secondaryStatText.gameObject.SetActive(true);
            }
            RefreshDataPanelsRoot();
        }

        public void HideSecondaryStat()
        {
            if (secondaryStatText) secondaryStatText.gameObject.SetActive(false);
            RefreshDataPanelsRoot();
        }

        /// <summary>
        /// The shared DataPanels background is the parent of BOTH the secondary stat
        /// and the crystal-reward line, so it must be visible when either child is
        /// showing and hidden only when neither is - otherwise an empty panel renders
        /// behind cards that have no extra stats (the old code never hid it). Driven
        /// from the child active-states so it stays correct regardless of the order in
        /// which Show/Hide are called (e.g. a winner with no secondary stat).
        /// </summary>
        void RefreshDataPanelsRoot()
        {
            if (!dataPanelsRoot) return;
            bool anyChildShown =
                (secondaryStatText && secondaryStatText.gameObject.activeSelf) ||
                (crystalRewardRoot && crystalRewardRoot.activeSelf);
            dataPanelsRoot.SetActive(anyChildShown);
        }

        /// <summary>
        /// In-game live score update. Animates a counter roll + punch.
        /// </summary>
        public void UpdateScore(int crystalCount) => ScoreAnimator.AnimateTo(crystalCount);

        private void ApplyDomainColor(Color domainColor)
        {
            if (backgroundImage)
            {
                var c = domainColor;
                c.a = backgroundTintAlpha;
                backgroundImage.color = c;
            }

            if (domainIndicatorImage)
            {
                domainIndicatorImage.gameObject.SetActive(true);
                domainIndicatorImage.color = domainColor;
            }
        }

        private void PlayEntrance(int staggerIndex)
        {
            EnsureCanvasGroup();
            _entranceSeq?.Kill();
            _entranceSeq = CardEntranceAnimator.Play(transform, _canvasGroup, animSettings, staggerIndex);
        }

        private void OnDestroy()
        {
            _scoreAnimator?.Kill();
            _entranceSeq?.Kill();
        }
    }
}
