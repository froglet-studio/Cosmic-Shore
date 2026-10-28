using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Rhino's HUD view. Everything it shows lives on the fleet's four-card ability row
    /// (<see cref="VesselHUDView.abilityIcons"/>, drawn by the ability lockup), driven by
    /// <see cref="RhinoHUDController"/> through the base view's card pushes.
    ///
    /// <para><b>CHARGE card - the katana</b> (Garrett 2026-10-28). The card's icon is a transparent
    /// container; this view generates the sword under it: a pivot at the HILT holding one Image that
    /// shows either the curved katana or the straight one. The controller pushes the real sword's pose
    /// every frame (<see cref="SetKatanaPose"/>): the katana turns about the hilt with the swipe (a
    /// fraction of the real angle, so it stays inside the card), MIRRORS so its cutting edge leads the
    /// swing, shortens as it chops, and shows the straight blade - the sword seen from behind - once both
    /// triggers are in the chop. White while dark, danger while lit (<see cref="SetKatanaLit"/>).
    /// Sprites and the hilt come from Tools/Build/icon_kit/specs/rhino.py.</para>
    ///
    /// <para><b>MASS card - the afterimages</b> (Garrett 2026-10-28). The card's icon is a transparent
    /// container over three prefab parts: the sword (white) and two prism copies of it in the pilot's
    /// domain colour, shown only while the afterimages are being laid (<see cref="SetAfterimage"/>).</para>
    ///
    /// <para><b>SPACE card - Blade Reach</b> (Garrett 2026-10-28). A transparent container over a layout
    /// this view generates from five parts: the katana leaning across the card, its blade SCALED along
    /// itself to the live length (24 at rest -> 40 at Space 10), two posts at the rest length and the full
    /// reach, and a super-shielded prism (the flat stella octangula) at the live tip - whole below Space 5,
    /// where the blade binds on it, shattered along its facets once Shield Breaker is active
    /// (<see cref="SetBladeReach"/>).</para>
    ///
    /// <para><b>TIME card - Ramp Spool / Momentum</b> (Garrett 2026-10-28: "this ability is about the rhino and its
    /// invulnerability to danger prisms"). A transparent container over four prefab parts: the Rhino from above
    /// flying right (domain colour), a danger prism ahead of its nose, and - while the Time level-5 ward, Momentum,
    /// holds - a white chevron ward up in front of the nose with the prism glanced away (<see cref="SetMomentum"/>).
    /// The plate stays the speed meter; the ward's threshold (70% of top speed) is ONE mark on it, drawn the
    /// Sparrow's way: a short dim tick at each slanted edge while it is ahead, a gap through the fill once passed
    /// (<see cref="SetMomentumMark"/>).</para>
    ///
    /// <para>It used to carry a pre-lockup STATUS CLUSTER beside the row - a skimmer-size ring, a
    /// "slowed" counter, a slow-line flash, a debuff timer, laser and crystal indicators - wired
    /// through six fields here and three GameObjects the Rhino prefab added to this HUD. Those fields,
    /// GameObjects and channels were deleted on 2026-10-16 (Docs/ABILITY_LOCKUP.md, vessel skill rule 38).</para>
    /// </summary>
    public class RhinoVesselHUDView : VesselHUDView
    {
        const string KatanaPivotName = "KatanaPivot";
        const string KatanaName = "Katana";

        [Header("Charge card - the katana")]
        [Tooltip("The curved katana (Rhino_KatanaCurved), cutting edge on its LEFT, drawn on the full card.")]
        [SerializeField] private Sprite katanaCurvedSprite;
        [Tooltip("The straight katana seen from behind (Rhino_KatanaChop), shown once both triggers are in the chop.")]
        [SerializeField] private Sprite katanaChopSprite;
        [Tooltip("The size the sprites were drawn at (px). The hilt below is in these pixels.")]
        [SerializeField] private float katanaAuthoredSize = 128f;
        [Tooltip("Where the blade leaves the nose, in authored px from the sprite's top-left: the pivot the " +
                 "katana turns, mirrors and shortens about (specs/rhino.py KATANA_HILT).")]
        [SerializeField] private Vector2 katanaHilt = new(60f, 100f);
        [Tooltip("The fraction of the sword's real swipe the card turns (0.5: a full 90 deg pull turns the card 45).")]
        [SerializeField, Range(0f, 1f)] private float katanaSwipeFraction = 0.5f;
        [Tooltip("The chop (0..1 of the full both-triggers pitch) at and above which the straight blade shows. " +
                 "0.75 is a summed pull of 1.5, the energize stance's threshold.")]
        [SerializeField, Range(0f, 1f)] private float katanaChopStraight01 = 0.75f;
        [Tooltip("The blade's length on the card at a full chop, as a fraction of its rest length (it tips away).")]
        [SerializeField, Range(0.2f, 1f)] private float katanaChopLength01 = 0.8f;
        [Tooltip("The katana while the blade is dark.")]
        [SerializeField] private Color katanaDarkColor = Color.white;
        [Tooltip("The katana while the blade is lit (energized): the danger tint (ElementalBarsConfigSO.FireColor).")]
        [SerializeField] private Color katanaLitColor = new(1f, 0.3294f, 0.1020f, 1f);

        [Header("Mass card - the afterimages")]
        [Tooltip("The sword part (Rhino_AfterimageBlade), always shown, white.")]
        [SerializeField] private Image afterimageBladeIcon;
        [Tooltip("The nearer prism copy (Rhino_AfterimageNear), shown in the domain colour while the afterimages are laid.")]
        [SerializeField] private Image afterimageNearIcon;
        [Tooltip("The further prism copy (Rhino_AfterimageFar), shown in the domain colour while the afterimages are laid.")]
        [SerializeField] private Image afterimageFarIcon;
        [Tooltip("The nearer copy's opacity, as a fraction of the domain colour's.")]
        [SerializeField, Range(0f, 1f)] private float afterimageNearAlpha = 0.8f;
        [Tooltip("The further copy's opacity, as a fraction of the domain colour's.")]
        [SerializeField, Range(0f, 1f)] private float afterimageFarAlpha = 0.45f;

        [Header("Space card - Blade Reach")]
        [Tooltip("The tsuba and grip (Rhino_ReachHilt), upright on reachPartHilt.")]
        [SerializeField] private Sprite reachHiltSprite;
        [Tooltip("The blade at full reach (Rhino_ReachBlade), upright on reachPartHilt; scaled to the live length.")]
        [SerializeField] private Sprite reachBladeSprite;
        [Tooltip("The super-shielded prism (Rhino_ReachPrism), centred on its sprite.")]
        [SerializeField] private Sprite reachPrismSprite;
        [Tooltip("The prism shattered along its facets (Rhino_ReachPrismBroken), centred: Shield Breaker.")]
        [SerializeField] private Sprite reachPrismBrokenSprite;
        [Tooltip("The posts at the rest length and the full reach (Rhino_ReachPosts), in card space.")]
        [SerializeField] private Sprite reachPostsSprite;
        [Tooltip("Where the upright parts' hilt sits on their sprites, authored px from the top-left (specs/rhino.py PART_HILT).")]
        [SerializeField] private Vector2 reachPartHilt = new(64f, 100f);
        [Tooltip("Where the hilt sits on the card, authored px from the top-left (REACH_HILT).")]
        [SerializeField] private Vector2 reachCardHilt = new(20f, 112f);
        [Tooltip("The katana's lean, degrees clockwise from upright (REACH_ANGLE).")]
        [SerializeField] private float reachAngle = 52f;
        [Tooltip("The blade's length on the card at rest and at full reach, authored px (REACH_REST, REACH_MAX).")]
        [SerializeField] private Vector2 reachLengthRestMax = new(40.8f, 68f);
        [Tooltip("The prism's centre past the blade's tip, authored px (PRISM_ALONG).")]
        [SerializeField] private float reachPrismAlong = 13.12f;
        [Tooltip("The prism's centre to the blade's side per px of blade (PRISM_SIDE_PER_LENGTH; + is up-left).")]
        [SerializeField] private float reachPrismSidePerLength = -0.0913f;
        [Tooltip("The posts' opacity.")]
        [SerializeField, Range(0f, 1f)] private float reachPostsAlpha = 0.6f;

        [Header("Time card - Ramp Spool / Momentum")]
        [Tooltip("The Rhino from above, flying right (Rhino_SpoolHull), always shown in the domain colour.")]
        [SerializeField] private Image spoolHullIcon;
        [Tooltip("The chevron ward ahead of the nose (Rhino_SpoolWard), shown white while Momentum holds.")]
        [SerializeField] private Image spoolWardIcon;
        [Tooltip("The danger prism ahead of the nose (Rhino_SpoolPrism), shown until Momentum holds.")]
        [SerializeField] private Image spoolPrismIcon;
        [Tooltip("The danger prism glanced away (Rhino_SpoolGlance), shown while Momentum holds.")]
        [SerializeField] private Image spoolGlanceIcon;
        [Tooltip("The ward's colour.")]
        [SerializeField] private Color spoolWardColor = Color.white;
        [Tooltip("The danger prisms' colour: the danger tint (ElementalBarsConfigSO.FireColor), as the lit katana.")]
        [SerializeField] private Color spoolDangerColor = new(1f, 0.3294f, 0.1020f, 1f);

        [Tooltip("Thickness, in canvas units, of the Momentum threshold's marks on the Time gauge (the Sparrow's gunHeatMarkThickness).")]
        [SerializeField, Min(0.5f)] private float momentumMarkThickness = 2f;
        [Tooltip("The threshold once speed has passed it: a GAP cut through the fill - the plate showing through " +
                 "(match AbilityLockupStyle's gauge track colour).")]
        [SerializeField] private Color momentumGapColor = new(0.086f, 0.094f, 0.129f, 0.9f);
        [Tooltip("The threshold while speed is still below it: a short tick in from each slanted edge of the plate.")]
        [SerializeField] private Color momentumMarkAheadColor = new(1f, 1f, 1f, 0.35f);
        [Tooltip("Length, in canvas units, of an ahead tick, in from each edge of the gauge.")]
        [SerializeField, Min(1f)] private float momentumTickLength = 7f;
        [Tooltip("How far the plate's slanted edge sits in from the gauge's rect at the BOTTOM of the card, in canvas " +
                 "units (AbilityLockupStyle's trapezoid inset): a tick starts at the slant at its own height.")]
        [SerializeField, Min(0f)] private float momentumTickSlantInset = 9f;

        Image _momentumGap, _momentumTickLeft, _momentumTickRight;
        float _momentumThreshold01 = -1f;

        RectTransform _reachHost, _reachPivot, _reachBladeScale, _reachPrismRt;
        Image _reachHilt, _reachBlade, _reachPrism, _reachPosts;
        float _reach01 = -1f;

        RectTransform _katanaPivot;
        Image _katana;
        float _katanaFacing = 1f;   // +1: edge left (as drawn), -1: mirrored, edge right. Kept at rest.
        bool _katanaLit;

        public override void Initialize() { }

        /// <summary>
        /// The card's katana for a pose of the real sword: <paramref name="swipe"/> -1..1 (right trigger
        /// minus left, + is a swing to the right), <paramref name="chop01"/> 0..1 (the summed pull over 2).
        /// </summary>
        public void SetKatanaPose(float swipe, float chop01)
        {
            if (!_katanaPivot || !_katana) return;
            var pose = KatanaPose(swipe, chop01, _katanaFacing, katanaSwipeFraction * 90f, katanaChopStraight01, katanaChopLength01);
            _katanaFacing = pose.Facing;
            _katanaPivot.localRotation = Quaternion.Euler(0f, 0f, pose.AngleZ);
            _katanaPivot.localScale = new Vector3(pose.Facing, pose.Length01, 1f);
            var sprite = pose.Straight ? katanaChopSprite : katanaCurvedSprite;
            if (sprite && _katana.sprite != sprite) _katana.sprite = sprite;
        }

        /// <summary>White while the blade is dark, danger while it is lit.</summary>
        public void SetKatanaLit(bool lit)
        {
            _katanaLit = lit;
            if (_katana) _katana.color = lit ? katanaLitColor : katanaDarkColor;
        }

        /// <summary>
        /// The Mass card: the prism copies show, in the pilot's <paramref name="domain"/> colour, only while
        /// the afterimages are being <paramref name="laying"/>; the sword part is always shown, white.
        /// </summary>
        public void SetAfterimage(bool laying, Color domain)
        {
            if (afterimageBladeIcon) afterimageBladeIcon.color = Color.white;
            PaintCopy(afterimageNearIcon, laying, domain, afterimageNearAlpha);
            PaintCopy(afterimageFarIcon, laying, domain, afterimageFarAlpha);
        }

        static void PaintCopy(Image copy, bool laying, Color domain, float alpha)
        {
            if (!copy) return;
            copy.enabled = laying;
            domain.a *= alpha;
            copy.color = domain;
        }

        /// <summary>
        /// The Space card: the blade at <paramref name="reach01"/> of its range (0 rest, 1 Space 10), the prism
        /// shattered when <paramref name="breaker"/> (Shield Breaker active), the blade danger while
        /// <paramref name="lit"/>, the prism in <paramref name="prismColor"/> (alpha 0 keeps the authored colour).
        /// </summary>
        public void SetBladeReach(float reach01, bool breaker, bool lit, Color prismColor)
        {
            if (!_reachHost) return;
            var color = lit ? katanaLitColor : katanaDarkColor;
            if (_reachHilt) _reachHilt.color = color;
            if (_reachBlade) _reachBlade.color = color;
            if (_reachPosts) _reachPosts.color = new Color(1f, 1f, 1f, reachPostsAlpha);
            if (_reachPrism)
            {
                var sprite = breaker && reachPrismBrokenSprite ? reachPrismBrokenSprite : reachPrismSprite;
                if (_reachPrism.sprite != sprite) _reachPrism.sprite = sprite;
                if (prismColor.a > 0f) _reachPrism.color = prismColor;
            }
            reach01 = Mathf.Clamp01(reach01);
            if (Mathf.Approximately(reach01, _reach01) && _reachHost.rect.size == _reachLaidSize) return;
            _reach01 = reach01;
            LayOutReach();
        }

        Vector2 _reachLaidSize;

        /// <summary>Which Time-card parts show: the prism ahead until Momentum holds; then the ward and the glanced prism.</summary>
        public struct MomentumParts
        {
            public bool Prism, Glance, Ward;
        }

        /// <summary>What <see cref="SetMomentum"/> shows, as a pure function so it is tested without a canvas.</summary>
        public static MomentumParts MomentumPartsFor(bool warded) => new() { Prism = !warded, Glance = warded, Ward = warded };

        /// <summary>
        /// The Time card: the hull in the pilot's <paramref name="domain"/> colour, and the ward up with the danger
        /// prism glanced away while Momentum is <paramref name="warded"/>, the prism ahead of the nose otherwise.
        /// </summary>
        public void SetMomentum(bool warded, Color domain)
        {
            var parts = MomentumPartsFor(warded);
            if (spoolHullIcon) spoolHullIcon.color = domain;
            ShowPart(spoolPrismIcon, parts.Prism, spoolDangerColor);
            ShowPart(spoolGlanceIcon, parts.Glance, spoolDangerColor);
            ShowPart(spoolWardIcon, parts.Ward, spoolWardColor);
        }

        static void ShowPart(Image part, bool shown, Color color)
        {
            if (!part) return;
            part.enabled = shown;
            part.color = color;
        }

        /// <summary>
        /// The Momentum threshold on the Time gauge, <paramref name="threshold01"/> of top speed, against the live
        /// <paramref name="speed01"/>: a dim tick in from each slanted edge while it is ahead, a gap through the fill
        /// once passed (the Sparrow's heat marks, <c>SparrowHUDView.SetGunHeatPhaseJoins</c>). A threshold of 0 or
        /// less hides the mark.
        /// </summary>
        public void SetMomentumMark(float threshold01, float speed01)
        {
            if (!TryGetAbilityGauge(Element.Time, out var gauge) || !gauge) return;

            if (!_momentumGap)
            {
                var parent = gauge.rectTransform;
                _momentumGap = ResolveGeneratedChild<Image>(parent, "MomentumGap");
                _momentumTickLeft = ResolveGeneratedChild<Image>(parent, "MomentumTickL");
                _momentumTickRight = ResolveGeneratedChild<Image>(parent, "MomentumTickR");
                _momentumGap.raycastTarget = _momentumTickLeft.raycastTarget = _momentumTickRight.raycastTarget = false;
                _momentumThreshold01 = -1f;
            }

            if (!Mathf.Approximately(threshold01, _momentumThreshold01))
            {
                _momentumThreshold01 = threshold01;
                float slant = momentumTickSlantInset * (1f - threshold01);   // the plate narrows toward its base
                PlaceMomentumMark(_momentumGap, 0f, 1f, threshold01, 0f, 0f);
                PlaceMomentumMark(_momentumTickLeft, 0f, 0f, threshold01, momentumTickLength, slant);
                PlaceMomentumMark(_momentumTickRight, 1f, 1f, threshold01, momentumTickLength, -slant);
            }

            bool shown = threshold01 > 0f;
            bool passed = speed01 >= threshold01;
            _momentumGap.gameObject.SetActive(shown && passed);
            _momentumTickLeft.gameObject.SetActive(shown && !passed);
            _momentumTickRight.gameObject.SetActive(shown && !passed);
            _momentumGap.color = momentumGapColor;
            _momentumTickLeft.color = _momentumTickRight.color = momentumMarkAheadColor;
        }

        // A horizontal mark at height y01 of the gauge, spanning anchors x0..x1 (a stretched gap), or `width` canvas
        // units in from that edge (x0 == x1: a tick, pivoted on the edge so it grows inward, shifted `dx` to the slant).
        void PlaceMomentumMark(Image img, float x0, float x1, float y01, float width, float dx)
        {
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(x0, y01);
            rt.anchorMax = new Vector2(x1, y01);
            rt.pivot = new Vector2(x0, 0.5f);
            rt.anchoredPosition = new Vector2(dx, 0f);
            rt.sizeDelta = new Vector2(width, momentumMarkThickness);
        }

        /// <summary>
        /// Where the Space card's prism centre sits, in authored px from the card's top-left, for a blade
        /// <paramref name="length"/> px long: past the tip along the blade, leaning with its curve. Pure, so it
        /// is tested without a canvas (the twin of specs/rhino.py prism_centre).
        /// </summary>
        public static Vector2 ReachPrismCentre(Vector2 hilt, float angleDeg, float length, float along, float sidePerLength)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            float d = length + along, side = sidePerLength * length;
            return new Vector2(hilt.x + Mathf.Sin(a) * d - Mathf.Cos(a) * side,
                               hilt.y - Mathf.Cos(a) * d - Mathf.Sin(a) * side);
        }

        void LayOutReach()
        {
            var size = _reachHost.rect.size;
            _reachLaidSize = size;
            float px = katanaAuthoredSize > 0f ? katanaAuthoredSize : 128f;
            var k = new Vector2(size.x / px, size.y / px);

            // the hilt pivot: the upright parts' hilt moved onto the card's hilt, turned to the lean
            _reachPivot.anchoredPosition = new Vector2((reachCardHilt.x - reachPartHilt.x) * k.x,
                                                       -(reachCardHilt.y - reachPartHilt.y) * k.y);
            _reachPivot.localRotation = Quaternion.Euler(0f, 0f, -reachAngle);
            float length = Mathf.Lerp(reachLengthRestMax.x, reachLengthRestMax.y, _reach01);
            _reachBladeScale.localScale = new Vector3(1f, reachLengthRestMax.y > 0f ? length / reachLengthRestMax.y : 1f, 1f);

            var c = ReachPrismCentre(reachCardHilt, reachAngle, length, reachPrismAlong, reachPrismSidePerLength);
            _reachPrismRt.anchoredPosition = new Vector2((c.x - px * 0.5f) * k.x, -(c.y - px * 0.5f) * k.y);
        }

        /// <summary>What <see cref="SetKatanaPose"/> draws, as a pure function so it is tested without a canvas.</summary>
        public struct KatanaPoseResult
        {
            /// <summary>The pivot's z rotation (deg; negative is clockwise on screen).</summary>
            public float AngleZ;
            /// <summary>+1 as drawn (edge left), -1 mirrored about the hilt (edge right).</summary>
            public float Facing;
            /// <summary>The blade's length as a fraction of its rest length.</summary>
            public float Length01;
            /// <summary>True when the straight blade (seen from behind) shows.</summary>
            public bool Straight;
        }

        /// <summary>
        /// The katana's pose on the card. A swing to the right turns it clockwise and mirrors it so the edge
        /// (drawn on the left) leads; a swing left turns it back with the edge on the left; at rest it keeps
        /// <paramref name="facing"/>, the side it last faced, so letting go does not flip it.
        /// </summary>
        public static KatanaPoseResult KatanaPose(float swipe, float chop01, float facing, float fullSwipeCardDegrees,
                                                  float straightAt01, float fullChopLength01)
        {
            swipe = Mathf.Clamp(swipe, -1f, 1f);
            chop01 = Mathf.Clamp01(chop01);
            float f = swipe > 0f ? -1f : swipe < 0f ? 1f : (facing < 0f ? -1f : 1f);
            return new KatanaPoseResult
            {
                AngleZ = -swipe * fullSwipeCardDegrees,
                Facing = f,
                Length01 = Mathf.Lerp(1f, fullChopLength01, chop01),
                Straight = chop01 >= straightAt01,
            };
        }

        public override void EnsureGeneratedAbilityIcons()
        {
            base.EnsureGeneratedAbilityIcons();
            BuildKatana();
            BuildReach();
        }

        // Find-or-create the Space card's layout under its icon (a transparent container since 2026-10-28): the prism,
        // the hilt pivot (hilt part + a scale node holding the blade, both pivoted on the part hilt), the posts on top.
        void BuildReach()
        {
            if (!reachBladeSprite || !TryGetAbilityIcon(Element.Space, out var host) || !host) return;
            if (_reachHost) return;

            var hostImage = ResolveGeneratedChild<Image>(host.rectTransform, "BladeReach");   // a holder: it draws nothing
            hostImage.enabled = false;
            hostImage.raycastTarget = false;
            _reachHost = hostImage.rectTransform;
            StretchReach(_reachHost, new Vector2(0.5f, 0.5f));

            float px = katanaAuthoredSize > 0f ? katanaAuthoredSize : 128f;
            var partPivot = new Vector2(reachPartHilt.x / px, 1f - reachPartHilt.y / px);

            _reachPrism = ResolveGeneratedChild<Image>(_reachHost, "Prism");
            _reachPrism.raycastTarget = false;
            _reachPrism.sprite = reachPrismSprite;
            _reachPrismRt = _reachPrism.rectTransform;
            StretchReach(_reachPrismRt, new Vector2(0.5f, 0.5f));

            var pivotImage = ResolveGeneratedChild<Image>(_reachHost, "HiltPivot");
            pivotImage.enabled = false;
            pivotImage.raycastTarget = false;
            _reachPivot = pivotImage.rectTransform;
            StretchReach(_reachPivot, partPivot);

            _reachHilt = ResolveGeneratedChild<Image>(_reachPivot, "Hilt");
            _reachHilt.raycastTarget = false;
            _reachHilt.sprite = reachHiltSprite;
            StretchReach(_reachHilt.rectTransform, new Vector2(0.5f, 0.5f));

            var scaleImage = ResolveGeneratedChild<Image>(_reachPivot, "BladeScale");
            scaleImage.enabled = false;
            scaleImage.raycastTarget = false;
            _reachBladeScale = scaleImage.rectTransform;
            StretchReach(_reachBladeScale, partPivot);

            _reachBlade = ResolveGeneratedChild<Image>(_reachBladeScale, "Blade");
            _reachBlade.raycastTarget = false;
            _reachBlade.sprite = reachBladeSprite;
            StretchReach(_reachBlade.rectTransform, new Vector2(0.5f, 0.5f));

            _reachPosts = ResolveGeneratedChild<Image>(_reachHost, "Posts");
            _reachPosts.raycastTarget = false;
            _reachPosts.sprite = reachPostsSprite;
            StretchReach(_reachPosts.rectTransform, new Vector2(0.5f, 0.5f));

            _reach01 = 0f;
            SetBladeReach(0f, false, false, new Color(0f, 0f, 0f, 0f));
            LayOutReach();
        }

        static void StretchReach(RectTransform rt, Vector2 pivot)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
        }

        // Find-or-create the katana under the Charge card's icon (a transparent container since 2026-10-28): a pivot
        // stretched over the card with its pivot at the hilt, and the blade stretched inside it so the sprite's own
        // margins land where the kit drew them.
        void BuildKatana()
        {
            if (!katanaCurvedSprite || !TryGetAbilityIcon(Element.Charge, out var host) || !host) return;
            if (_katanaPivot && _katana) return;

            var pivotImage = ResolveGeneratedChild<Image>(host.rectTransform, KatanaPivotName);   // a holder: it draws nothing
            pivotImage.enabled = false;
            pivotImage.raycastTarget = false;
            _katanaPivot = pivotImage.rectTransform;
            _katanaPivot.anchorMin = Vector2.zero;
            _katanaPivot.anchorMax = Vector2.one;
            _katanaPivot.sizeDelta = Vector2.zero;
            float size = katanaAuthoredSize > 0f ? katanaAuthoredSize : 128f;
            _katanaPivot.pivot = new Vector2(katanaHilt.x / size, 1f - katanaHilt.y / size);
            _katanaPivot.anchoredPosition = Vector2.zero;

            _katana = ResolveGeneratedChild<Image>(_katanaPivot, KatanaName);
            _katana.raycastTarget = false;
            _katana.sprite = katanaCurvedSprite;
            _katana.preserveAspect = true;
            var rt = _katana.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            _katanaFacing = 1f;
            SetKatanaLit(_katanaLit);
            SetKatanaPose(0f, 0f);
        }
    }
}
