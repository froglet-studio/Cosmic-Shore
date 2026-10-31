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

        [Header("Charge card: Erase / Lock-on (icon_kit specs/angler.py, Garrett picked B 2026-10-31)")]
        [Tooltip("The two creatures left in the school (Angler_LockSchool): the pilot's domain colour.")]
        [SerializeField] Image lockSchoolIcon;
        [Tooltip("The spent creature's wake (Angler_LockWake): white, shown while a hunter is out.")]
        [SerializeField] Image lockWakeIcon;
        [Tooltip("The third creature (Angler_LockHunter): the domain colour in the school, white once a lock spends it.")]
        [SerializeField] Image lockHunterIcon;
        [Tooltip("The rival (Angler_LockRival): the danger tint, dimmed until it is hunted.")]
        [SerializeField] Image lockRivalIcon;
        [Tooltip("The lock ring a hunted hull wears (Angler_LockRing): white, shown while a hunter is out.")]
        [SerializeField] Image lockRingIcon;
        [Tooltip("The rival's colour: the danger tint (ElementalBarsConfigSO.FireColor), as the Rhino's danger prisms.")]
        [SerializeField] Color lockDangerColor = new(1f, 0.3294f, 0.1020f, 1f);
        [Tooltip("The rival's alpha while nothing hunts it.")]
        [SerializeField, Range(0f, 1f)] float lockRivalIdleAlpha = 0.6f;
        [Tooltip("The wake's alpha while a hunter is out.")]
        [SerializeField, Range(0f, 1f)] float lockWakeAlpha = 0.6f;

        [Header("Mass card: Lure, Tame & Grow (icon_kit specs/angler.py, Garrett picked B 2026-10-31)")]
        [Tooltip("The Angler side-on (Angler_LureHull): the pilot's domain colour.")]
        [SerializeField] Image lureHullIcon;
        [Tooltip("The esca's glow (Angler_LureGlow): white, shown while the lure is held.")]
        [SerializeField] Image lureGlowIcon;
        [Tooltip("The lit esca bulb (Angler_LureBulb): white, shown while the lure is held.")]
        [SerializeField] Image lureBulbIcon;
        [Tooltip("The creature drawn to the bulb (Angler_LureFish): blue; danger at Mass 5 (Steal Fauna); the domain colour for a beat as one joins.")]
        [SerializeField] Image lureFishIcon;
        [Tooltip("The wild creature's colour (ElementalBarsConfigSO.BlueColor).")]
        [SerializeField] Color lureWildColor = new(56f / 255f, 130f / 255f, 1f, 1f);
        [Tooltip("The creature's alpha while the lure is not held.")]
        [SerializeField, Range(0f, 1f)] float lureIdleAlpha = 0.6f;
        [Tooltip("The glow's alpha while the lure is held.")]
        [SerializeField, Range(0f, 1f)] float lureGlowAlpha = 0.6f;
        [Tooltip("How long the creature wears the domain colour after one joins the school (s, unscaled).")]
        [SerializeField, Min(0f)] float lureJoinedSeconds = 1f;

        [Header("Space card: Paint (icon_kit specs/angler.py, Garrett picked D 2026-10-31)")]
        [Tooltip("Group A (Angler_PaintGroupA), above: white when the next paint sends it, else the domain colour, dimmed.")]
        [SerializeField] Image paintGroupAIcon;
        [Tooltip("Group B (Angler_PaintGroupB), below: white when the next paint sends it, else the domain colour, dimmed.")]
        [SerializeField] Image paintGroupBIcon;
        [Tooltip("The prism (Angler_PaintPrism): blue until something is painted, white (lit) after. Hidden at Space 5.")]
        [SerializeField] Image paintPrismIcon;
        [Tooltip("Space 5 Paint Shields: the cracked shield (Angler_PaintShell), the pilot's shielded colour.")]
        [SerializeField] Image paintShellIcon;
        [Tooltip("Space 5 Paint Shields: the painted prism inside the shield (Angler_PaintCore), white.")]
        [SerializeField] Image paintCoreIcon;
        [Tooltip("The waiting group's alpha (and its count's).")]
        [SerializeField, Range(0f, 1f)] float paintWaitingAlpha = 0.6f;
        [Tooltip("Font size of the two group counts on the Space card.")]
        [SerializeField, Min(6f)] float groupCountFontSize = 18f;
        [Tooltip("Where each group's count sits in the Space icon (normalized, from bottom-left): group A, then group B.")]
        [SerializeField] Vector2[] groupCountAnchors = { new(0.406f, 0.734f), new(0.406f, 0.266f) };

        /// <summary>Seat every readout at rest (nothing tamed, the light off). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(chargeGauge);
            Seat(massGauge);
            Seat(spaceGauge);
            Seat(timeGauge);
            _lockDomain = Color.white;
            _lockHunting = true;
            SetLockOn(false);
            _joinedUntil = 0f;
            _massUpgraded = false;
            _luring = true;
            SetLure(false);
            _sendShown = -1;
            _groupShown[0] = _groupShown[1] = -1;
            _spaceUpgraded = false;
            _painted = true;
            SetPaintPrism(false);
        }

        // ---- the Space card: the two groups that take turns, and the prism they eat ----

        readonly TextMeshProUGUI[] _groupCounts = new TextMeshProUGUI[2];
        readonly int[] _groupShown = { -1, -1 };
        int _sendShown = -1;
        bool _spaceUpgraded, _painted;
        Color _paintDomain = Color.white, _paintShielded = new(0f, 0f, 0f, 0f);

        /// <summary>The pilot's domain and shielded colours on the Space card (alpha 0 = not resolved yet, keep the last).</summary>
        public void SetPaintColors(Color domain, Color shielded)
        {
            if (domain.a > 0f && domain != _paintDomain) { _paintDomain = domain; _sendShown = -1; }
            if (shielded.a > 0f) { _paintShielded = shielded; if (paintShellIcon) paintShellIcon.color = shielded; }
        }

        /// <summary>
        /// Each group's size and which one the next paint sends (<c>AnglerCommandExecutor.SendGroup</c>, <c>GroupCount</c>):
        /// that group and its count are white, the other the domain colour at <see cref="paintWaitingAlpha"/>.
        /// </summary>
        public void SetPaintGroups(int countA, int countB, int sendGroup)
        {
            if (!_groupCounts[0]) EnsureGroupCounts();
            if (sendGroup != _sendShown)
            {
                _sendShown = sendGroup;
                var waiting = new Color(_paintDomain.r, _paintDomain.g, _paintDomain.b, paintWaitingAlpha);
                Paint(paintGroupAIcon, _groupCounts[0], sendGroup == 0 ? Color.white : waiting);
                Paint(paintGroupBIcon, _groupCounts[1], sendGroup == 1 ? Color.white : waiting);
            }
            SetGroupCount(0, countA);
            SetGroupCount(1, countB);
        }

        static void Paint(Image icon, TextMeshProUGUI count, Color c)
        {
            if (icon) icon.color = c;
            if (count) count.color = c;
        }

        void SetGroupCount(int g, int n)
        {
            if (!_groupCounts[g] || n == _groupShown[g]) return;
            _groupShown[g] = n;
            _groupCounts[g].text = n.ToString();
        }

        /// <summary>Whether anything is painted (lit): the prism turns from blue to white. At Space 5 the shielded prism shows.</summary>
        public void SetPaintPrism(bool painted)
        {
            if (painted == _painted) return;
            _painted = painted;
            if (paintPrismIcon)
            {
                paintPrismIcon.enabled = !_spaceUpgraded;
                paintPrismIcon.color = painted ? Color.white : lureWildColor;
            }
            if (paintShellIcon) paintShellIcon.enabled = _spaceUpgraded;
            if (paintCoreIcon)
            {
                paintCoreIcon.enabled = _spaceUpgraded;
                paintCoreIcon.color = painted ? Color.white : lureWildColor;
            }
        }

        /// <summary>The two counts, built at runtime beside the groups (the fauna count's pattern). Idempotent by name.</summary>
        void EnsureGroupCounts()
        {
            Image spaceIcon = null;
            for (int i = 0; i < abilityIcons.Count; i++)
                if (abilityIcons[i].element == Element.Space) spaceIcon = abilityIcons[i].icon;
            if (!spaceIcon) return;
            for (int g = 0; g < 2; g++)
            {
                var text = MakeCount(spaceIcon.rectTransform, g == 0 ? "GroupACount" : "GroupBCount");
                var rt = text.rectTransform;
                var at = g < groupCountAnchors.Length ? groupCountAnchors[g] : new Vector2(0.5f, 0.5f);
                rt.anchorMin = rt.anchorMax = rt.pivot = at;
                text.alignment = TextAlignmentOptions.Center;
                text.fontSize = groupCountFontSize;
                _groupCounts[g] = text;
            }
            _sendShown = -1;
            _groupShown[0] = _groupShown[1] = -1;
        }

        // ---- the Mass card: the esca lit, a creature drawn to it ----

        bool _luring, _massUpgraded;
        float _joinedUntil;

        /// <summary>The hull's colour on the Mass card: the pilot's domain (alpha 0 = not resolved yet, keep the last).</summary>
        public void SetLureColor(Color domain)
        {
            if (domain.a <= 0f) return;
            if (lureHullIcon) lureHullIcon.color = domain;
            _lureDomain = domain;
        }

        Color _lureDomain = Color.white;

        /// <summary>A creature just joined the school: the drawn creature wears the domain colour for a beat.</summary>
        public void PlayLureJoined() => _joinedUntil = Time.unscaledTime + lureJoinedSeconds;

        /// <summary>Whether the lure is held: the bulb lights and glows, the creature comes into the light.</summary>
        public void SetLure(bool luring)
        {
            _luring = luring;
            if (lureBulbIcon) lureBulbIcon.enabled = luring;
            if (lureGlowIcon)
            {
                lureGlowIcon.enabled = luring;
                lureGlowIcon.color = new Color(1f, 1f, 1f, lureGlowAlpha);
            }
            if (!lureFishIcon) return;
            Color c = Time.unscaledTime < _joinedUntil ? _lureDomain
                    : _massUpgraded ? lockDangerColor
                    : lureWildColor;
            if (!luring && Time.unscaledTime >= _joinedUntil) c.a *= lureIdleAlpha;
            lureFishIcon.color = c;
        }

        // ---- the Charge card: one creature spent per lock, out hunting the locked rival ----

        Color _lockDomain = Color.white;
        bool _lockHunting;

        /// <summary>The school's colour on the Charge card: the pilot's domain (alpha 0 = not resolved yet, keep the last).</summary>
        public void SetLockOnColor(Color domain)
        {
            if (domain.a <= 0f || domain == _lockDomain) return;
            _lockDomain = domain;
            if (lockSchoolIcon) lockSchoolIcon.color = domain;
            if (lockHunterIcon && !_lockHunting) lockHunterIcon.color = domain;
        }

        /// <summary>
        /// Whether a lock has spent a creature that is still out hunting (<c>AnglerCommandExecutor.HunterCount</c> &gt; 0): the
        /// third creature turns white and leaves its wake, the rival wears the lock ring at full danger. How many is the plate.
        /// </summary>
        public void SetLockOn(bool hunting)
        {
            if (hunting == _lockHunting) return;
            _lockHunting = hunting;
            if (lockHunterIcon) lockHunterIcon.color = hunting ? Color.white : _lockDomain;
            if (lockWakeIcon)
            {
                lockWakeIcon.enabled = hunting;
                lockWakeIcon.color = new Color(1f, 1f, 1f, lockWakeAlpha);
            }
            if (lockRingIcon) lockRingIcon.enabled = hunting;
            if (lockRivalIcon)
                lockRivalIcon.color = new Color(lockDangerColor.r, lockDangerColor.g, lockDangerColor.b,
                                                hunting ? lockDangerColor.a : lockDangerColor.a * lockRivalIdleAlpha);
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
        TextMeshProUGUI _seedCount, _faunaCount;
        int _seedsShown = -1, _faunaShown = -1;

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
                if (massIcon) _faunaCount = MakeCount(massIcon.rectTransform, "FaunaCount");
                _faunaShown = -1;
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
            if (element == Element.Space)
            {
                _spaceUpgraded = upgraded;   // Paint Shields: the shielded prism and its cracking shield
                bool p = _painted;
                _painted = !p;
                SetPaintPrism(p);
            }
            if (element == Element.Mass)
            {
                _massUpgraded = upgraded;   // Steal Fauna: the creature drawn in may be another Angler's
                SetLure(_luring);
            }
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
