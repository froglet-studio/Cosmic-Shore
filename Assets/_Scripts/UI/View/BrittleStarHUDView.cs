using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Brittle Star's HUD readouts (design: <c>R_VesselActions/BRITTLE_STAR.md</c> §4). The row order IS the element
    /// map: <b>Charge Mode (charge) · Creation Mode (mass) · Space Mode (space) · Speed Mode (time)</b>.
    ///
    /// <para>One gauge per card, each answering the one question its card poses. No card turns the CTA lime for "on"
    /// (Garrett, 2026-11-01: "i don't put the CTA color as the on color, it's too much"): the card's own parts say the
    /// mode is on, and a gauge keeps its resting colour unless something is wrong (the guns overheated).</para>
    /// <list type="number">
    /// <item><b>Charge</b> — how many rings can I throw? The spare-ring stock (0, half, full of two).</item>
    /// <item><b>Space</b> — how hot are the guns? Heat 0..1, in the HOT colour while overheated.</item>
    /// <item><b>Creation</b>, <b>Speed</b> — each mode's switch, eased 0..1: a SWITCH moving between its ends, never a
    /// meter being spent (the Stoat's Mass card).</item>
    /// </list>
    /// Colour here is a GAUGE channel. <see cref="SetAbilityUpgraded"/> re-anchors the captured rest scales (vessel rule 9)
    /// and keeps the four level-5 flags: each card's level-5 state is the ACT the upgrade unlocks, a part that appears
    /// only once the upgrade is live (Garrett's icon rule, ability-icon LESSONS 46, 47, 67) - the sabers thrown
    /// (Thrown Sabers), the blocks as shielded prisms (Shielded Trail), a burst where the rounds meet (Explosive
    /// Rounds), the trail swept round a turn under a roll arc (Speed Roll).
    /// </summary>
    public class BrittleStarHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: the spare-ring stock (one throw each).")]
        [SerializeField] Image ringGauge;
        [Tooltip("Mass card: Creation Mode's switch.")]
        [SerializeField] Image creationGauge;
        [Tooltip("Space card: the guns' heat.")]
        [SerializeField] Image destructionGauge;
        [Tooltip("Time card: Speed Mode's switch.")]
        [SerializeField] Image speedGauge;

        [Header("Charge card: the five cords (icon_kit specs/brittlestar.py, Garrett picked C 2026-11-01)")]
        [Tooltip("The cords loose, a whip each (BrittleStar_ChargeWhips), white: fades out as RT pulls.")]
        [SerializeField] Image chargeWhipsIcon;
        [Tooltip("The cords pulled straight (BrittleStar_ChargeSabers), white: fades in as RT pulls.")]
        [SerializeField] Image chargeSabersIcon;
        [Tooltip("The Brittle Star nose-on (BrittleStar_ChargeStar), the domain colour.")]
        [SerializeField] Image chargeStarIcon;
        [Tooltip("Alpha of the cords while Charge Mode is off.")]
        [SerializeField, Range(0f, 1f)] float cordsOffAlpha = 0.25f;

        [Header("Mass card: the corner trails (icon_kit specs/brittlestar.py, Garrett picked A 2026-11-01)")]
        [Tooltip("Each corner's line of prisms coming at you (BrittleStar_MassTrails), white: fades out as Mass widens the blocks.")]
        [SerializeField] Image massTrailsIcon;
        [Tooltip("The same lines with Mass's fat blocks (BrittleStar_MassTrailsFat), white: fades in as Mass widens them.")]
        [SerializeField] Image massTrailsFatIcon;
        [Tooltip("The Brittle Star seen from behind (BrittleStar_MassStar), the domain colour.")]
        [SerializeField] Image massStarIcon;
        [Tooltip("Alpha of the trails while Creation Mode is off.")]
        [SerializeField, Range(0f, 1f)] float trailsOffAlpha = 0.25f;

        [Header("Space card: the five guns (icon_kit specs/brittlestar.py, Garrett picked A 2026-11-01)")]
        [Tooltip("The rounds splayed (BrittleStar_SpaceRounds), white: the aim stick pushed.")]
        [SerializeField] Image spaceRoundsIcon;
        [Tooltip("The rounds meeting ahead (BrittleStar_SpaceRoundsMeet), white: the aim stick centred.")]
        [SerializeField] Image spaceRoundsMeetIcon;
        [Tooltip("The rounds splayed at Space's doubled range (BrittleStar_SpaceRoundsFar), white.")]
        [SerializeField] Image spaceRoundsFarIcon;
        [Tooltip("The Brittle Star flying right (BrittleStar_SpaceStar), the domain colour.")]
        [SerializeField] Image spaceStarIcon;
        [Tooltip("Alpha of the rounds while Space Mode is off or the guns are overheated.")]
        [SerializeField, Range(0f, 1f)] float roundsOffAlpha = 0.25f;

        [Header("Time card: the stretch (icon_kit specs/brittlestar.py, Garrett picked A 2026-11-01)")]
        [Tooltip("The cruise trail, short blocks with gaps (BrittleStar_TimeTrailCruise), white: shown dim while Speed Mode is off.")]
        [SerializeField] Image timeTrailCruiseIcon;
        [Tooltip("The blocks stretched into one joined line (BrittleStar_TimeTrailFast), white: Speed Mode on.")]
        [SerializeField] Image timeTrailFastIcon;
        [Tooltip("Stretched further at Time's x8 (BrittleStar_TimeTrailFaster), white.")]
        [SerializeField] Image timeTrailFasterIcon;
        [Tooltip("The Brittle Star flying right (BrittleStar_TimeStar), the domain colour.")]
        [SerializeField] Image timeStarIcon;
        [Tooltip("Alpha of the cruise trail while Speed Mode is off.")]
        [SerializeField, Range(0f, 1f)] float cruiseTrailAlpha = 0.4f;

        [Header("Level-5 states (icon_kit specs/brittlestar.py, 2026-11-03; each shown only while its upgrade is live)")]
        [Tooltip("Charge 5 Thrown Sabers: the five sabers thrown out past their turrets, tumbling (BrittleStar_ChargeSabersFree), white: " +
                 "takes the sabers' alpha while the upgrade is live.")]
        [SerializeField] Image chargeSabersFreeIcon;
        [Tooltip("Mass 5 Shielded Trail: each corner's line as shielded prisms (BrittleStar_MassTrailsShielded), white.")]
        [SerializeField] Image massTrailsShieldedIcon;
        [Tooltip("Mass 5 Shielded Trail with Mass's fat blocks (BrittleStar_MassTrailsShieldedFat), white.")]
        [SerializeField] Image massTrailsShieldedFatIcon;
        [Tooltip("Space 5 Explosive Rounds: the burst where the rounds meet (BrittleStar_SpaceBurst), white.")]
        [SerializeField] Image spaceBurstIcon;
        [Tooltip("Space 5: the splayed rounds cleared round the burst (BrittleStar_SpaceRoundsBurst).")]
        [SerializeField] Sprite spaceRoundsBurstSprite;
        [Tooltip("Space 5: the meeting rounds cleared round the burst (BrittleStar_SpaceRoundsMeetBurst).")]
        [SerializeField] Sprite spaceRoundsMeetBurstSprite;
        [Tooltip("Space 5: the far rounds cleared round the burst (BrittleStar_SpaceRoundsFarBurst).")]
        [SerializeField] Sprite spaceRoundsFarBurstSprite;
        [Tooltip("Time 5 Speed Roll: the stretched trail swept round a turn (BrittleStar_TimeTrailTurn), white.")]
        [SerializeField] Image timeTrailTurnIcon;
        [Tooltip("Time 5 Speed Roll: the roll arc over the star (BrittleStar_TimeRoll), white.")]
        [SerializeField] Image timeRollIcon;

        [Header("Omni card: the spare-ring stock (icon_kit specs/brittlestar.py, Garrett picked C 2026-11-01)")]
        [Tooltip("The omni card with no spare ring held (BrittleStar_OmniStock0): two empty slots under the star.")]
        [SerializeField] Sprite omniStock0Sprite;
        [Tooltip("One spare held (BrittleStar_OmniStock1): one solid ring, one empty slot.")]
        [SerializeField] Sprite omniStock1Sprite;
        [Tooltip("Two spares held, the stock full (BrittleStar_OmniStock2): both rings solid.")]
        [SerializeField] Sprite omniStock2Sprite;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("The guns are overheated (the Space card).")]
        [SerializeField] Color hotColor = new(1f, 0.32f, 0.18f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        // The two gauge colours as the worn colour set paints them (a gauge eases to its colour every push, so a set worn
        // mid-flight is taken at the next one).
        Color GaugeColor => ColorTrace.Retrace(ColorTrace.Ui, gaugeColor);
        Color HotColor => ColorTrace.Retrace(ColorTrace.Ui, hotColor);

        /// <summary>Seat every readout at rest (no spare rings, cold guns, every mode off). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            // The level-5 flags are NOT reset here: the controller seeds them through SetAbilityUpgraded after the
            // first Initialize, and a re-run on a swap must not drop a live upgrade.
            CaptureRoundSprites();
            ApplyRoundSprites();
            Seat(ringGauge, 0f);
            Seat(creationGauge, 0f);
            Seat(destructionGauge, 0f);
            Seat(speedGauge, 0f);
            SetCords(0f, 0f, new Color(0f, 0f, 0f, 0f));
            SetCreationTrails(0f, 0f, new Color(0f, 0f, 0f, 0f));
            SetGuns(0f, 0f, 0f, false, new Color(0f, 0f, 0f, 0f));
            SetSpeedTrail(0f, 0f, new Color(0f, 0f, 0f, 0f));
        }

        void Seat(Image gauge, float fill)
        {
            if (!gauge) return;
            gauge.fillAmount = fill;
            gauge.color = GaugeColor;
        }

        public void SetCharge(float spareStock01, bool on) => Push(ringGauge, spareStock01, GaugeColor);
        public void SetCreation(float on01) => Push(creationGauge, on01, GaugeColor);
        public void SetSpace(float heat01, bool on, bool overheated) =>
            Push(destructionGauge, heat01, overheated ? HotColor : GaugeColor);
        public void SetSpeed(float on01) => Push(speedGauge, on01, GaugeColor);

        /// <summary>
        /// The Charge card's cords: whips while Charge Mode is on and RT is loose, crossfading into sabers as RT pulls
        /// (<paramref name="pull01"/>, <c>BrittleStarExecutor.CordPull01</c>); dim whips with the mode off
        /// (<paramref name="cordsOut01"/>, the cords' eased presence). The star wears <paramref name="domain"/>
        /// (alpha 0 = not resolved yet, keep the last).
        /// </summary>
        public void SetCords(float cordsOut01, float pull01, Color domain)
        {
            if (domain.a > 0f) _domain = domain;
            float out01 = Mathf.Clamp01(cordsOut01), pull = Mathf.Clamp01(pull01) * out01;
            float cords = Mathf.Lerp(cordsOffAlpha, 1f, out01);
            if (chargeWhipsIcon) chargeWhipsIcon.color = new Color(1f, 1f, 1f, cords * (1f - pull));
            // Charge 5: the rigid sabers are the ones LT can throw - drawn thrown, tumbling past their turrets.
            if (chargeSabersIcon) chargeSabersIcon.color = new Color(1f, 1f, 1f, _chargeUpgraded ? 0f : cords * pull);
            if (chargeSabersFreeIcon) chargeSabersFreeIcon.color = new Color(1f, 1f, 1f, _chargeUpgraded ? cords * pull : 0f);
            if (chargeStarIcon) chargeStarIcon.color = _domain;
        }

        /// <summary>
        /// The Mass card's corner trails: white while Creation Mode is on (<paramref name="on01"/>, its eased switch), dim
        /// with it off; crossfading to the fat blocks as Mass widens them (<paramref name="girth01"/>,
        /// <c>BrittleStarExecutor.BlockGirth01</c>). The star wears <paramref name="domain"/> (alpha 0 = keep the last).
        /// </summary>
        public void SetCreationTrails(float on01, float girth01, Color domain)
        {
            if (domain.a > 0f) _domain = domain;
            float trails = Mathf.Lerp(trailsOffAlpha, 1f, Mathf.Clamp01(on01)), fat = Mathf.Clamp01(girth01);
            // Mass 5: every block laid arrives shielded - the same lines drawn as shielded prisms.
            float plain = _massUpgraded ? 0f : 1f, shielded = 1f - plain;
            if (massTrailsIcon) massTrailsIcon.color = new Color(1f, 1f, 1f, plain * trails * (1f - fat));
            if (massTrailsFatIcon) massTrailsFatIcon.color = new Color(1f, 1f, 1f, plain * trails * fat);
            if (massTrailsShieldedIcon) massTrailsShieldedIcon.color = new Color(1f, 1f, 1f, shielded * trails * (1f - fat));
            if (massTrailsShieldedFatIcon) massTrailsShieldedFatIcon.color = new Color(1f, 1f, 1f, shielded * trails * fat);
            if (massStarIcon) massStarIcon.color = _domain;
        }

        /// <summary>
        /// The Space card's guns: the rounds meeting ahead with the aim stick centred, splayed as it is pushed
        /// (<paramref name="splay01"/>), the splayed rounds further out as Space doubles the range (<paramref name="range01"/>);
        /// dim with Space Mode off (<paramref name="on01"/>) or the guns <paramref name="overheated"/> (the plate goes hot).
        /// </summary>
        public void SetGuns(float on01, float splay01, float range01, bool overheated, Color domain)
        {
            if (domain.a > 0f) _domain = domain;
            float a = overheated ? roundsOffAlpha : Mathf.Lerp(roundsOffAlpha, 1f, Mathf.Clamp01(on01));
            float splay = Mathf.Clamp01(splay01), far = Mathf.Clamp01(range01);
            if (spaceRoundsMeetIcon) spaceRoundsMeetIcon.color = new Color(1f, 1f, 1f, a * (1f - splay));
            if (spaceRoundsIcon) spaceRoundsIcon.color = new Color(1f, 1f, 1f, a * splay * (1f - far));
            if (spaceRoundsFarIcon) spaceRoundsFarIcon.color = new Color(1f, 1f, 1f, a * splay * far);
            // Space 5: a round a prism stops bursts - the burst where the rounds meet, at the rounds' own alpha.
            if (spaceBurstIcon) spaceBurstIcon.color = new Color(1f, 1f, 1f, _spaceUpgraded ? a : 0f);
            if (spaceStarIcon) spaceStarIcon.color = _domain;
        }

        /// <summary>
        /// The Time card's trail: the gapped cruise blocks (dim) while Speed Mode is off, the stretched joined line as it
        /// comes on (<paramref name="on01"/>, its eased switch), stretched further as Time raises it x6 -> x8
        /// (<paramref name="boost01"/>).
        /// </summary>
        public void SetSpeedTrail(float on01, float boost01, Color domain)
        {
            if (domain.a > 0f) _domain = domain;
            float on = Mathf.Clamp01(on01), boost = Mathf.Clamp01(boost01);
            if (timeTrailCruiseIcon) timeTrailCruiseIcon.color = new Color(1f, 1f, 1f, cruiseTrailAlpha * (1f - on));
            // Time 5: in Speed Mode the right stick rolls and pulls - the stretched trail swept round a turn, the roll arc.
            float straight = _timeUpgraded ? 0f : 1f;
            if (timeTrailFastIcon) timeTrailFastIcon.color = new Color(1f, 1f, 1f, straight * on * (1f - boost));
            if (timeTrailFasterIcon) timeTrailFasterIcon.color = new Color(1f, 1f, 1f, straight * on * boost);
            float turn = _timeUpgraded ? on : 0f;
            if (timeTrailTurnIcon) timeTrailTurnIcon.color = new Color(1f, 1f, 1f, turn);
            if (timeRollIcon) timeRollIcon.color = new Color(1f, 1f, 1f, turn);
            if (timeStarIcon) timeStarIcon.color = _domain;
        }

        /// <summary>
        /// The omni card shows the spare-ring stock every crystal adds to: an empty slot or a solid ring per spare
        /// (<paramref name="spares"/>, 0..2, <c>BrittleStarExecutor.SpareRings</c>).
        /// </summary>
        public void SetSpareRings(int spares) =>
            SetOmniAbilitySprite(spares >= 2 ? omniStock2Sprite : spares == 1 ? omniStock1Sprite : omniStock0Sprite);

        Color _domain = Color.white;
        bool _chargeUpgraded, _massUpgraded, _spaceUpgraded, _timeUpgraded;
        Sprite _roundsSprite, _roundsMeetSprite, _roundsFarSprite;   // the round parts' base sprites, captured once

        /// <summary>Remember the Space card's base round sprites the first time, so Space 5 can swap them and swap back.</summary>
        void CaptureRoundSprites()
        {
            if (!_roundsSprite && spaceRoundsIcon) _roundsSprite = spaceRoundsIcon.sprite;
            if (!_roundsMeetSprite && spaceRoundsMeetIcon) _roundsMeetSprite = spaceRoundsMeetIcon.sprite;
            if (!_roundsFarSprite && spaceRoundsFarIcon) _roundsFarSprite = spaceRoundsFarIcon.sprite;
        }

        /// <summary>Space 5 draws the rounds cleared round the burst; below it, the base rounds. A missing upgraded sprite
        /// keeps the base one.</summary>
        void ApplyRoundSprites()
        {
            Swap(spaceRoundsIcon, _roundsSprite, spaceRoundsBurstSprite);
            Swap(spaceRoundsMeetIcon, _roundsMeetSprite, spaceRoundsMeetBurstSprite);
            Swap(spaceRoundsFarIcon, _roundsFarSprite, spaceRoundsFarBurstSprite);
        }

        void Swap(Image image, Sprite baseSprite, Sprite upgradedSprite)
        {
            if (!image) return;
            var sprite = _spaceUpgraded && upgradedSprite ? upgradedSprite : baseSprite;
            if (sprite && image.sprite != sprite) image.sprite = sprite;
        }

        void Push(Image gauge, float fill01, Color color)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, color, k);
        }

        /// <summary>
        /// Re-anchor every gauge's rest scale after the base's upgrade handling, or the next gauge write settles it back to
        /// the pre-upgrade scale and wipes the lockup's persistent bump (vessel contract rule 9).
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            switch (element)
            {
                case Element.Charge: _chargeUpgraded = upgraded; break;
                case Element.Mass: _massUpgraded = upgraded; break;
                case Element.Space:
                    _spaceUpgraded = upgraded;
                    CaptureRoundSprites();
                    ApplyRoundSprites();
                    break;
                case Element.Time: _timeUpgraded = upgraded; break;
            }
            var gauge = element switch
            {
                Element.Charge => ringGauge,
                Element.Mass => creationGauge,
                Element.Space => destructionGauge,
                Element.Time => speedGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
