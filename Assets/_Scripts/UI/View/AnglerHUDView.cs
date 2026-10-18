using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Angler's HUD readouts (design: <c>R_VesselActions/ANGLER.md</c> §7). The row order is the fleet's
    /// <b>charge · mass · space · time</b>, and the approved map (2026-10-16, <c>ElementalAbilityMaps/Angler.asset</c>) fills it
    /// as <b>Erase / Lock-on · Lure, Tame &amp; Grow · Paint · Dash to the School</b>.
    ///
    /// <para>One gauge per card:</para>
    /// <list type="number">
    /// <item><b>Charge</b> - the share of the school hunting a locked vessel; READY while LT erases.</item>
    /// <item><b>Mass</b> - the furthest-along taming in the light; READY while the lure is out.</item>
    /// <item><b>Space</b> - the picked squad's share out on a painted order; READY while RT paints with something painted.</item>
    /// <item><b>Time</b> - full when the dash is ready (a school to dash to, no dash in flight); READY then.</item>
    /// </list>
    /// The gauges are named for their element; each keeps the serialized name it had in round 2 (FormerlySerializedAs),
    /// so the HUD prefab's wiring survives the rename. Colour here is a GAUGE channel; the upgrade lives on the lockup's
    /// card, so this view only re-anchors its captured rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).
    /// </summary>
    public class AnglerHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: Erase / Lock-on - the share of the school hunting a locked vessel.")]
        [SerializeField, FormerlySerializedAs("commandGauge")] Image chargeGauge;
        [Tooltip("Mass card: Lure, Tame & Grow - the furthest-along taming in the light.")]
        [SerializeField, FormerlySerializedAs("leashGauge")] Image massGauge;
        [Tooltip("Space card: Paint - the picked squad's share out on a painted order.")]
        [SerializeField, FormerlySerializedAs("lureGauge")] Image spaceGauge;
        [Tooltip("Time card: Dash to the School - full when the dash is ready.")]
        [SerializeField, FormerlySerializedAs("tameGauge")] Image timeGauge;

        [Header("Counts")]
        [Tooltip("The OMNI card's icon: a seed, the flora an omni crystal left (planted the next time the lure is let go). " +
                 "Its number is how many seeds are held; it is dim while none is.")]
        [SerializeField] Sprite seedSprite;
        [Tooltip("Font size of the numbers on the Mass card (fauna you lead) and the omni card (seeds held).")]
        [SerializeField, Min(6f)] float countFontSize = 22f;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("A card's ability is live or ready (erasing, the lure out, painting, the dash ready) - the palette's CTA lime.")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        /// <summary>Seat every readout at rest (nothing tamed, the light off). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(chargeGauge);
            Seat(massGauge);
            Seat(spaceGauge);
            Seat(timeGauge);
        }

        void Seat(Image gauge)
        {
            if (!gauge) return;
            gauge.fillAmount = 0f;
            gauge.color = gaugeColor;
        }

        // ---- the numbers: fauna on the Mass card, seeds on the omni card (built at runtime; the lockup re-homes them) ----

        const string SeedHostName = "AnglerSeedButton";
        const float SeedIconSize = 80f;
        Image _seedIcon;
        TextMeshProUGUI _seedCount, _faunaCount, _squadLabel;
        int _seedsShown = -1, _faunaShown = -1;
        string _squadShown;

        /// <summary>
        /// Builds the omni card's icon (the Dolphin's pattern: a vessel-built icon bound to the core card, because what the
        /// Angler's crystal leaves is a COUNT of seeds, not a picture) and the Mass card's fauna count. Idempotent by name.
        /// </summary>
        public override void EnsureGeneratedAbilityIcons()
        {
            base.EnsureGeneratedAbilityIcons();
            if (!_seedIcon)
            {
                var host = transform.Find(SeedHostName) as RectTransform;
                if (!host)
                {
                    host = new GameObject(SeedHostName, typeof(RectTransform)).GetComponent<RectTransform>();
                    host.SetParent(transform, false);
                }
                _seedIcon = ResolveGeneratedChild<Image>(host, "AnglerSeedIcon");
                var rt = _seedIcon.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(SeedIconSize, SeedIconSize);
                _seedIcon.sprite = seedSprite;
                _seedIcon.preserveAspect = true;
                _seedIcon.raycastTarget = false;
                _seedCount = MakeCount(rt, "SeedCount");
                BindCoreAbilityIcon(CoreAbility.OmniCrystal, _seedIcon);
                _seedsShown = -1;
            }
            if (!_faunaCount)
            {
                Image massIcon = null;
                for (int i = 0; i < abilityIcons.Count; i++)
                    if (abilityIcons[i].element == Element.Mass) massIcon = abilityIcons[i].icon;
                if (massIcon)
                {
                    _faunaCount = MakeCount(massIcon.rectTransform, "FaunaCount");
                    // The picked squad, over the Mass card. Blank while the whole school answers, so a pick (X) can never
                    // silently leave a kind out of every order (the playtest's "the QuadFish don't always listen").
                    _squadLabel = MakeCount(massIcon.rectTransform, "SquadLabel");
                    var rt = _squadLabel.rectTransform;
                    rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(160f, 28f);
                    _squadLabel.alignment = TextAlignmentOptions.Top;
                    _squadLabel.text = "";
                }
                _faunaShown = -1;
                _squadShown = null;
            }
        }

        TextMeshProUGUI MakeCount(RectTransform parent, string name)
        {
            var text = ResolveGeneratedChild<TextMeshProUGUI>(parent, name);
            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(48f, 28f);
            text.alignment = TextAlignmentOptions.BottomRight;
            text.fontSize = countFontSize;
            // Never wrap or ellipsise: an overflowing number is a loud, fixable fault, a truncated one a wrong reading.
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.margin = Vector4.zero;
            text.raycastTarget = false;
            text.text = "0";
            return text;
        }

        /// <summary>The fauna you lead, on the Mass card (the designer, 2026-10-17).</summary>
        public void SetFaunaCount(int count)
        {
            if (!_faunaCount) EnsureGeneratedAbilityIcons();
            if (!_faunaCount || count == _faunaShown) return;
            _faunaShown = count;
            _faunaCount.text = count.ToString();
        }

        /// <summary>The picked squad over the Mass card ("QuadFish x3"); blank while the whole school answers.</summary>
        public void SetSquad(string label)
        {
            if (!_squadLabel) EnsureGeneratedAbilityIcons();
            label ??= "";
            if (!_squadLabel || label == _squadShown) return;
            _squadShown = label;
            _squadLabel.text = label;
        }

        /// <summary>The seeds the omni crystals left, on the omni card; the icon is dim while none is held.</summary>
        public void SetFloraSeeds(int count)
        {
            if (!_seedIcon) EnsureGeneratedAbilityIcons();
            if (!_seedIcon || count == _seedsShown) return;
            _seedsShown = count;
            if (_seedCount) _seedCount.text = count > 0 ? count.ToString() : "";
            _seedIcon.color = count > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
        }

        public void SetCharge(float hunting01, bool erasing) => Push(chargeGauge, hunting01, erasing);
        public void SetMass(float tame01, bool luring) => Push(massGauge, tame01, luring);
        public void SetSpace(float ordered01, bool painting) => Push(spaceGauge, ordered01, painting);
        public void SetTime(float ready01, bool ready) => Push(timeGauge, ready01, ready);

        void Push(Image gauge, float fill01, bool ready)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, ready ? readyColor : gaugeColor, k);
        }

        /// <summary>
        /// Re-anchor every gauge's rest scale after the base's upgrade handling, or the next gauge write settles it back to
        /// the pre-upgrade scale and wipes the lockup's persistent bump (vessel contract rule 9).
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            var gauge = element switch
            {
                Element.Charge => chargeGauge,
                Element.Mass => massGauge,
                Element.Space => spaceGauge,
                Element.Time => timeGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
