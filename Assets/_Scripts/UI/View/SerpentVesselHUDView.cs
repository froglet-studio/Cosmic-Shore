using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.UI
{
    public class SerpentVesselHUDView : VesselHUDView
    {
        [Header("SEED WALL")]
        [SerializeField] private Sprite[] shieldIconsByCount;
        [SerializeField] private Image   shieldIcon;

        [Header("Fuel pellet pips (TIME card)")]
        [Tooltip("The four lit pellets drawn over the TIME icon's slots, top to bottom. Pip n " +
                 "is full once the tank holds n pellets; the refilling pellet shows its partial fill.")]
        [SerializeField] private Image boostPip1;
        [SerializeField] private Image boostPip2;
        [SerializeField] private Image boostPip3;
        [SerializeField] private Image boostPip4;

        [Header("Pip Colors")]
        [SerializeField] private Color pipFullColor      = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color pipEmptyColor     = new Color(1f, 1f, 1f, 0.25f);

        [Tooltip("How fast the pips ease toward the tank's real level, in pellets per second. " +
                 "Fuel refills in one-second steps; this turns each step into a visible pour. " +
                 "A burn drops the pips at once - spending is never eased.")]
        [SerializeField, Min(0.01f)] private float pipFillRate = 1.5f;

        [Header("Fifth pellet (TIME 5)")]
        [Tooltip("The TIME card's slots re-laid as FIVE rows (PelletSlots5.png, written by " +
                 "Tools/Build/author_serpent_pellet_icon.py from the same art). Shown while Time 5 is " +
                 "active, when the tank holds five pellets.")]
        [SerializeField] private Sprite pelletSlotsFive;

        [Tooltip("Scale of each pellet row in the five-row layout. Must match the generator's FIVE_SCALE.")]
        [SerializeField, Range(0.5f, 1f)] private float fivePelletScale = 0.8f;

        [Tooltip("Vertical distance between pellet rows in the five-row layout, in canvas units. Must " +
                 "match the generator's FIVE_SPACING.")]
        [SerializeField, Min(1f)] private float fivePelletRowSpacing = 24.9f;

        [Header("Seed Wall card (MASS)")]
        [Tooltip("The wall's base bricks at their real size and spacing (Serpent_SeedWallBricks), in the pilot's " +
                 "domain colour (SetSeedWallColor).")]
        [SerializeField] private Image seedWallBricksIcon;
        [Tooltip("The seed, the wall's centre brick (Serpent_SeedWallSeed), white.")]
        [SerializeField] private Image seedWallSeedIcon;
        [Tooltip("Lockdown (Mass 5): the danger panels in the closed holes (Serpent_SeedWallPanels), shown only " +
                 "while the upgrade is active.")]
        [SerializeField] private Image seedWallPanelsIcon;
        [Tooltip("The panels' colour: the danger tint (ElementalBarsConfigSO.FireColor).")]
        [SerializeField] private Color seedWallPanelColor = new Color(1f, 0.3294f, 0.102f, 1f);

        [Header("Cloak / Sniper Shot card (SPACE)")]
        [Tooltip("The SPACE card's picture while the scope is down: the cloak (unscoped RT).")]
        [SerializeField] private Sprite spaceCloakSprite;
        [Tooltip("The SPACE card's picture while the scope is held: the sniper shot (scoped RT). The " +
                 "card swaps pictures in place so its trigger glyph stays where it is.")]
        [SerializeField] private Sprite spaceSniperSprite;

        bool _scopedShown;
        bool _scopedSeeded;

        Image[] _boostPips;
        float _pelletsShown = -1f;
        float _pelletsTarget;

        Image _pip5;
        Image _pelletSlots;
        Sprite _pelletSlotsFour;
        Vector2[] _fourPositions;

        public override void Initialize()
        {
            if (seedWallPanelsIcon)
            {
                seedWallPanelsIcon.color = seedWallPanelColor;
                seedWallPanelsIcon.enabled = IsAbilityUpgraded(Element.Mass);
            }
            BuildBoostPipCache();
            InitializeShieldHUD();
            InitializeBoostPips();
            SetFivePellets(IsAbilityUpgraded(Element.Time));
        }

        void BuildBoostPipCache()
        {
            if (_boostPips != null) return;

            _boostPips = new[] { boostPip1, boostPip2, boostPip3, boostPip4 };
        }

        // ---------- Public API for controller ----------

        /// <summary>The Seed Wall card's bricks, in the pilot's <paramref name="domain"/> colour.</summary>
        public void SetSeedWallColor(Color domain)
        {
            if (seedWallBricksIcon) seedWallBricksIcon.color = domain;
        }

        /// <summary>
        /// The right trigger cloaks unscoped and fires the sniper scoped, so the SPACE card shows
        /// whichever the trigger will do now. Both sprites must be wired for it to swap; with either
        /// missing the card keeps its authored picture.
        /// </summary>
        public void SetScoped(bool scoped)
        {
            if (_scopedSeeded && scoped == _scopedShown) return;
            _scopedSeeded = true;
            _scopedShown = scoped;
            if (!spaceCloakSprite || !spaceSniperSprite) return;
            if (TryGetAbilityIcon(Element.Space, out var icon) && icon)
                icon.sprite = scoped ? spaceSniperSprite : spaceCloakSprite;
        }

        /// <summary>Lockdown (Mass 5) seals the card's closed holes with their danger panels.</summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            if (element == Element.Mass && seedWallPanelsIcon) seedWallPanelsIcon.enabled = upgraded;
            if (element == Element.Time) SetFivePellets(upgraded);
        }

        /// <summary>
        /// Time 5 gives the tank a fifth pellet, so the TIME card lays its pellets out as five rows:
        /// the slots swap to the five-row art and the four pips shrink and close up, with a fifth
        /// (a copy of the first) under them. Off again, everything goes back where it was authored.
        /// </summary>
        void SetFivePellets(bool five)
        {
            if (!boostPip1 || !boostPip2 || !boostPip3 || !boostPip4) return;

            if (_fourPositions == null)
            {
                _fourPositions = new[]
                {
                    boostPip1.rectTransform.anchoredPosition, boostPip2.rectTransform.anchoredPosition,
                    boostPip3.rectTransform.anchoredPosition, boostPip4.rectTransform.anchoredPosition,
                };
                var host = boostPip1.transform.parent;
                if (host && host.TryGetComponent(out _pelletSlots)) _pelletSlotsFour = _pelletSlots.sprite;
            }

            if (five && !_pip5)
            {
                _pip5 = Instantiate(boostPip1, boostPip1.transform.parent);
                _pip5.name = "Line_5";
            }

            var four = new[] { boostPip1, boostPip2, boostPip3, boostPip4 };
            for (int i = 0; i < four.Length; i++)
            {
                var rt = four[i].rectTransform;
                rt.anchoredPosition = five
                    ? new Vector2(_fourPositions[i].x * fivePelletScale, (2 - i) * fivePelletRowSpacing)
                    : _fourPositions[i];
                rt.localScale = five ? new Vector3(fivePelletScale, fivePelletScale, 1f) : Vector3.one;
            }

            if (_pip5)
            {
                _pip5.gameObject.SetActive(five);
                var rt = _pip5.rectTransform;
                rt.anchoredPosition = new Vector2(_fourPositions[0].x * fivePelletScale, -2f * fivePelletRowSpacing);
                rt.localScale = new Vector3(fivePelletScale, fivePelletScale, 1f);
            }

            if (_pelletSlots)
                _pelletSlots.sprite = five && pelletSlotsFive ? pelletSlotsFive : _pelletSlotsFour;

            _boostPips = five ? new[] { boostPip1, boostPip2, boostPip3, boostPip4, _pip5 } : four;
            _pelletsShown = -1f;
            PaintPips(_pelletsTarget);
        }

        public void InitializeHUD()
        {
            InitializeShieldHUD();
            InitializeBoostPips();
        }

        void InitializeShieldHUD()
        {
            if (!shieldIcon) return;

            shieldIcon.enabled = true;
            if (!shieldIcon.gameObject.activeSelf)
                shieldIcon.gameObject.SetActive(true);
        }

        public void SetShieldCount(int shields)
        {
            if (!shieldIcon || shieldIconsByCount == null || shieldIconsByCount.Length < 5)
                return;

            shields = Mathf.Clamp(shields, 0, 4);
            var sprite = shieldIconsByCount[shields];
            if (!sprite) return;

            if (!shieldIcon.gameObject.activeSelf)
                shieldIcon.gameObject.SetActive(true);

            shieldIcon.enabled = true;
            shieldIcon.sprite  = sprite;
        }

        void InitializeBoostPips()
        {
            BuildBoostPipCache();
            _pelletsShown = -1f;
            foreach (var pip in _boostPips)
            {
                if (!pip) continue;
                pip.gameObject.SetActive(true);
                pip.enabled = true;
                pip.type = Image.Type.Filled;
            }
            PaintPips(_pelletsTarget);
        }

        /// <summary>
        /// The tank's level in pellets - 2.6 is two ready and a third 60% refilled. A drop (a burn)
        /// lands at once; a rise is poured in over <see cref="pipFillRate"/> so the one-second
        /// refill ticks read as fuel flowing rather than as a counter jumping.
        /// </summary>
        public void SetPelletFuel(float pellets)
        {
            BuildBoostPipCache();
            _pelletsTarget = Mathf.Max(0f, pellets);

            // Spending snaps; an inactive HUD (every hull but the local pilot's) never ticks
            // Update, so it settles too rather than holding a stale level.
            if (_pelletsShown < 0f || _pelletsTarget < _pelletsShown || !isActiveAndEnabled)
                PaintPips(_pelletsTarget);
        }

        void Update()
        {
            if (_pelletsShown < 0f || Mathf.Approximately(_pelletsShown, _pelletsTarget)) return;
            PaintPips(Mathf.MoveTowards(_pelletsShown, _pelletsTarget, pipFillRate * Time.deltaTime));
        }

        void PaintPips(float pellets)
        {
            _pelletsShown = pellets;
            if (_boostPips == null) return;

            for (int i = 0; i < _boostPips.Length; i++)
            {
                var pip = _boostPips[i];
                if (!pip) continue;

                float fill = Mathf.Clamp01(pellets - i);
                pip.fillAmount = fill;
                pip.color = fill >= 1f ? pipFullColor
                          : fill > 0f  ? Color.Lerp(pipEmptyColor, pipFullColor, 0.5f)   // refilling: lit, not yet burnable
                          : pipEmptyColor;
            }
        }
    }
}
