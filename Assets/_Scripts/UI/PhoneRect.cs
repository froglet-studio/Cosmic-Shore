using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A panel's place on a phone (Docs/UI_STYLE_GUIDE.md §8, D6; the approved phone boards, Docs/MENU_REDESIGN_B.md
    /// §127-128). The RectTransform keeps its authored 1920 x 1080 pose; this component holds the pose the panel takes
    /// on the 800 x 450 phone canvas. <see cref="AdaptiveCanvasScaler"/> applies it to every PhoneRect under its canvas
    /// when the phone layout turns on, and puts the authored pose back when it turns off; a PhoneRect enabled later
    /// (a pooled card, a panel opened after the switch) asks its canvas itself.
    ///
    /// Use the pose on rects a LayoutGroup does not drive (a layout group overwrites its children's poses every
    /// rebuild); for a child of a layout group, leave Moves Rect off and give it a phone Layout Size instead. A TMP text
    /// with no KitText (whose role already carries a phone size) takes its phone Font Size here.
    /// The generators (Tools/Build/author_*.py) write these values, so a phone layout is authored like the PC one.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Cosmic Shore/UI/Phone Rect")]
    public class PhoneRect : MonoBehaviour
    {
        [Header("A screen with no phone layout yet")]
        [Tooltip("Draw this block on a phone exactly as the PC layout shrunk onto the phone draws it: it keeps its authored " +
                 "anchors, is laid out as if its parent were 1080 units tall, and is scaled down to fit; the KitTexts inside " +
                 "keep their PC sizes. For a screen whose phone board is not drawn yet, so its canvas can still switch to " +
                 "the phone layout. The pose fields below are ignored.")]
        [SerializeField] private bool drawAtPcScale;
        [Tooltip("The PC reference height the block is drawn at.")]
        [SerializeField] private float pcHeight = 1080f;

        [Header("Pose on a phone (800 x 450 canvas units)")]
        [Tooltip("Move the rect to the pose below on a phone. Off for a child a LayoutGroup places.")]
        [SerializeField] private bool movesRect = true;
        [Tooltip("Anchor min on a phone.")]
        [SerializeField] private Vector2 anchorMin = new(0.5f, 0.5f);
        [Tooltip("Anchor max on a phone.")]
        [SerializeField] private Vector2 anchorMax = new(0.5f, 0.5f);
        [Tooltip("Pivot on a phone.")]
        [SerializeField] private Vector2 pivot = new(0.5f, 0.5f);
        [Tooltip("Anchored position on a phone, in phone canvas units.")]
        [SerializeField] private Vector2 anchoredPosition;
        [Tooltip("Size delta on a phone, in phone canvas units.")]
        [SerializeField] private Vector2 sizeDelta = new(100f, 100f);

        [Header("Text and layout on a phone")]
        [Tooltip("The TMP text's size on a phone, in phone canvas units (its auto-size maximum too). 0 leaves it. Leave 0 " +
                 "on a text with a KitText: its role's phone size already applies.")]
        [SerializeField, Min(0f)] private float fontSize;
        [Tooltip("The LayoutElement's preferred width and height on a phone. 0 leaves that axis.")]
        [SerializeField] private Vector2 layoutSize;
        [Tooltip("A uniform scale on a phone for a block drawn at the PC size (an in-game HUD piece whose parts are " +
                 "authored in 1920 units). 0 leaves the scale.")]
        [SerializeField, Min(0f)] private float scale;
        [Tooltip("Draw this block at its PC look scaled by this much on a phone, re-flowed to the phone's width: its " +
                 "horizontally stretched width grows by 1/scale so its layout fills the parent once scaled, and its height " +
                 "and position are left to whoever owns them (a scroll view's content, its size fitter). Its KitTexts keep " +
                 "their PC sizes. 0 leaves it. Used for a scroll view whose cards are built at the PC size.")]
        [SerializeField, Min(0f)] private float widthScale;

        [Header("Children on a phone")]
        [Tooltip("Hide this object on a phone (its CanvasGroup goes transparent and stops taking input); shown again on " +
                 "PC. Needs a CanvasGroup on the same object.")]
        [SerializeField] private bool hideOnPhone;
        [Tooltip("Stack this object's active children top-down on a phone (the nav band becomes a rail): its own " +
                 "LayoutGroup switches off and each child takes Stack Item Size, Stack Spacing apart from Stack Origin. " +
                 "A child with its own PhoneRect is left to it. The order is Stack Order by name, then the rest in " +
                 "sibling order.")]
        [SerializeField] private bool stackChildren;
        [SerializeField] private Vector2 stackOrigin;
        [SerializeField] private Vector2 stackItemSize = new(72f, 56f);
        [SerializeField] private float stackSpacing;
        [SerializeField] private string[] stackOrder = System.Array.Empty<string>();
        [Tooltip("The GridLayoutGroup's cell size on a phone (0 leaves it).")]
        [SerializeField] private Vector2 gridCellSize;
        [Tooltip("The GridLayoutGroup's column count on a phone, as a Fixed Column Count constraint (0 leaves it).")]
        [SerializeField, Min(0)] private int gridColumns;
        [Tooltip("The GridLayoutGroup's spacing on a phone (negative leaves it).")]
        [SerializeField] private Vector2 gridSpacing = new(-1f, -1f);

        private RectTransform _rect;
        private bool _authoredRead;
        private Vector2 _aMin, _aMax, _pivot, _pos, _size;
        private float _font, _fontMax, _leW, _leH, _leMinW, _leMinH;
        private Vector3 _scale;
        private TMP_Text _text;
        private LayoutElement _le;
        private CanvasGroup _group;
        private float _groupAlpha;
        private bool _groupInteractable, _groupBlocks;
        private LayoutGroup _layout;
        private bool _layoutEnabled;
        private GridLayoutGroup _grid;
        private Vector2 _gridCell, _gridSpacing;
        private GridLayoutGroup.Constraint _gridConstraint;
        private int _gridCount;
        private readonly System.Collections.Generic.List<(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)> _stacked = new();
        private AdaptiveCanvasScaler _scaler;
        private bool _scalerResolved;

        /// <summary>Whether the phone pose is on.</summary>
        public bool OnPhone { get; private set; }

        /// <summary>Whether this block draws at the PC scale on a phone (a screen with no phone layout yet).</summary>
        public bool DrawsAtPcScale => drawAtPcScale;

        /// <summary>Whether <paramref name="t"/> sits inside a block drawn at the PC scale on a phone (a fallback screen, or
        /// a block given a phone scale): its KitTexts keep their PC sizes there.</summary>
        public static bool InsidePcScaleBlock(Transform t)
        {
            for (var at = t; at; at = at.parent)
                if (at.TryGetComponent(out PhoneRect p) && (p.drawAtPcScale || p.scale > 0f || p.widthScale > 0f)) return true;
            return false;
        }

        private RectTransform Rect => _rect ? _rect : (_rect = (RectTransform)transform);

        private void OnEnable()
        {
            if (!_scalerResolved)
            {
                _scaler = GetComponentInParent<AdaptiveCanvasScaler>(true);
                _scalerResolved = true;
            }
            if (_scaler && _scaler.UsesPhoneLayout != OnPhone) Apply(_scaler.UsesPhoneLayout);
        }

        private void OnTransformParentChanged() => _scalerResolved = false;

        // A PC-scale block fills its parent, so its own rect changes when the parent's does (a resolution change, the
        // canvas switching reference): re-fit then, when the parent's rect is current.
        private void OnRectTransformDimensionsChange()
        {
            if (OnPhone && drawAtPcScale) FitPcBlock();
            if (OnPhone && widthScale > 0f) FitWidthAtScale();
        }

        /// <summary>Scale the block by Width Scale and widen its stretched width by 1/scale, so its layout fills the parent
        /// once scaled. Height and position are left alone.</summary>
        private void FitWidthAtScale()
        {
            var r = Rect;
            if (r.parent is not RectTransform parent) return;
            float s = widthScale;
            float span = _aMax.x - _aMin.x;
            float w = _size.x + span * parent.rect.width * (1f / s - 1f);
            if (Mathf.Abs(r.sizeDelta.x - w) > 0.01f) r.sizeDelta = new Vector2(w, r.sizeDelta.y);
            var scaleV = new Vector3(s * _scale.x, s * _scale.y, _scale.z);
            if (r.localScale != scaleV) r.localScale = scaleV;
        }

        /// <summary>
        /// Lay the block out as the PC layout shrunk onto the phone would: in a parent of size P/s (s = P.y / pcHeight)
        /// with its authored pose, then scaled by s about its pivot. Its pivot then lands at the authored position times s,
        /// and its unscaled size is what the virtual parent gives it: the authored size plus the stretched span of the
        /// difference between the virtual parent and the real one.
        /// </summary>
        public void FitPcBlock()
        {
            if (!OnPhone || !drawAtPcScale) return;
            var r = Rect;
            if (r.parent is not RectTransform parent) return;
            var size = parent.rect.size;
            if (size.y <= 0f || pcHeight <= 0f) return;
            float s = size.y / pcHeight;
            var span = _aMax - _aMin;
            var delta = _size + Vector2.Scale(span, size) * (1f / s - 1f);
            var pos = _pos * s;
            if ((r.sizeDelta - delta).sqrMagnitude > 0.0001f) r.sizeDelta = delta;
            if ((r.anchoredPosition - pos).sqrMagnitude > 0.0001f) r.anchoredPosition = pos;
            var scaleV = new Vector3(s * _scale.x, s * _scale.y, _scale.z);
            if (r.localScale != scaleV) r.localScale = scaleV;
        }

        /// <summary>Put the panel in its phone pose, or back in its authored one.</summary>
        public void Apply(bool phone)
        {
            if (phone == OnPhone) return;
            var r = Rect;
            if (phone)
            {
                if (!_authoredRead) ReadAuthored(r);
                if (drawAtPcScale)
                {
                    OnPhone = true;
                    FitPcBlock();
                    return;
                }
                if (movesRect) Set(r, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
                if (scale > 0f) r.localScale = new Vector3(scale, scale, 1f);
                if (widthScale > 0f) { OnPhone = true; FitWidthAtScale(); }
                if (_text && fontSize > 0f)
                {
                    _text.fontSize = fontSize;
                    if (_text.enableAutoSizing) _text.fontSizeMax = fontSize;
                }
                if (_le)
                {
                    // The phone size is the preferred size, and a minimum larger than it comes down to it (the Home hubs'
                    // 210-unit minimum would not fit four across a phone).
                    if (layoutSize.x > 0f) { _le.preferredWidth = layoutSize.x; if (_leMinW > layoutSize.x) _le.minWidth = layoutSize.x; }
                    if (layoutSize.y > 0f) { _le.preferredHeight = layoutSize.y; if (_leMinH > layoutSize.y) _le.minHeight = layoutSize.y; }
                }
                ApplyChildren(true);
            }
            else if (_authoredRead)
            {
                if (movesRect || drawAtPcScale) Set(r, _aMin, _aMax, _pivot, _pos, _size);
                if (scale > 0f || drawAtPcScale || widthScale > 0f) r.localScale = _scale;
                if (widthScale > 0f && !movesRect) r.sizeDelta = new Vector2(_size.x, r.sizeDelta.y);
                if (_text && fontSize > 0f)
                {
                    _text.fontSize = _font;
                    if (_text.enableAutoSizing) _text.fontSizeMax = _fontMax;
                }
                if (_le)
                {
                    if (layoutSize.x > 0f) { _le.preferredWidth = _leW; _le.minWidth = _leMinW; }
                    if (layoutSize.y > 0f) { _le.preferredHeight = _leH; _le.minHeight = _leMinH; }
                }
                ApplyChildren(false);
            }
            OnPhone = phone;
        }

        private void ReadAuthored(RectTransform r)
        {
            _aMin = r.anchorMin; _aMax = r.anchorMax; _pivot = r.pivot;
            _pos = r.anchoredPosition; _size = r.sizeDelta; _scale = r.localScale;
            if (TryGetComponent(out _text)) { _font = _text.fontSize; _fontMax = _text.fontSizeMax; }
            if (TryGetComponent(out _le)) { _leW = _le.preferredWidth; _leH = _le.preferredHeight; _leMinW = _le.minWidth; _leMinH = _le.minHeight; }
            if (TryGetComponent(out _group)) { _groupAlpha = _group.alpha; _groupInteractable = _group.interactable; _groupBlocks = _group.blocksRaycasts; }
            if (TryGetComponent(out _layout)) _layoutEnabled = _layout.enabled;
            if (TryGetComponent(out _grid))
            {
                _gridCell = _grid.cellSize; _gridSpacing = _grid.spacing;
                _gridConstraint = _grid.constraint; _gridCount = _grid.constraintCount;
            }
            _authoredRead = true;
        }

        private void ApplyChildren(bool phone)
        {
            if (hideOnPhone && _group)
            {
                _group.alpha = phone ? 0f : _groupAlpha;
                _group.interactable = !phone && _groupInteractable;
                _group.blocksRaycasts = !phone && _groupBlocks;
            }
            if (_grid)
            {
                if (gridCellSize.x > 0f && gridCellSize.y > 0f) _grid.cellSize = phone ? gridCellSize : _gridCell;
                if (gridColumns > 0)
                {
                    _grid.constraint = phone ? GridLayoutGroup.Constraint.FixedColumnCount : _gridConstraint;
                    _grid.constraintCount = phone ? gridColumns : _gridCount;
                }
                if (gridSpacing.x >= 0f && gridSpacing.y >= 0f) _grid.spacing = phone ? gridSpacing : _gridSpacing;
            }
            if (!stackChildren) return;
            if (phone)
            {
                if (_layout) _layout.enabled = false;
                _stacked.Clear();
                int i = 0;
                foreach (var child in StackSequence())
                {
                    _stacked.Add((child, child.anchorMin, child.anchorMax, child.pivot, child.anchoredPosition, child.sizeDelta));
                    var top = new Vector2(0f, 1f);
                    Set(child, top, top, top, stackOrigin + new Vector2(0f, -i * (stackItemSize.y + stackSpacing)), stackItemSize);
                    i++;
                }
            }
            else
            {
                foreach (var s in _stacked)
                    if (s.rt) Set(s.rt, s.aMin, s.aMax, s.pivot, s.pos, s.size);
                _stacked.Clear();
                if (_layout) _layout.enabled = _layoutEnabled;
            }
        }

        /// <summary>The children to stack: Stack Order by name first, then the rest in sibling order; never one that is
        /// inactive or carries its own PhoneRect.</summary>
        private System.Collections.Generic.IEnumerable<RectTransform> StackSequence()
        {
            var t = transform;
            var used = new System.Collections.Generic.HashSet<Transform>();
            foreach (var name in stackOrder)
            {
                var c = t.Find(name);
                if (c && Stackable(c)) { used.Add(c); yield return (RectTransform)c; }
            }
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (!used.Contains(c) && Stackable(c)) yield return (RectTransform)c;
            }
        }

        private static bool Stackable(Transform c) =>
            c.gameObject.activeSelf && c is RectTransform && !c.TryGetComponent(out PhoneRect _);

        private static void Set(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        /// <summary>Set the phone pose from code (tests and editor tooling; the generators write the fields).</summary>
        public void SetPhonePose(Vector2 aMin, Vector2 aMax, Vector2 pv, Vector2 pos, Vector2 size)
        {
            movesRect = true;
            anchorMin = aMin; anchorMax = aMax; pivot = pv; anchoredPosition = pos; sizeDelta = size;
            if (OnPhone) Set(Rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        }

        /// <summary>Make this a PC-scale block from code (tests and editor tooling).</summary>
        public void SetDrawAtPcScale(bool on, float referenceHeight = 1080f)
        {
            drawAtPcScale = on; pcHeight = referenceHeight;
        }

        /// <summary>Set the phone text and layout sizes from code (tests and editor tooling).</summary>
        /// <summary>Set the phone children behaviour from code (tests and editor tooling).</summary>
        public void SetPhoneChildren(bool hide, bool stack, Vector2 origin, Vector2 itemSize, float spacing, string[] order,
                                     Vector2 cell, int columns)
        {
            hideOnPhone = hide; stackChildren = stack; stackOrigin = origin; stackItemSize = itemSize; stackSpacing = spacing;
            stackOrder = order ?? System.Array.Empty<string>(); gridCellSize = cell; gridColumns = columns;
        }

        public void SetPhoneSizes(float phoneFontSize, Vector2 phoneLayoutSize, bool moves, float phoneScale = 0f, float phoneWidthScale = 0f)
        {
            fontSize = phoneFontSize; layoutSize = phoneLayoutSize; movesRect = moves; scale = phoneScale; widthScale = phoneWidthScale;
        }
    }
}
