using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Lightweight in-game player score entry for the MiniGameHUD during gameplay.
    /// Shows avatar, name, live-updating score with counter roll and punch animations.
    /// Used by MiniGameHUD and MultiplayerHUD for real-time score tracking.
    /// For end-game scoreboard cards, see <see cref="PlayerScoreCard"/>.
    ///
    /// Score-number animation is delegated to <see cref="ScoreNumberAnimator"/> and
    /// the entrance to <see cref="CardEntranceAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PlayerScoreEntry : MonoBehaviour
    {
        [SerializeField] private Image avatarImage;
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private Image domainIndicatorImage;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private Sequence _entranceSeq;
        private ScoreNumberAnimator _scoreAnimator;

        private ScoreNumberAnimator ScoreAnimator =>
            _scoreAnimator ??= new ScoreNumberAnimator(scoreText, animSettings);

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Setup(string playerName, int initialScore, Color domainColor, bool isLocalPlayer, int staggerIndex = 0)
        {
            if (playerNameText) playerNameText.text = playerName;
            ScoreAnimator.SetImmediate(initialScore);

            if (domainIndicatorImage)
            {
                domainIndicatorImage.gameObject.SetActive(true);
                domainIndicatorImage.color = domainColor;
            }

            PlayEntrance(staggerIndex);
        }

        public void SetAvatar(Sprite avatarSprite)
        {
            if (!avatarImage) return;
            if (avatarSprite != null)
            {
                avatarImage.sprite = avatarSprite;
                avatarImage.enabled = true;
            }
            else
            {
                avatarImage.enabled = false;
            }
        }

        public void UpdateScore(int newScore) => ScoreAnimator.AnimateTo(newScore);

        /// <summary>
        /// Paint this entry's domain chip. Split out of <see cref="Setup"/> so the domain-column
        /// layout can mark the local player: that path <see cref="Populate"/>s rather than
        /// <see cref="Setup"/>s, because a chip carries no score of its own and must not run the
        /// score animator or the entrance stagger.
        /// </summary>
        public void SetDomainIndicator(Color domainColor)
        {
            if (!domainIndicatorImage) return;
            domainIndicatorImage.gameObject.SetActive(true);
            domainIndicatorImage.color = domainColor;
        }

        public void Populate(string playerName, string score, Sprite avatar = null)
        {
            if (playerNameText) playerNameText.text = playerName;
            if (scoreText) scoreText.text = score;
            SetAvatar(avatar);
        }

        public void Show(bool visible) => gameObject.SetActive(visible);

        void PlayEntrance(int staggerIndex)
        {
            if (!_canvasGroup)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _entranceSeq?.Kill();
            _entranceSeq = CardEntranceAnimator.Play(transform, _canvasGroup, animSettings, staggerIndex);
        }

        void OnDestroy()
        {
            _scoreAnimator?.Kill();
            _entranceSeq?.Kill();
        }
    }
}
