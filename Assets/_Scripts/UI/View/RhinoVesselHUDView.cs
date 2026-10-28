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
