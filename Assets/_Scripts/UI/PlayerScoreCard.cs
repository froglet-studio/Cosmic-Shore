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

        [Header("Style-B row (Docs/MENU_REDESIGN_B.md section 22.4) - all optional")]
        [Tooltip("The placing - '1', '2' - drawn from the card's order on the board.")]
        [SerializeField] private TMP_Text rankText;
        [Tooltip("The pilot's domain under their name, in the domain colour.")]
        [SerializeField] private TMP_Text domainText;
        [Tooltip("The row's slivered hairline: Light for the local pilot, Neutral Dark for everyone else.")]
        [SerializeField] private Image borderImage;
        [SerializeField] private CosmicShore.UI.UIColorToken localBorderToken = CosmicShore.UI.UIColorToken.TextLight;
        [SerializeField] private CosmicShore.UI.UIColorToken otherBorderToken = CosmicShore.UI.UIColorToken.SurfaceDark;
        [Tooltip("The first place's rank wears Light; every other rank Neutral Lightest.")]
        [SerializeField] private CosmicShore.UI.UIColorToken leaderRankToken = CosmicShore.UI.UIColorToken.TextLight;
        [SerializeField] private CosmicShore.UI.UIColorToken rankToken = CosmicShore.UI.UIColorToken.NeutralLightest;

        [Header("Rev 8 glass ladder (Docs/MENU_REDESIGN_B.md section 49) - all optional")]
        [Tooltip("The thin stripe down the row's left edge, in the pilot's domain colour.")]
        [SerializeField] private Image teamStripe;
        [Tooltip("The line along the row's foot: the pilot's share of the team's count. Hidden when there is none.")]
        [SerializeField] private RectTransform shareBar;
        [Tooltip("The row's glass fill; the local pilot's is tinted toward the selection blue.")]
        [SerializeField] private Image glassFill;
        [SerializeField] private CosmicShore.UI.UIColorToken glassToken = CosmicShore.UI.UIColorToken.SurfaceVeryDark;
        [Range(0f, 1f)]
        [SerializeField] private float glassAlpha = 0.8f;
        [Tooltip("How far the local pilot's glass leans toward the selection blue.")]
        [Range(0f, 1f)]
        [SerializeField] private float localGlassTint = 0.35f;
        [Tooltip("The border's light for a pilot on the winning team (ElementRim_Time), on another team " +
                 "(ElementRim_Space) and for the local pilot (SelectionRim). Empty: the hairline tokens above.")]
        [SerializeField] private Material wonBorderMaterial;
        [SerializeField] private Material lostBorderMaterial;
        [SerializeField] private Material localBorderMaterial;

        [Header("Animation (optional - falls back to defaults)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private bool _isLocal;
        private bool _teamWon = true;
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
            if (rankText)
            {
                rankText.text = (staggerIndex + 1).ToString();
                rankText.color = CosmicShore.UI.UITheme.Resolve(null, staggerIndex == 0 ? leaderRankToken : rankToken);
            }
            SetLocal(false);
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

        /// <summary>The local pilot's row wears the selection (and "(you)" after the name).</summary>
        public void SetLocal(bool isLocal)
        {
            _isLocal = isLocal;
            RefreshBorder();
            RefreshGlass();
            if (isLocal && playerNameText && !playerNameText.text.EndsWith(" (you)"))
                playerNameText.text += " (you)";
        }

        /// <summary>Whether the pilot's team won: the border runs Time's light for a winner, Space's otherwise.</summary>
        public void SetOutcome(bool teamWon)
        {
            _teamWon = teamWon;
            RefreshBorder();
        }

        /// <summary>The placing shown on the row (the results' rank; rows grouped by team are not in rank order).</summary>
        public void SetRank(int rank)
        {
            if (!rankText) return;
            rankText.text = rank.ToString();
            rankText.color = CosmicShore.UI.UITheme.Resolve(null, rank == 1 ? leaderRankToken : rankToken);
        }

        /// <summary>Hides the row's own score when its team header says it once for everyone.</summary>
        public void ShowScore(bool show)
        {
            if (playerScoreText) playerScoreText.gameObject.SetActive(show);
        }

        /// <summary>The pilot's share (0-1) of the team's count along the row's foot; below 0 hides the line.</summary>
        public void SetShare(float share)
        {
            if (!shareBar) return;
            bool show = share >= 0f;
            shareBar.gameObject.SetActive(show);
            if (!show) return;
            var max = shareBar.anchorMax;
            max.x = Mathf.Clamp(share, 0.02f, 1f);
            shareBar.anchorMax = max;
        }

        void RefreshBorder()
        {
            if (!borderImage) return;
            var material = _isLocal ? localBorderMaterial : _teamWon ? wonBorderMaterial : lostBorderMaterial;
            if (material)
            {
                borderImage.material = material;
                // The rim materials carry their own colour; the local selection takes the theme's blue.
                borderImage.color = CosmicShore.UI.UITheme.Resolve(null, _isLocal ? CosmicShore.UI.UIColorToken.Selection
                                                                                  : CosmicShore.UI.UIColorToken.TextLight);
                return;
            }
            borderImage.color = CosmicShore.UI.UITheme.Resolve(null, _isLocal ? localBorderToken : otherBorderToken);
        }

        void RefreshGlass()
        {
            if (!glassFill) return;
            var glass = CosmicShore.UI.UITheme.Resolve(null, glassToken);
            if (_isLocal)
                glass = Color.Lerp(glass, CosmicShore.UI.UITheme.Resolve(null, CosmicShore.UI.UIColorToken.Selection), localGlassTint);
            glass.a = glassAlpha;
            glassFill.color = glass;
        }

        /// <summary>The domain line under the name ("Jade"), in the domain colour.</summary>
        public void SetDomain(string label, Color color)
        {
            if (!domainText) return;
            domainText.text = label;
            domainText.color = color;
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

            if (teamStripe) teamStripe.color = domainColor;
            if (shareBar) shareBar.gameObject.SetActive(false);
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
