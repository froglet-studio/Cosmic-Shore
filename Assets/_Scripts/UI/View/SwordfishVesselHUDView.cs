using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Swordfish's HUD view (its own since 2026-11-12; it flew the Rhino's view and katana cards until then).
    /// Every card is drawn from the pilot's own view, behind the hull, with the sword as what it is since 2026-11-08:
    /// the fish's BILL. Sprites and every authored number come from Tools/Build/icon_kit/specs/swordfish.py.
    ///
    /// <para><b>CHARGE - Energize.</b> The icon is a transparent container over the snout (a prefab part, white) and
    /// a bill this view generates on a pivot at the hilt. The controller pushes the real sword's pose every frame
    /// (<see cref="SetBillPose"/>): a side swing turns the bill toward that side, and the bill is foreshortened while
    /// it points ahead and full length out to a side or raised (both triggers, the energize stance). White dark,
    /// danger lit (<see cref="SetBillLit"/>).</para>
    ///
    /// <para><b>MASS - Afterimage.</b> Three prefab parts: the bill (white) and two prism copies of it in the pilot's
    /// domain colour, shown while the afterimages are laid (<see cref="SetAfterimage"/>).</para>
    ///
    /// <para><b>SPACE - Blade Reach.</b> A layout this view generates from five parts: the bill laid across the card,
    /// scaled along itself to the live length (24 -> 40), posts at the rest and full reach, and a super-shielded prism
    /// at the tip, shattered once Shield Breaker is active (<see cref="SetBladeReach"/>).</para>
    ///
    /// <para><b>TIME - Quickdraw.</b> Four prefab parts: the snout and the bill snapped out to the side (white), the
    /// swoosh of the swing (white, brighter as Time quickens the sword), and the swoosh back the other way while
    /// Flurry (Time 5) holds (<see cref="SetQuickdraw"/>). The plate is the swing's recovery draining away.</para>
    /// </summary>
    public class SwordfishVesselHUDView : VesselHUDView
    {
        const string BillPivotName = "BillPivot";
        const string BillName = "Bill";

        [Header("Charge card - the bill")]
        [Tooltip("The bill at full length, upright on billHilt (Swordfish_Bill).")]
        [SerializeField] private Sprite billSprite;
        [Tooltip("The snout the bill stands off (Swordfish_BillSocket), white, never turns.")]
        [SerializeField] private Image billSocketIcon;
        [Tooltip("The size the sprites were drawn at (px). The hilts below are in these pixels.")]
        [SerializeField] private float authoredSize = 128f;
        [Tooltip("Where the bill leaves the snout, authored px from the sprite's top-left (CH_HILT).")]
        [SerializeField] private Vector2 billHilt = new(64f, 104f);
        [Tooltip("How far the card's bill turns for a full side swing (the real one is 90 deg; BILL_SIDE_CARD_DEG).")]
        [SerializeField, Range(0f, 90f)] private float billSideCardDegrees = 55f;
        [Tooltip("The bill's length on the card while it points straight ahead, as a fraction of its full length " +
                 "(foreshortened: the camera is behind it; BILL_REST01).")]
        [SerializeField, Range(0.2f, 1f)] private float billRestLength01 = 0.65f;
        [Tooltip("The bill while the blade is dark.")]
        [SerializeField] private Color billDarkColor = Color.white;
        [Tooltip("The bill while the blade is lit (energized): the danger tint (ElementalBarsConfigSO.FireColor).")]
        [SerializeField] private Color billLitColor = new(1f, 0.3294f, 0.1020f, 1f);

        [Header("Mass card - the afterimages")]
        [SerializeField] private Image afterimageBladeIcon;
        [SerializeField] private Image afterimageNearIcon;
        [SerializeField] private Image afterimageFarIcon;
        [SerializeField, Range(0f, 1f)] private float afterimageNearAlpha = 0.8f;
        [SerializeField, Range(0f, 1f)] private float afterimageFarAlpha = 0.45f;

        [Header("Space card - Blade Reach")]
        [Tooltip("The snout socket (Swordfish_ReachHilt), upright on reachPartHilt.")]
        [SerializeField] private Sprite reachHiltSprite;
        [Tooltip("The bill at full reach (Swordfish_ReachBlade), upright on reachPartHilt; scaled to the live length.")]
        [SerializeField] private Sprite reachBladeSprite;
        [SerializeField] private Sprite reachPrismSprite;
        [SerializeField] private Sprite reachPrismBrokenSprite;
        [SerializeField] private Sprite reachPostsSprite;
        [SerializeField] private Vector2 reachPartHilt = new(64f, 100f);
        [SerializeField] private Vector2 reachCardHilt = new(20f, 112f);
        [SerializeField] private float reachAngle = 52f;
        [SerializeField] private Vector2 reachLengthRestMax = new(40.8f, 68f);
        [Tooltip("The prism's centre past the bill's tip, authored px (PRISM_ALONG).")]
        [SerializeField] private float reachPrismAlong = 13.12f;
        [Tooltip("The prism's centre to the bill's side per px of bill: 0, the bill is straight.")]
        [SerializeField] private float reachPrismSidePerLength;
        [SerializeField, Range(0f, 1f)] private float reachPostsAlpha = 0.6f;

        [Header("Time card - Quickdraw")]
        [Tooltip("The snout (Swordfish_QuickdrawSocket), white.")]
        [SerializeField] private Image quickdrawSocketIcon;
        [Tooltip("The bill snapped out to the right (Swordfish_QuickdrawBill), white.")]
        [SerializeField] private Image quickdrawBillIcon;
        [Tooltip("The swoosh of the swing just made (Swordfish_QuickdrawArc), white; its opacity follows Time's rate.")]
        [SerializeField] private Image quickdrawArcIcon;
        [Tooltip("Flurry: the swoosh back the other way (Swordfish_QuickdrawFlurry), shown while Time 5 holds.")]
        [SerializeField] private Image quickdrawFlurryIcon;
        [Tooltip("The swoosh's opacity at rest (rate x1) and at Time 10 (x2.5).")]
        [SerializeField] private Vector2 quickdrawArcAlphaRestFull = new(0.55f, 1f);

        RectTransform _reachHost, _reachPivot, _reachBladeScale, _reachPrismRt;
        Image _reachHilt, _reachBlade, _reachPrism, _reachPosts;
        float _reach01 = -1f;
        Vector2 _reachLaidSize;

        RectTransform _billPivot;
        Image _bill;
        bool _billLit;

        public override void Initialize() { }

        void OnEnable() => ColorTrace.Changed += RepaintBill;

        void OnDisable() => ColorTrace.Changed -= RepaintBill;

        void RepaintBill() => SetBillLit(_billLit);

        // ── Charge ──────────────────────────────────────────────────────────────

        /// <summary>What <see cref="SetBillPose"/> draws, as a pure function so it is tested without a canvas.</summary>
        public struct BillPoseResult
        {
            /// <summary>The pivot's z rotation (deg; negative is clockwise on screen, a swing to the right).</summary>
            public float AngleZ;
            /// <summary>The bill's length as a fraction of its full length.</summary>
            public float Length01;
        }

        /// <summary>
        /// The bill on the card for a side swing <paramref name="side"/> (-1..1, + right) and a raise
        /// <paramref name="raise01"/> (0..1): turned toward the side, and lengthened from its foreshortened
        /// rest length as it leaves the line of sight - out to a side or up.
        /// </summary>
        public static BillPoseResult BillPose(float side, float raise01, float sideCardDegrees, float restLength01)
        {
            side = Mathf.Clamp(side, -1f, 1f);
            raise01 = Mathf.Clamp01(raise01);
            return new BillPoseResult
            {
                AngleZ = -side * sideCardDegrees,
                Length01 = Mathf.Lerp(restLength01, 1f, Mathf.Max(Mathf.Abs(side), raise01)),
            };
        }

        public void SetBillPose(float side, float raise01)
        {
            if (!_billPivot) return;
            var pose = BillPose(side, raise01, billSideCardDegrees, billRestLength01);
            _billPivot.localRotation = Quaternion.Euler(0f, 0f, pose.AngleZ);
            _billPivot.localScale = new Vector3(1f, pose.Length01, 1f);
        }

        /// <summary>White while the blade is dark, danger while it is lit.</summary>
        public void SetBillLit(bool lit)
        {
            _billLit = lit;
            if (_bill) _bill.color = ColorTrace.Retrace(ColorTrace.Ui, lit ? billLitColor : billDarkColor);
        }

        // ── Mass ────────────────────────────────────────────────────────────────

        /// <summary>The prism copies show, in the pilot's <paramref name="domain"/> colour, only while laid.</summary>
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

        // ── Space ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The bill at <paramref name="reach01"/> of its range, the prism shattered when <paramref name="breaker"/>,
        /// the bill danger while <paramref name="lit"/>, the prism in <paramref name="prismColor"/> (alpha 0 keeps
        /// the authored colour).
        /// </summary>
        public void SetBladeReach(float reach01, bool breaker, bool lit, Color prismColor)
        {
            if (!_reachHost) return;
            var color = ColorTrace.Retrace(ColorTrace.Ui, lit ? billLitColor : billDarkColor);
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

        /// <summary>Where the Space card's prism centre sits, authored px from the card's top-left (specs prism_centre).</summary>
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
            float px = authoredSize > 0f ? authoredSize : 128f;
            var k = new Vector2(size.x / px, size.y / px);
            _reachPivot.anchoredPosition = new Vector2((reachCardHilt.x - reachPartHilt.x) * k.x,
                                                       -(reachCardHilt.y - reachPartHilt.y) * k.y);
            _reachPivot.localRotation = Quaternion.Euler(0f, 0f, -reachAngle);
            float length = Mathf.Lerp(reachLengthRestMax.x, reachLengthRestMax.y, _reach01);
            _reachBladeScale.localScale = new Vector3(1f, reachLengthRestMax.y > 0f ? length / reachLengthRestMax.y : 1f, 1f);
            var c = ReachPrismCentre(reachCardHilt, reachAngle, length, reachPrismAlong, reachPrismSidePerLength);
            _reachPrismRt.anchoredPosition = new Vector2((c.x - px * 0.5f) * k.x, -(c.y - px * 0.5f) * k.y);
        }

        // ── Time ────────────────────────────────────────────────────────────────

        /// <summary>
        /// The Time card: the snout and bill white, the swoosh brighter with Time's rate (<paramref name="rate01"/>: 0 at
        /// x1, 1 at x2.5), and the swoosh back the other way while <paramref name="flurry"/> (Time 5) holds.
        /// </summary>
        public void SetQuickdraw(bool flurry, float rate01)
        {
            if (quickdrawSocketIcon) quickdrawSocketIcon.color = Color.white;
            if (quickdrawBillIcon) quickdrawBillIcon.color = Color.white;
            float a = Mathf.Lerp(quickdrawArcAlphaRestFull.x, quickdrawArcAlphaRestFull.y, Mathf.Clamp01(rate01));
            if (quickdrawArcIcon) quickdrawArcIcon.color = new Color(1f, 1f, 1f, a);
            if (quickdrawFlurryIcon)
            {
                quickdrawFlurryIcon.enabled = flurry;
                quickdrawFlurryIcon.color = Color.white;
            }
        }

        // ── generated parts ─────────────────────────────────────────────────────

        public override void EnsureGeneratedAbilityIcons()
        {
            base.EnsureGeneratedAbilityIcons();
            BuildBill();
            BuildReach();
        }

        // Find-or-create the bill under the Charge card's icon: a pivot stretched over the card with its pivot at the
        // hilt, the bill stretched inside it so the sprite's own margins land where the kit drew them. The snout part
        // (a prefab child) is moved after it, so the bill's root runs in under the snout.
        void BuildBill()
        {
            if (!billSprite || !TryGetAbilityIcon(Element.Charge, out var host) || !host) return;
            if (_billPivot && _bill) return;

            var pivotImage = ResolveGeneratedChild<Image>(host.rectTransform, BillPivotName);
            pivotImage.enabled = false;
            pivotImage.raycastTarget = false;
            _billPivot = pivotImage.rectTransform;
            _billPivot.anchorMin = Vector2.zero;
            _billPivot.anchorMax = Vector2.one;
            _billPivot.sizeDelta = Vector2.zero;
            float size = authoredSize > 0f ? authoredSize : 128f;
            _billPivot.pivot = new Vector2(billHilt.x / size, 1f - billHilt.y / size);
            _billPivot.anchoredPosition = Vector2.zero;

            _bill = ResolveGeneratedChild<Image>(_billPivot, BillName);
            _bill.raycastTarget = false;
            _bill.sprite = billSprite;
            _bill.preserveAspect = true;
            Stretch(_bill.rectTransform, new Vector2(0.5f, 0.5f));

            if (billSocketIcon) billSocketIcon.transform.SetAsLastSibling();
            SetBillLit(_billLit);
            SetBillPose(0f, 0f);
        }

        void BuildReach()
        {
            if (!reachBladeSprite || !TryGetAbilityIcon(Element.Space, out var host) || !host) return;
            if (_reachHost) return;

            var hostImage = ResolveGeneratedChild<Image>(host.rectTransform, "BladeReach");
            hostImage.enabled = false;
            hostImage.raycastTarget = false;
            _reachHost = hostImage.rectTransform;
            Stretch(_reachHost, new Vector2(0.5f, 0.5f));

            float px = authoredSize > 0f ? authoredSize : 128f;
            var partPivot = new Vector2(reachPartHilt.x / px, 1f - reachPartHilt.y / px);

            _reachPrism = ResolveGeneratedChild<Image>(_reachHost, "Prism");
            _reachPrism.raycastTarget = false;
            _reachPrism.sprite = reachPrismSprite;
            _reachPrismRt = _reachPrism.rectTransform;
            Stretch(_reachPrismRt, new Vector2(0.5f, 0.5f));

            var pivotImage = ResolveGeneratedChild<Image>(_reachHost, "HiltPivot");
            pivotImage.enabled = false;
            pivotImage.raycastTarget = false;
            _reachPivot = pivotImage.rectTransform;
            Stretch(_reachPivot, partPivot);

            var scaleImage = ResolveGeneratedChild<Image>(_reachPivot, "BladeScale");
            scaleImage.enabled = false;
            scaleImage.raycastTarget = false;
            _reachBladeScale = scaleImage.rectTransform;
            Stretch(_reachBladeScale, partPivot);

            _reachBlade = ResolveGeneratedChild<Image>(_reachBladeScale, "Blade");
            _reachBlade.raycastTarget = false;
            _reachBlade.sprite = reachBladeSprite;
            Stretch(_reachBlade.rectTransform, new Vector2(0.5f, 0.5f));

            // the snout over the bill's root
            _reachHilt = ResolveGeneratedChild<Image>(_reachPivot, "Hilt");
            _reachHilt.raycastTarget = false;
            _reachHilt.sprite = reachHiltSprite;
            Stretch(_reachHilt.rectTransform, new Vector2(0.5f, 0.5f));

            _reachPosts = ResolveGeneratedChild<Image>(_reachHost, "Posts");
            _reachPosts.raycastTarget = false;
            _reachPosts.sprite = reachPostsSprite;
            Stretch(_reachPosts.rectTransform, new Vector2(0.5f, 0.5f));

            _reach01 = 0f;
            SetBladeReach(0f, false, false, new Color(0f, 0f, 0f, 0f));
            LayOutReach();
        }

        static void Stretch(RectTransform rt, Vector2 pivot)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
