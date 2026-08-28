using System.Collections.Generic;
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
    /// these side by side in one centred row ARE the top bar - score row over icon row, divided
    /// per team - which is why the column carries no background plate of its own: the division is
    /// the layout, and a plate behind each column re-draws a boundary the arrangement states.
    /// Both plate slots (<see cref="domainIndicatorImage"/>, <see cref="accentImage"/>) are
    /// therefore optional and left unwired on the shipped prefab; the tint methods no-op.
    ///
    /// Sum-number animation is delegated to <see cref="ScoreNumberAnimator"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class DomainScorePanel : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text domainSumText;
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

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private ScoreNumberAnimator _sumAnimator;
        private readonly List<PlayerScoreEntry> _spawnedAvatars = new();

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

        public void UpdateSum(int newSum) => SumAnimator.AnimateTo(newSum);

        void OnDestroy()
        {
            _sumAnimator?.Kill();
        }
    }
}
