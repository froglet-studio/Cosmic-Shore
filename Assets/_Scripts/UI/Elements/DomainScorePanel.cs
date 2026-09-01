using System.Collections.Generic;
using DG.Tweening;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One COLUMN of the in-game top bar: a single domain's aggregated objective score on top and
    /// a horizontal row of that team's player icons (humans + AI) directly underneath. Three of
    /// these packed side by side ARE the top bar - score row over icon row, divided per team.
    ///
    /// The column carries no PLATE: a rectangle behind each column re-draws a boundary the
    /// arrangement already states. What it carries instead is LIGHT - a soft team-coloured glow
    /// rising off the accent strip (<see cref="glowImage"/>), which says "this column is Jade"
    /// without adding an edge, and which can MOVE. It breathes continuously so the bar is alive
    /// while nothing is happening, and punches on a score change so the team that just scored is
    /// the one that catches your eye. <see cref="domainIndicatorImage"/> (the retired plate slot)
    /// is left unwired and its tint method no-ops.
    ///
    /// Sum-number animation is delegated to <see cref="ScoreNumberAnimator"/>; the glow's two
    /// tweens are owned here and killed in <see cref="OnDestroy"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DomainScorePanel : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text domainSumText;

        [Tooltip("Soft team-coloured glow behind the column - the background that replaces the " +
                 "plate. Tinted, breathed and punched here. Leave unassigned to skip.")]
        [SerializeField] private Image glowImage;
        [SerializeField] private Image domainIndicatorImage;
        [Tooltip("Optional thin accent strip painted with the domain's secondary ship color. Leave unassigned to skip.")]
        [SerializeField] private Image accentImage;
        [Tooltip("Alpha applied to the domain-color tint on the indicator image so the panel reads as a subtle background rather than a saturated block. 0 = invisible, 1 = fully opaque.")]
        [Range(0f, 1f)]
        [SerializeField] private float indicatorAlpha = 0.15f;
        [Tooltip("Alpha applied to the accent strip's tint. Higher than indicator alpha so the strip pops against the muted background.")]
        [Range(0f, 1f)]
        [SerializeField] private float accentAlpha = 0.85f;

        [Header("Avatars")]
        [Tooltip("Container the small per-player avatars are parented under (HorizontalLayoutGroup expected).")]
        [SerializeField] private Transform avatarContainer;
        [Tooltip("Prefab cloned once per teammate. A PlayerScoreEntry works (name + avatar) - its score field is left empty.")]
        [SerializeField] private PlayerScoreEntry avatarEntryPrefab;

        [Tooltip("Alpha applied to a TEAMMATE's chip tint. The local player's own chip is always " +
                 "fully opaque, which is what tells you which column is yours now that names are gone.")]
        [Range(0f, 1f)]
        [SerializeField] private float teammateChipAlpha = 0.45f;

        [Header("Glow")]
        [Tooltip("Alpha the glow rests at. The breath swings around this value.")]
        [Range(0f, 1f)]
        [SerializeField] private float glowRestAlpha = 0.5f;

        [Tooltip("How far the breath swings either side of the rest alpha, as a fraction of it.")]
        [Range(0f, 1f)]
        [SerializeField] private float glowBreathDepth = 0.28f;

        [Tooltip("Seconds for one full breath (dim -> bright -> dim).")]
        [Min(0.1f)]
        [SerializeField] private float glowBreathPeriod = 3.2f;

        [Tooltip("Alpha the glow punches to when this domain scores, before easing back.")]
        [Range(0f, 1f)]
        [SerializeField] private float glowScoreAlpha = 1f;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private ScoreNumberAnimator _sumAnimator;
        private readonly List<PlayerScoreEntry> _spawnedAvatars = new();
        private Tween _glowBreath;
        private Tween _glowPunch;
        private Color _glowColor = Color.white;

        public Domains Domain { get; private set; } = Domains.Blue;

        private ScoreNumberAnimator SumAnimator =>
            _sumAnimator ??= new ScoreNumberAnimator(domainSumText, animSettings);

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>
        /// Legacy single-color setup. Tints both the background and (if present)
        /// the accent strip with the same color. Use the <see cref="DomainColorSet"/>
        /// overload below for the modern multi-color theme look.
        /// </summary>
        public void Setup(Domains domain, Color domainColor, int initialSum)
        {
            Domain = domain;
            SumAnimator.SetImmediate(initialSum);
            ApplyIndicatorColor(domainColor, indicatorAlpha);
            ApplyAccentColor(domainColor, accentAlpha);
            ArmGlow(domainColor);
        }

        /// <summary>
        /// Modern setup. Pulls multiple colors out of the per-domain theme
        /// palette so the panel reads as a designed info chip rather than a
        /// single flat tint:
        ///   * background indicator → <see cref="DomainColorSet.ShipColor1"/>
        ///     at <see cref="indicatorAlpha"/> (subtle, muted backdrop)
        ///   * accent strip → <see cref="DomainColorSet.ShipColor2"/> at
        ///     <see cref="accentAlpha"/> (bright pop)
        ///   * sum text → <see cref="DomainColorSet.BrightCrystalColor"/>
        ///     so the number reads as the team's signature color.
        /// </summary>
        public void Setup(Domains domain, DomainColorSet colorSet, int initialSum)
        {
            Domain = domain;

            if (colorSet == null)
            {
                // Theme palette unavailable - fall back to neutral white sum and hide the accent.
                SumAnimator.SetImmediate(initialSum);
                if (domainIndicatorImage) domainIndicatorImage.gameObject.SetActive(true);
                if (accentImage) accentImage.gameObject.SetActive(false);
                return;
            }

            var textColor = colorSet.BrightCrystalColor;
            textColor.a = 1f;
            SumAnimator.SetBaseColor(textColor);
            SumAnimator.SetImmediate(initialSum);

            ApplyIndicatorColor(colorSet.ShipColor1, indicatorAlpha);
            ApplyAccentColor(colorSet.ShipColor2, accentAlpha);

            // The glow takes the BRIGHT crystal colour, the same one the number wears, so the
            // column reads as one lit object rather than a number sitting on a differently-tinted
            // wash. ShipColor1 is the muted hull tone and is far too dark to carry light.
            ArmGlow(colorSet.BrightCrystalColor);
        }

        void ApplyIndicatorColor(Color color, float alpha)
        {
            if (!domainIndicatorImage) return;
            domainIndicatorImage.gameObject.SetActive(true);
            color.a = alpha;
            domainIndicatorImage.color = color;
        }

        void ApplyAccentColor(Color color, float alpha)
        {
            if (!accentImage) return;
            accentImage.gameObject.SetActive(true);
            color.a = alpha;
            accentImage.color = color;
        }

        /// <summary>
        /// Add one teammate's icon to this column's row.
        ///
        /// NO NAME is drawn, for anybody - the icon is the identity. A name rendered under one
        /// avatar and not the others made the local player's column a different HEIGHT from the
        /// rest, so the row stopped reading as one divided block, and it is the only text in the
        /// top bar that carries no number. The local player is marked instead by their chip taking
        /// the domain colour at full strength while teammates sit at
        /// <see cref="teammateChipAlpha"/> - Style Foundation section 3, "your avatar chip, team
        /// colour", expressed as the one channel that survives at chip size.
        /// </summary>
        public void AddPlayerIcon(Sprite avatar, Color domainColor, bool isLocalPlayer)
        {
            if (!avatarContainer || !avatarEntryPrefab) return;
            var entry = Instantiate(avatarEntryPrefab, avatarContainer);
            entry.Populate(string.Empty, string.Empty, avatar);

            var chip = domainColor;
            chip.a = isLocalPlayer ? 1f : teammateChipAlpha;
            entry.SetDomainIndicator(chip);

            _spawnedAvatars.Add(entry);
        }

        public void ClearAvatars()
        {
            foreach (var a in _spawnedAvatars)
                if (a) Destroy(a.gameObject);
            _spawnedAvatars.Clear();
        }

        /// <summary>
        /// Arm the glow: tint it, park it at rest, and start the breath. Idempotent - a rebuild
        /// re-arms rather than stacking a second breath tween on the same image.
        /// </summary>
        void ArmGlow(Color color)
        {
            if (!glowImage) return;

            _glowColor = color;
            _glowColor.a = glowRestAlpha;
            glowImage.gameObject.SetActive(true);
            glowImage.color = _glowColor;

            _glowBreath?.Kill();
            if (glowBreathDepth <= 0f || glowBreathPeriod <= 0f) return;

            // Yoyo between the two ends of the swing, starting from the DIM end so a freshly
            // built bar brightens into view instead of fading out of it.
            float lo = Mathf.Clamp01(glowRestAlpha * (1f - glowBreathDepth));
            float hi = Mathf.Clamp01(glowRestAlpha * (1f + glowBreathDepth));
            SetGlowAlpha(lo);
            _glowBreath = glowImage
                .DOFade(hi, glowBreathPeriod * 0.5f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject)
                .SetUpdate(animSettings == null || animSettings.useUnscaledTime);
        }

        void SetGlowAlpha(float a)
        {
            if (!glowImage) return;
            var c = _glowColor;
            c.a = a;
            glowImage.color = c;
        }

        public void UpdateSum(int newSum)
        {
            SumAnimator.AnimateTo(newSum);
            PunchGlow();
        }

        /// <summary>
        /// Flare the glow on a score change, then hand the column back to its breath. The breath
        /// is paused rather than killed, so the punch cannot leave the light stuck at full and a
        /// rapid run of scores re-triggers cleanly instead of stacking.
        /// </summary>
        void PunchGlow()
        {
            if (!glowImage) return;

            _glowPunch?.Kill();
            _glowBreath?.Pause();

            bool unscaled = animSettings == null || animSettings.useUnscaledTime;
            SetGlowAlpha(Mathf.Clamp01(glowScoreAlpha));
            _glowPunch = glowImage
                .DOFade(Mathf.Clamp01(glowRestAlpha), 0.45f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .SetUpdate(unscaled)
                .OnComplete(() => _glowBreath?.Play());
        }

        void OnDestroy()
        {
            _sumAnimator?.Kill();
            _glowBreath?.Kill();
            _glowPunch?.Kill();
        }
    }
}
