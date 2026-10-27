using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class SparrowHUDView : VesselHUDView
    {
        [Header("Missiles")]
        [SerializeField] private Sprite[] missileIcons;
        [SerializeField] private Image missileIcon;

        [Tooltip("Seconds the missile charge gauge takes to travel to a new value. Short - the " +
                 "gauge moves once per prism destroyed, so anything long would still be catching " +
                 "up when the next one lands - but never zero: nothing on this HUD pops.")]
        [SerializeField] private float missileChargeTweenDuration = 0.12f;

        [Tooltip("Extra time the gauge takes to travel when it RESETS (a rocket was just earned, " +
                 "so the fill runs off the top and starts again from empty). Slower than an " +
                 "ordinary step because it is the beat worth seeing.")]
        [SerializeField] private float missileChargeResetTweenDuration = 0.3f;

        [Header("Afterburner - the Time card")]
        [Tooltip("The Sparrow itself, seen from above (its real mesh, nose right): the part that TEACHES " +
                 "the card. Painted in the pilot's domain colour, and SWAPPED between the two sprites " +
                 "below as the boost is held and released - the hull folds its fans for speed, which is " +
                 "the ability. The plate under it shows no meter: the boost is indefinite, and the " +
                 "strafing roll's armed pip that filled it was retired on 2026-10-26 (Garrett: \"lets " +
                 "not show that that is armed because its cooldown is so low\").")]
        [SerializeField] private Image afterburnerHullIcon;
        [Tooltip("The hull at rest: fans spread (Sparrow_AfterburnerRest).")]
        [SerializeField] private Sprite afterburnerRestSprite;
        [Tooltip("The hull boosting: fans folded, the SpeedUp take (Sparrow_AfterburnerHullBoost).")]
        [SerializeField] private Sprite afterburnerBoostSprite;
        [Tooltip("The exhaust part behind the hull, so the card says MOTION even at rest (Garrett " +
                 "2026-10-26: \"show some jets coming out of the sparrow so the default state communicates " +
                 "motion\"). Tinted the HUD's BLUE (the shared config's BlueColor - \"give them a blue tint\"), " +
                 "never the domain colour; swapped with the hull: short idling plumes at rest, nearly twice " +
                 "as long while boosting.")]
        [SerializeField] private Image afterburnerJetsIcon;
        [SerializeField] private Sprite afterburnerJetsRestSprite;
        [SerializeField] private Sprite afterburnerJetsBoostSprite;
        [Tooltip("Shared colour spec; the jets wear its blueColor. Loaded from Resources when left empty; " +
                 "never author a second copy of the colour.")]
        [SerializeField] private ElementalBarsConfigSO barsConfig;
        [SerializeField] private string barsConfigResourcePath = "ElementalBarsConfig";
        [Tooltip("Fallback jets colour when the shared config cannot be resolved.")]
        [SerializeField] private Color afterburnerJetsFallbackColor = new(0.22f, 0.51f, 1f, 1f);
        [Tooltip("Scale punch on the hull the instant it folds, so the fold reads even at a glance.")]
        [SerializeField, Min(0f)] private float afterburnerFoldPunchScale = 0.2f;
        [SerializeField, Min(0.01f)] private float afterburnerFoldPunchDuration = 0.25f;
        [Tooltip("Fallback colour of the hull part until the controller pushes the pilot's domain colour.")]
        [SerializeField] private Color afterburnerHullFallbackColor = Color.white;

        [SerializeField] private Image weaponModeIcon;
        [SerializeField] private Sprite[] weaponModeIcons = new Sprite[2];

        [Header("Turret Stance - the Mass card")]
        [Tooltip("The Sparrow itself, stopped (fans spread), from above, nose right - its real mesh " +
                 "(Sparrow_TurretStanceHull). Painted in the pilot's domain colour.")]
        [SerializeField] private Image stanceHullIcon;
        [Tooltip("The rounds leaving both gun pods - a round, then the same round swollen, because Mass is " +
                 "the substance of what you fire. A GHOST while flying, solid once the stance is taken " +
                 "(the stationary-mode event), and at Mass 5 the shielded sprite in the domain's shielded " +
                 "colour: fired prisms arrive shielded, and a shielded prism reads as an octahedron " +
                 "(Garrett 2026-10-26: \"b is good\").")]
        [SerializeField] private Image stanceRoundsIcon;
        [Tooltip("The rounds as bare prisms (Sparrow_TurretStanceRounds).")]
        [SerializeField] private Sprite stanceRoundsSprite;
        [Tooltip("The rounds at Mass 5: octahedrons (Sparrow_TurretStanceRoundsShielded).")]
        [SerializeField] private Sprite stanceRoundsShieldedSprite;
        [Tooltip("Alpha of the rounds while the stance is NOT held - a ghost of what the stance fires.")]
        [SerializeField, Range(0f, 1f)] private float stanceRoundsGhostAlpha = 0.6f;
        [Tooltip("Scale punch on the rounds the instant the stance is taken.")]
        [SerializeField, Min(0f)] private float stancePunchScale = 0.2f;
        [SerializeField, Min(0.01f)] private float stancePunchDuration = 0.25f;
        [Tooltip("Fallback colour of the hull and the bare rounds until the controller pushes the pilot's domain colour.")]
        [SerializeField] private Color stanceFallbackColor = Color.white;

        [Header("Gun Heat")]
        [Tooltip("Thickness, in canvas units, of the marks drawn across the guns card's heat " +
                 "gauge at each phase transition of the spread curve (accuracy lost, each cap " +
                 "reached, each plateau expiring).")]
        [SerializeField, Min(0.5f)] private float gunHeatMarkThickness = 2f;

        [Tooltip("A phase join the heat has already passed is a GAP cut through the fill, so the fill reads as one " +
                 "bar per level reached (Garrett 2026-10-27: the boundaries should read without dominating the " +
                 "card). The plate showing through - match AbilityLockupStyle's gauge track colour.")]
        [SerializeField] private Color gunHeatGapColor = new(0.086f, 0.094f, 0.129f, 0.9f);

        [Tooltip("A phase join still ahead of the heat: a short tick in from each slanted edge of the plate.")]
        [SerializeField] private Color gunHeatMarkAheadColor = new(1f, 1f, 1f, 0.35f);

        [Tooltip("Length, in canvas units, of an ahead tick, in from each edge of the gauge.")]
        [SerializeField, Min(1f)] private float gunHeatTickLength = 7f;

        [Tooltip("How far the plate's slanted edge sits in from the gauge's rect at the BOTTOM of the card, in " +
                 "canvas units (AbilityLockupStyle's trapezoid inset): a tick starts at the slant at its own " +
                 "height, so the stencil never clips it.")]
        [SerializeField, Min(0f)] private float gunHeatTickSlantInset = 9f;

        [Header("Gun Spray (the Space card's icon)")]
        [Tooltip("One cannon round, centred in its sprite. The view clones it once per entry of Gun Spray Rounds " +
                 "and lays the clones out on their rays inside the LIVE cone. Empty = the card keeps the legacy " +
                 "weapon-mode sprite swap.")]
        [SerializeField] private Sprite gunSprayRoundSprite;

        [Tooltip("The round the turret stance fires (a bare prism), swapped onto every clone while the stance is " +
                 "held. Prisms fly square-on, so these clones are never turned.")]
        [SerializeField] private Sprite gunSprayPrismRoundSprite;

        [Tooltip("Space 5 (Piercing Bullets): a hostile prism on the centre line, drawn under the rounds so they " +
                 "read as flying through it.")]
        [SerializeField] private Sprite gunSprayPierceSprite;

        [Tooltip("Where the rounds leave the gun, in AUTHORED icon pixels (y down) - the icon kit's SPRAY_MUZZLE.")]
        [SerializeField] private Vector2 gunSprayMuzzle = new(2f, 64f);

        [Tooltip("The authored icon size the muzzle and distances are measured in (the kit draws at 128).")]
        [SerializeField, Min(1f)] private float gunSprayAuthoredSize = 128f;

        [Tooltip("Each round: its distance from the muzzle (authored px) and where it sits across the cone " +
                 "(-1 = lower edge, +1 = upper edge). Every rank puts one round on an edge so the angle is drawn. " +
                 "The icon kit's SPRAY_RANKS (Tools/Build/icon_kit/specs/sparrow.py).")]
        [SerializeField] private GunSprayRound[] gunSprayRounds =
        {
            new(20f, 1f), new(20f, -0.4f), new(42f, -1f), new(42f, 0.3f), new(64f, 1f),
            new(64f, -0.55f), new(86f, -1f), new(86f, 0.6f), new(110f, 1f), new(110f, -1f),
        };

        [Tooltip("The CARD's half-angle, in degrees, at each of the gun's three real caps (sustainable, blow-out, " +
                 "collapse). The real angles are too small to see until the blow-out, so each level gets a " +
                 "readable angle and the card interpolates the live value between them (SprayCardAngle).")]
        [SerializeField] private float[] gunSprayCardAngles = { 8f, 19f, 30f };

        [Tooltip("The rounds' colour until the controller's first push names the pilot's domain.")]
        [SerializeField] private Color gunSprayFallbackColor = Color.white;

        [Tooltip("Opacity of the Space 5 prism (it is a target, not the subject).")]
        [SerializeField, Range(0f, 1f)] private float gunSprayPierceAlpha = 0.6f;

        [Tooltip("The Space 5 prism's danger tint when the elemental bars config is missing.")]
        [SerializeField] private Color gunSprayPierceFallbackColor = new(1f, 0.33f, 0.1f, 1f);

        [Header("Blocked Input Highlights")]
        [SerializeField] private Color blockedInputColor = Color.red;
        [SerializeField] private float blockedPulseDuration = 0.4f;

        readonly Dictionary<InputEvents, Tween> _blockTweens = new();
        private Tween _afterburnerPunchTween;
        private bool _boosting;
        private Color _afterburnerHullColor = Color.white;

        private Tween _stancePunchTween;
        private bool _inStance;
        private bool _stanceShielded;
        private Color _stanceColor = Color.white;
        private Color _stanceShieldedColor = new(0f, 0f, 0f, 0f);   // alpha 0 = not resolved yet: keep the domain colour
        private Tween _missileChargeTween;
        private float _missileCharge = -1f;

        const string GunHeatGaugeName = "GunHeatGauge";
        private Image _gunHeatGauge;
        private float _gunHeat = -1f;
        readonly List<float> _gunHeatJoins = new();
        readonly List<HeatJoinMarks> _gunHeatMarks = new();

        /// <summary>One round of the Space card's spray: how far from the muzzle, and where across the cone.</summary>
        [System.Serializable]
        public struct GunSprayRound
        {
            [Tooltip("Distance from the muzzle, in authored icon pixels.")]
            public float distance;

            [Tooltip("Where the round sits across the cone: -1 the lower edge, 0 the centre line, +1 the upper edge.")]
            [Range(-1f, 1f)] public float side;

            public GunSprayRound(float distance, float side)
            {
                this.distance = distance;
                this.side = side;
            }
        }

        // A phase join's three Images: the gap cut through the fill once passed, the two edge ticks while ahead.
        struct HeatJoinMarks
        {
            public Image Gap, TickLeft, TickRight;
        }

        const string GunSprayName = "GunSpray";
        private RectTransform _gunSprayHost;
        private Image _gunSprayPierce;
        readonly List<Image> _gunSprayImages = new();
        private float _gunSprayAngle = -1f;
        private bool _gunSprayStance;
        private bool _gunSprayPierced;
        private Color _gunSprayColor = Color.white;
        private Vector2 _gunSprayLaidSize;

        public override void Initialize()
        {
            if (missileIcon)
                missileIcon.enabled = false;

            // The hull part sits at rest, fans spread, in the fallback colour until the controller's
            // first push names the pilot's domain.
            _boosting = false;
            _afterburnerHullColor = afterburnerHullFallbackColor;
            if (afterburnerHullIcon)
            {
                if (afterburnerRestSprite) afterburnerHullIcon.sprite = afterburnerRestSprite;
                afterburnerHullIcon.color = _afterburnerHullColor;
                afterburnerHullIcon.rectTransform.localScale = Vector3.one;
            }
            // The stance parts: the hull and ghost rounds in the fallback colour until the first push.
            _inStance = false;
            _stanceShielded = false;
            _stanceColor = stanceFallbackColor;
            if (stanceHullIcon) stanceHullIcon.color = _stanceColor;
            if (stanceRoundsIcon)
            {
                if (stanceRoundsSprite) stanceRoundsIcon.sprite = stanceRoundsSprite;
                stanceRoundsIcon.rectTransform.localScale = Vector3.one;
            }
            PaintStanceRounds();

            if (afterburnerJetsIcon)
            {
                if (afterburnerJetsRestSprite) afterburnerJetsIcon.sprite = afterburnerJetsRestSprite;
                EnsureBarsConfig();
                afterburnerJetsIcon.color = barsConfig ? barsConfig.BlueColor : afterburnerJetsFallbackColor;
            }

            if (weaponModeIcon)
                weaponModeIcon.enabled = false;

            // The Space card's spray: one stream, not yet in the stance, in the fallback colour until the first push.
            _gunSprayAngle = -1f;
            _gunSprayStance = false;
            _gunSprayColor = gunSprayFallbackColor;
            LayOutGunSpray(0f);

            foreach (var h in highlights.Where(h => h.image))
            {
                h.image.enabled = false;
                h.image.color = Color.white;
            }
        }

        #region Missiles

        public void InitializeMissileIcon()
        {
            if (!missileIcon || missileIcons == null || missileIcons.Length == 0)
                return;

            var sprite = missileIcons[^1];
            if (!sprite) return;

            missileIcon.sprite = sprite;
            missileIcon.enabled = true;
        }

        /// <summary>
        /// How close the NEXT rocket is, 0..1, on the Charge card's gauge — the ability lockup
        /// has already re-homed that Image into the card and masked it to the trapezoid, so this
        /// only ever writes <c>fillAmount</c>, exactly like every other vessel's meter.
        ///
        /// <para>Distinct from <see cref="SetMissilesFromAmmo01"/>, which says how many rockets
        /// the bay HOLDS: the icon ladder is the count, this is the charge. Earning a rocket
        /// therefore fills this to the top and starts it again from empty — the reset IS the
        /// signal that one was created, so it is deliberately given its own longer travel rather
        /// than snapping.</para>
        /// </summary>
        public void SetMissileCharge(float charge01)
        {
            if (!TryGetAbilityGauge(Element.Charge, out var gauge) || !gauge) return;

            charge01 = Mathf.Clamp01(charge01);
            if (Mathf.Approximately(charge01, _missileCharge)) return;

            // A DROP means the RACK changed - a rocket rolled over into the bay, or one was
            // fired - so the bar is starting again rather than stepping. That is worth watching,
            // and a rise is one prism's worth, which is not.
            bool reset = charge01 < _missileCharge;
            _missileCharge = charge01;

            _missileChargeTween?.Kill();
            _missileChargeTween = gauge
                .DOFillAmount(charge01, reset ? missileChargeResetTweenDuration
                                              : missileChargeTweenDuration)
                .SetEase(reset ? Ease.InOutQuad : Ease.OutQuad)
                .SetLink(gauge.gameObject);
        }

        /// <summary>
        /// How many rockets the bay HOLDS right now, drawn from the authored icon ladder.
        ///
        /// <para><b>It counts ROCKETS, not tank fraction.</b> It used to spread <c>ammo01</c>
        /// across the sprite range and ROUND, which conflates two different quantities and was
        /// wrong even at the old one-cost weapon: at cost 0.5 a 0.75-full tank holds ONE rocket
        /// and <c>round(0.75 x 2)</c> drew TWO. Now it is <c>floor(ammo / cost)</c> — the same
        /// arithmetic <see cref="CosmicShore.Gameplay.FireGunActionExecutor.ChargeToNextShot"/>
        /// takes the fractional part of, so the ladder and the charge gauge are two readings of
        /// one division and cannot disagree.</para>
        ///
        /// <para>The count is CLAMPED to the art: the ladder has three sprites (0/1/2) while the
        /// bay now holds four base rockets, so a full bay reads as the top sprite. That
        /// under-reports rather than lying about which rocket is next, and closing it is an ART
        /// task (five sprites), not a code one.</para>
        /// </summary>
        /// <param name="cost01">One BASE shot's cost as a fraction of the full tank. 0 =
        /// unknown, in which case the ladder falls back to the tank fraction it used to show.</param>
        public void SetMissilesFromAmmo01(float ammo01, float cost01)
        {
            if (!missileIcon || missileIcons == null || missileIcons.Length == 0)
                return;

            var sprite = missileIcons[LadderState(ammo01, cost01, missileIcons.Length)];
            if (!sprite) return;

            missileIcon.sprite = sprite;
            missileIcon.enabled = true;
        }

        /// <summary>
        /// Which rung of the icon ladder a tank shows, as a pure function so it can be tested
        /// without a canvas — the sibling of
        /// <see cref="CosmicShore.Gameplay.FireGunActionExecutor.ChargeToNextShot"/>, which takes
        /// the FRACTIONAL part of the same division.
        /// </summary>
        /// <param name="ammo01">How full the tank is, 0..1.</param>
        /// <param name="cost01">One BASE shot's cost as a fraction of the tank; 0 = unknown.</param>
        /// <param name="ladderLength">How many sprites the ladder has (rung 0 = empty).</param>
        public static int LadderState(float ammo01, float cost01, int ladderLength)
        {
            int maxState = Mathf.Max(0, ladderLength - 1);
            ammo01 = Mathf.Clamp01(ammo01);

            return cost01 > 0f
                ? Mathf.Clamp(Mathf.FloorToInt(ammo01 / cost01), 0, maxState)
                : Mathf.Clamp(Mathf.RoundToInt(ammo01 * maxState), 0, maxState);
        }

        #endregion

        #region Turret Stance
        /// <summary>
        /// Everything the Mass card's parts carry: the hull and the bare rounds in the pilot's domain
        /// colour, the shielded rounds in the domain's shielded colour (alpha 0 = unresolved, which
        /// falls back to the domain colour), the rounds at ghost alpha until the stance is held.
        /// </summary>
        void PaintStanceRounds()
        {
            if (!stanceRoundsIcon) return;
            var c = _stanceShielded && _stanceShieldedColor.a > 0f ? _stanceShieldedColor : _stanceColor;
            c.a = _inStance ? 1f : stanceRoundsGhostAlpha;
            stanceRoundsIcon.color = c;
            var sprite = _stanceShielded ? stanceRoundsShieldedSprite : stanceRoundsSprite;
            if (sprite) stanceRoundsIcon.sprite = sprite;
        }

        /// <summary>The pilot's domain colour for the hull and the bare rounds.</summary>
        public void SetStanceColors(Color domain, Color shielded)
        {
            if (_stanceColor == domain && _stanceShieldedColor == shielded) return;
            _stanceColor = domain;
            _stanceShieldedColor = shielded;
            if (stanceHullIcon) stanceHullIcon.color = domain;
            PaintStanceRounds();
        }

        /// <summary>
        /// Whether the turret stance is held (the stationary-mode event the controller already
        /// subscribes to for the Space card's bullets-to-prisms swap). Taking it turns the ghost
        /// rounds solid with a punch; releasing it fades them back.
        /// </summary>
        public void SetStance(bool inStance)
        {
            if (inStance == _inStance) return;
            _inStance = inStance;
            PaintStanceRounds();
            if (!stanceRoundsIcon || !inStance || stancePunchScale <= 0f) return;
            _stancePunchTween?.Kill();
            stanceRoundsIcon.rectTransform.localScale = Vector3.one;
            _stancePunchTween = stanceRoundsIcon.rectTransform
                .DOPunchScale(Vector3.one * stancePunchScale, stancePunchDuration, 1, 0.5f)
                .SetLink(stanceRoundsIcon.gameObject);
        }

        /// <summary>
        /// The base call does the card's upgrade mark; Mass 5 also changes WHAT the stance fires, so
        /// the rounds part swaps to its shielded shape and colour, and Space 5 shows the Space card's
        /// rounds piercing a prism.
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            if (element == Element.Space && upgraded != _gunSprayPierced)
            {
                // Space 5 (Piercing Bullets): the prism the rounds fly through appears on the centre line.
                _gunSprayPierced = upgraded;
                PaintGunSpray();
            }
            if (element != Element.Mass || upgraded == _stanceShielded) return;
            _stanceShielded = upgraded;
            PaintStanceRounds();
        }
        #endregion

        #region Afterburner
        /// <summary>
        /// The pilot's domain colour for the hull part. A colour, not a state: the hull wears its
        /// team at rest and boosting alike - what changes on this card is the SHAPE (the fold).
        /// </summary>
        public void SetAfterburnerHullColor(Color color)
        {
            if (_afterburnerHullColor == color) return;
            _afterburnerHullColor = color;
            if (afterburnerHullIcon) afterburnerHullIcon.color = color;
        }

        /// <summary>
        /// Whether the boost is held right now, polled by the controller off the vessel's status.
        /// The hull part swaps to its folded sprite while it is and back to the spread one when it
        /// is not; the fold gets a small punch. The boost itself is indefinite and has nothing to
        /// meter, so the card's plate stays bare.
        /// </summary>
        public void SetBoosting(bool boosting)
        {
            if (boosting == _boosting) return;
            _boosting = boosting;
            if (!afterburnerHullIcon) return;

            var sprite = boosting ? afterburnerBoostSprite : afterburnerRestSprite;
            if (sprite) afterburnerHullIcon.sprite = sprite;
            var jets = boosting ? afterburnerJetsBoostSprite : afterburnerJetsRestSprite;
            if (afterburnerJetsIcon && jets) afterburnerJetsIcon.sprite = jets;

            if (!boosting || afterburnerFoldPunchScale <= 0f) return;
            _afterburnerPunchTween?.Kill();
            _stancePunchTween?.Kill();
            afterburnerHullIcon.rectTransform.localScale = Vector3.one;
            _afterburnerPunchTween = afterburnerHullIcon.rectTransform
                .DOPunchScale(Vector3.one * afterburnerFoldPunchScale, afterburnerFoldPunchDuration, 1, 0.5f)
                .SetLink(afterburnerHullIcon.gameObject);
        }
        #endregion

        #region Gun Heat

        /// <summary>
        /// The guns card (SPACE - the Pulsefire Cannons) carries the gun's HEAT as the fleet's
        /// linear gauge. The HUD prefab authors no meter for it, so one is generated here and
        /// bound into the Space entry; the lockup then re-homes it into the card and masks it to
        /// the plate exactly like an authored gauge. An authored gauge always wins, and nothing is
        /// generated for a card with no icon (a locked card refuses a gauge). The card's icon is
        /// generated here too: the spray's round Images under the card's (transparent) icon.
        ///
        /// <para>Idempotent: the lockup may rebuild, and after the first build the gauge no
        /// longer lives under this transform, so it is held by reference.</para>
        /// </summary>
        public override void EnsureGeneratedAbilityIcons()
        {
            for (int i = 0; i < abilityIcons.Count; i++)
            {
                var binding = abilityIcons[i];
                if (binding.element != Element.Space) continue;
                if (!binding.icon || binding.gauge) break;   // locked card, or authored art wins

                if (!_gunHeatGauge)
                {
                    var host = (RectTransform)transform;
                    _gunHeatGauge = ResolveGeneratedChild<Image>(host, GunHeatGaugeName);
                    _gunHeatGauge.raycastTarget = false;
                    _gunHeatGauge.type = Image.Type.Filled;
                    _gunHeatGauge.fillMethod = Image.FillMethod.Vertical;
                    _gunHeatGauge.fillOrigin = (int)Image.OriginVertical.Bottom;
                    _gunHeatGauge.fillAmount = 0f;
                }

                binding.gauge = _gunHeatGauge;
                abilityIcons[i] = binding;
                break;
            }

            BuildGunSpray();
        }

        /// <summary>
        /// How hot the guns are, 0..1 of the heat ceiling (the spread curve's full-collapse
        /// point). Written straight onto the gauge: heat is a continuous value that already moves
        /// smoothly (it integrates frame by frame), so a tween would only lag it.
        /// </summary>
        public void SetGunHeat(float heat01)
        {
            if (!TryGetAbilityGauge(Element.Space, out var gauge) || !gauge) return;

            heat01 = Mathf.Clamp01(heat01);
            if (Mathf.Approximately(heat01, _gunHeat)) return;
            _gunHeat = heat01;

            gauge.fillAmount = heat01;
            PaintGunHeatMarks();
        }

        /// <summary>
        /// Where the spread curve changes phase, as fractions of the heat ceiling. Each join has a
        /// GAP cut through the fill once the heat has passed it, so the fill reads as one bar per
        /// level reached, and a short dim TICK in from each edge while it is still ahead - the card
        /// says WHICH level the gun is on and how far the next is, without lines across the whole
        /// card (Garrett 2026-10-27).
        /// </summary>
        public void SetGunHeatPhaseJoins(List<float> joins01)
        {
            if (!TryGetAbilityGauge(Element.Space, out var gauge) || !gauge) return;

            _gunHeatJoins.Clear();
            if (joins01 != null) _gunHeatJoins.AddRange(joins01);

            var parent = gauge.rectTransform;
            for (int i = 0; i < _gunHeatJoins.Count; i++)
            {
                if (i >= _gunHeatMarks.Count || !_gunHeatMarks[i].Gap)
                {
                    var marks = new HeatJoinMarks
                    {
                        Gap = ResolveGeneratedChild<Image>(parent, $"HeatPhaseGap{i}"),
                        TickLeft = ResolveGeneratedChild<Image>(parent, $"HeatPhaseTickL{i}"),
                        TickRight = ResolveGeneratedChild<Image>(parent, $"HeatPhaseTickR{i}"),
                    };
                    marks.Gap.raycastTarget = marks.TickLeft.raycastTarget = marks.TickRight.raycastTarget = false;
                    if (i < _gunHeatMarks.Count) _gunHeatMarks[i] = marks;
                    else _gunHeatMarks.Add(marks);
                }

                float y = _gunHeatJoins[i];
                var m = _gunHeatMarks[i];
                float slant = gunHeatTickSlantInset * (1f - y);   // the plate narrows toward its base
                PlaceHeatMark(m.Gap, 0f, 1f, y, 0f, 0f);
                PlaceHeatMark(m.TickLeft, 0f, 0f, y, gunHeatTickLength, slant);
                PlaceHeatMark(m.TickRight, 1f, 1f, y, gunHeatTickLength, -slant);
            }

            for (int i = _gunHeatJoins.Count; i < _gunHeatMarks.Count; i++)
            {
                var m = _gunHeatMarks[i];
                if (m.Gap) m.Gap.gameObject.SetActive(false);
                if (m.TickLeft) m.TickLeft.gameObject.SetActive(false);
                if (m.TickRight) m.TickRight.gameObject.SetActive(false);
            }

            PaintGunHeatMarks();
        }

        // A horizontal mark at height y01 of the gauge, spanning anchors x0..x1 (a stretched gap), or `width` canvas
        // units in from that edge (x0 == x1: a tick, pivoted on the edge so it grows inward, shifted `dx` to the slant).
        void PlaceHeatMark(Image img, float x0, float x1, float y01, float width, float dx)
        {
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(x0, y01);
            rt.anchorMax = new Vector2(x1, y01);
            rt.pivot = new Vector2(x0, 0.5f);
            rt.anchoredPosition = new Vector2(dx, 0f);
            rt.sizeDelta = new Vector2(width, gunHeatMarkThickness);
        }

        void PaintGunHeatMarks()
        {
            float heat = Mathf.Max(0f, _gunHeat);
            for (int i = 0; i < _gunHeatJoins.Count && i < _gunHeatMarks.Count; i++)
            {
                var m = _gunHeatMarks[i];
                if (!m.Gap) continue;
                bool passed = heat >= _gunHeatJoins[i];
                m.Gap.gameObject.SetActive(passed);
                m.TickLeft.gameObject.SetActive(!passed);
                m.TickRight.gameObject.SetActive(!passed);
                m.Gap.color = gunHeatGapColor;
                m.TickLeft.color = m.TickRight.color = gunHeatMarkAheadColor;
            }
        }

        #endregion

        #region Gun Spray

        /// <summary>
        /// The card's half-angle for the gun's REAL half-angle: piecewise-linear from the curve's
        /// caps (0, sustainable, blow-out, collapse) onto <paramref name="cardAngles"/> (0, then one
        /// per cap), clamped at the last. The real angles are a degree or two until the blow-out -
        /// invisible on a 60 px card - so each level gets a readable angle and the live value moves
        /// between them. A cap the profile does not author (no blow-out, no collapse: it does not
        /// exceed the cap before it) is skipped, so the card holds at the widest level the gun reaches.
        /// </summary>
        public static float SprayCardAngle(float realHalfAngle, float sustainableCap, float blowoutCap,
                                           float collapseCap, IReadOnlyList<float> cardAngles)
        {
            if (cardAngles == null || cardAngles.Count == 0) return 0f;

            float real = Mathf.Max(0f, realHalfAngle);
            float prevReal = 0f, prevCard = 0f;
            int levels = Mathf.Min(3, cardAngles.Count);
            for (int i = 0; i < levels; i++)
            {
                float cap = i == 0 ? sustainableCap : i == 1 ? blowoutCap : collapseCap;
                if (cap <= prevReal) continue;
                if (real <= cap) return Mathf.Lerp(prevCard, cardAngles[i], (real - prevReal) / (cap - prevReal));
                prevReal = cap;
                prevCard = cardAngles[i];
            }
            return prevCard;
        }

        /// <summary>
        /// The gun's live spread, pushed every frame by the controller: the real half-angle and the
        /// profile's three caps (0 for a stage it does not author). Lays the rounds out on their rays.
        /// </summary>
        public void SetGunSpread(float realHalfAngle, float sustainableCap, float blowoutCap, float collapseCap)
        {
            float angle = SprayCardAngle(realHalfAngle, sustainableCap, blowoutCap, collapseCap, gunSprayCardAngles);
            // Re-laid when the angle moves OR the card's size does: the spray is generated before the lockup's
            // first layout pass, when the container still measures zero.
            if (Mathf.Abs(angle - _gunSprayAngle) < 0.05f && (!_gunSprayHost || _gunSprayHost.rect.size == _gunSprayLaidSize)) return;
            LayOutGunSpray(angle);
        }

        /// <summary>The pilot's domain colour for the rounds - yours, like the Mass card's.</summary>
        public void SetGunSprayColor(Color color)
        {
            if (_gunSprayColor == color) return;
            _gunSprayColor = color;
            PaintGunSpray();
        }

        // Find-or-create the spray under the Space card's icon (a transparent container since 2026-10-27): the
        // Space 5 prism first (it draws under), then one Image per round. Stretched to the container, so a round
        // sprite drawn at its own centre lands exactly where the authored layout puts it.
        void BuildGunSpray()
        {
            if (!weaponModeIcon || !gunSprayRoundSprite || gunSprayRounds == null || gunSprayRounds.Length == 0) return;
            if (_gunSprayHost && _gunSprayImages.Count == gunSprayRounds.Length) return;

            var parent = weaponModeIcon.rectTransform;
            var hostImage = ResolveGeneratedChild<Image>(parent, GunSprayName);   // a holder: it draws nothing
            hostImage.enabled = false;
            hostImage.raycastTarget = false;
            _gunSprayHost = hostImage.rectTransform;
            Stretch(_gunSprayHost);

            _gunSprayPierce = ResolveGeneratedChild<Image>(_gunSprayHost, "Pierce");
            _gunSprayPierce.raycastTarget = false;
            _gunSprayPierce.sprite = gunSprayPierceSprite;
            Stretch(_gunSprayPierce.rectTransform);

            _gunSprayImages.Clear();
            for (int i = 0; i < gunSprayRounds.Length; i++)
            {
                var img = ResolveGeneratedChild<Image>(_gunSprayHost, $"Round{i}");
                img.raycastTarget = false;
                Stretch(img.rectTransform);
                _gunSprayImages.Add(img);
            }

            float angle = Mathf.Max(0f, _gunSprayAngle);
            _gunSprayAngle = -1f;
            LayOutGunSpray(angle);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        // Each round on its own ray from the muzzle at (side x angle) degrees, turned to fly along it (a prism round
        // flies square-on). Authored pixels are y-down; the canvas is y-up.
        void LayOutGunSpray(float angle)
        {
            _gunSprayAngle = angle;
            if (!_gunSprayHost || _gunSprayImages.Count == 0) return;

            var size = _gunSprayHost.rect.size;
            _gunSprayLaidSize = size;
            float half = gunSprayAuthoredSize * 0.5f;
            float kx = size.x / gunSprayAuthoredSize, ky = size.y / gunSprayAuthoredSize;
            for (int i = 0; i < _gunSprayImages.Count && i < gunSprayRounds.Length; i++)
            {
                var img = _gunSprayImages[i];
                if (!img) continue;
                var round = gunSprayRounds[i];
                float a = angle * round.side;
                float rad = a * Mathf.Deg2Rad;
                float px = gunSprayMuzzle.x + round.distance * Mathf.Cos(rad);
                float py = gunSprayMuzzle.y - round.distance * Mathf.Sin(rad);
                var rt = img.rectTransform;
                rt.anchoredPosition = new Vector2((px - half) * kx, (half - py) * ky);
                rt.localEulerAngles = new Vector3(0f, 0f, _gunSprayStance ? 0f : a);
            }
            PaintGunSpray();
        }

        void PaintGunSpray()
        {
            var sprite = _gunSprayStance && gunSprayPrismRoundSprite ? gunSprayPrismRoundSprite : gunSprayRoundSprite;
            foreach (var img in _gunSprayImages)
            {
                if (!img) continue;
                img.sprite = sprite;
                img.color = _gunSprayColor;
            }

            if (!_gunSprayPierce) return;
            bool show = _gunSprayPierced && gunSprayPierceSprite;
            _gunSprayPierce.enabled = show;
            if (!show) return;
            EnsureBarsConfig();
            var danger = barsConfig ? barsConfig.FireColor : gunSprayPierceFallbackColor;
            danger.a = gunSprayPierceAlpha;
            _gunSprayPierce.color = danger;
        }

        void EnsureBarsConfig()
        {
            if (!barsConfig && !string.IsNullOrEmpty(barsConfigResourcePath))
                barsConfig = Resources.Load<ElementalBarsConfigSO>(barsConfigResourcePath);
        }

        #endregion

        #region Weapon Mode

        /// <summary>
        /// The turret stance on the Space card: with the spray built, every round swaps to a prism
        /// round and stops turning; without it (no round sprite authored) the legacy whole-icon swap.
        /// </summary>
        public void SetWeaponMode(bool isStationary)
        {
            // Recorded first: the controller's first push can land before the lockup has generated the spray.
            bool changed = _gunSprayStance != isStationary;
            _gunSprayStance = isStationary;
            if (_gunSprayHost)
            {
                if (changed) LayOutGunSpray(Mathf.Max(0f, _gunSprayAngle));
                return;
            }

            if (!weaponModeIcon || weaponModeIcons == null || weaponModeIcons.Length < 2)
                return;

            var idx = isStationary ? 1 : 0;
            var sprite = weaponModeIcons[idx];
            if (!sprite) return;

            weaponModeIcon.sprite = sprite;
            weaponModeIcon.enabled = true;
        }

        #endregion

        #region Blocked Input Highlights

        public void HandleInputEventBlocked(InputEventBlockPayload payload)
        {
            if (payload.Started)
                StartBlockedHighlight(payload.Input);
            else if (payload.Ended)
                StopBlockedHighlight(payload.Input);
        }

        void StartBlockedHighlight(InputEvents input)
        {
            var img = FindHighlightImage(input);
            if (!img) return;

            // Kill existing tween for this input
            if (_blockTweens.TryGetValue(input, out var existing))
                existing?.Kill();

            img.enabled = true;
            img.color = blockedInputColor;

            // Pulse between blocked color and a dimmed version
            var dimColor = blockedInputColor * 0.5f;
            dimColor.a = 1f;
            _blockTweens[input] = img.DOColor(dimColor, blockedPulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        void StopBlockedHighlight(InputEvents input)
        {
            if (_blockTweens.TryGetValue(input, out var tween))
            {
                tween?.Kill();
                _blockTweens.Remove(input);
            }

            var img = FindHighlightImage(input);
            if (!img) return;

            img.color = Color.white;
            img.enabled = false;
        }

        Image FindHighlightImage(InputEvents ev)
        {
            for (var i = 0; i < highlights.Count; i++)
                if (highlights[i].input == ev)
                    return highlights[i].image;
            return null;
        }

        #endregion

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _afterburnerPunchTween?.Kill();
            _missileChargeTween?.Kill();
            foreach (var tween in _blockTweens.Values)
                tween?.Kill();
            _blockTweens.Clear();
        }
    }
}
