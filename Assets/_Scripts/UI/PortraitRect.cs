using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A piece's place when a phone is held upright (Docs/UI_STYLE_GUIDE.md section 8, D6; Docs/MENU_REDESIGN_B.md section
    /// 138, the picked Rev 63 B: the same PC UI re-flowed onto a tall canvas). Every device shows the PC UI; when the
    /// screen is taller than it is wide, a canvas whose <see cref="AdaptiveCanvasScaler"/> has a portrait layout switches
    /// to a 1080 x 1920 reference matched to the width, and applies every PortraitRect under it. The RectTransform keeps
    /// its authored 1920 x 1080 pose; this component holds the portrait one, and puts the authored pose back on a turn to
    /// landscape. The pieces keep their PC sizes (no portrait type scale); only where they sit changes.
    ///
    /// A screen with no portrait pose yet is a block drawn at the PC scale fitted to the width (Draw At PC Scale): its
    /// 1920-wide layout is laid out in a parent 1920 wide and as tall as the screen allows at that scale, then scaled
    /// down, so it shows whole instead of spilling off the sides.
    /// The generators (Tools/Build/author_*.py) write these values, so a portrait pose is authored like the PC one.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Cosmic Shore/UI/Portrait Rect")]
    public class PortraitRect : MonoBehaviour
    {
        [Header("A screen with no portrait pose yet")]
        [Tooltip("Draw this block upright as its PC layout fitted to the screen's width: laid out in a parent PC Width wide and " +
                 "as tall as the screen allows at that scale, then scaled down. The pose fields below are ignored.")]
        [SerializeField] private bool drawAtPcScale;
        [Tooltip("The PC reference width the block is laid out at.")]
        [SerializeField, Min(1f)] private float pcWidth = 1920f;

        [Header("Pose when upright (1080-wide canvas units)")]
        [Tooltip("Move the rect to the pose below when upright. Off for a child a LayoutGroup places.")]
        [SerializeField] private bool movesRect = true;
        [SerializeField] private Vector2 anchorMin = new(0.5f, 0.5f);
        [SerializeField] private Vector2 anchorMax = new(0.5f, 0.5f);
        [SerializeField] private Vector2 pivot = new(0.5f, 0.5f);
        [SerializeField] private Vector2 anchoredPosition;
        [SerializeField] private Vector2 sizeDelta = new(100f, 100f);
        [Tooltip("A uniform scale when upright (0 leaves it).")]
        [SerializeField, Min(0f)] private float scale;
        [Tooltip("The LayoutElement's preferred width and height when upright (0 leaves that axis).")]
        [SerializeField] private Vector2 layoutSize;

        [Header("Children when upright")]
        [Tooltip("Hide this object when upright (needs a CanvasGroup on it that nothing else drives).")]
        [SerializeField] private bool hideInPortrait;
        [Tooltip("The GridLayoutGroup's cell size when upright (0 leaves it).")]
        [SerializeField] private Vector2 gridCellSize;
        [Tooltip("The GridLayoutGroup's column count when upright, as a Fixed Column Count (0 leaves it).")]
        [SerializeField, Min(0)] private int gridColumns;
        [Tooltip("The GridLayoutGroup's spacing when upright (negative leaves it).")]
        [SerializeField] private Vector2 gridSpacing = new(-1f, -1f);
        [Tooltip("The Horizontal / Vertical LayoutGroup's spacing when upright (negative leaves it).")]
        [SerializeField] private float layoutSpacing = -1f;

        private RectTransform _rect;
        private bool _authoredRead;
        private Vector2 _aMin, _aMax, _pivot, _pos, _size;
        private Vector3 _scale;
        private LayoutElement _le;
        private float _leW, _leH, _leMinW, _leMinH;
        private CanvasGroup _group;
        private float _groupAlpha;
        private bool _groupInteractable, _groupBlocks;
        private GridLayoutGroup _grid;
        private Vector2 _gridCell, _gridSpace;
        private GridLayoutGroup.Constraint _gridConstraint;
        private int _gridCount;
        private HorizontalOrVerticalLayoutGroup _hv;
        private float _hvSpacing;
        private AdaptiveCanvasScaler _scaler;

        /// <summary>Whether the upright pose is on.</summary>
        public bool Upright { get; private set; }

        /// <summary>Whether this block is the PC layout fitted to the width when upright.</summary>
        public bool DrawsAtPcScale => drawAtPcScale;

        private RectTransform Rect => _rect ? _rect : (_rect = (RectTransform)transform);

        private void OnEnable()
        {
            if (!_scaler) _scaler = GetComponentInParent<AdaptiveCanvasScaler>(true);
            if (_scaler && _scaler.UsesPortraitLayout != Upright) Apply(_scaler.UsesPortraitLayout);
        }

        private void OnTransformParentChanged() => _scaler = null;

        // A fitted block fills its parent, so it re-fits whenever the parent's rect changes (the canvas switching reference).
        private void OnRectTransformDimensionsChange()
        {
            if (Upright && drawAtPcScale) FitPcBlock();
        }

        /// <summary>Put the piece in its upright pose, or back in its authored one.</summary>
        public void Apply(bool upright)
        {
            if (upright == Upright) return;
            // An entrance playing now offsets the rect: land it first, so the authored pose read (or put back) is the rest
            // pose and the entrance does not carry on from the old one.
            if (TryGetComponent(out KitEntrance entrance) && entrance.IsPlaying) entrance.Finish();
            var r = Rect;
            if (upright)
            {
                if (!_authoredRead) ReadAuthored(r);
                Upright = true;
                if (drawAtPcScale)
                {
                    FitPcBlock();
                    return;
                }
                if (movesRect)
                {
                    r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.pivot = pivot;
                    r.anchoredPosition = anchoredPosition; r.sizeDelta = sizeDelta;
                }
                if (scale > 0f) r.localScale = new Vector3(scale, scale, 1f);
                if (_le)
                {
                    if (layoutSize.x > 0f) { _le.preferredWidth = layoutSize.x; if (_leMinW > layoutSize.x) _le.minWidth = layoutSize.x; }
                    if (layoutSize.y > 0f) { _le.preferredHeight = layoutSize.y; if (_leMinH > layoutSize.y) _le.minHeight = layoutSize.y; }
                }
                ApplyChildren(true);
                return;
            }
            Upright = false;
            if (!_authoredRead) return;
            if (movesRect || drawAtPcScale)
            {
                r.anchorMin = _aMin; r.anchorMax = _aMax; r.pivot = _pivot;
                r.anchoredPosition = _pos; r.sizeDelta = _size;
            }
            if (scale > 0f || drawAtPcScale) r.localScale = _scale;
            if (_le)
            {
                if (layoutSize.x > 0f) { _le.preferredWidth = _leW; _le.minWidth = _leMinW; }
                if (layoutSize.y > 0f) { _le.preferredHeight = _leH; _le.minHeight = _leMinH; }
            }
            ApplyChildren(false);
        }

        /// <summary>
        /// Fit the PC layout to the width: s = parent width / pcWidth, laid out in a virtual parent pcWidth wide and
        /// parent height / s tall with the authored pose, then scaled by s about its pivot. The pivot lands at the authored
        /// position times s, and the stretched span grows by the difference between the virtual parent and the real one.
        /// </summary>
        public void FitPcBlock()
        {
            if (!Upright || !drawAtPcScale) return;
            var r = Rect;
            if (r.parent is not RectTransform parent) return;
            var size = parent.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float s = size.x / pcWidth;
            var span = _aMax - _aMin;
            var delta = _size + Vector2.Scale(span, size) * (1f / s - 1f);
            var pos = _pos * s;
            if ((r.sizeDelta - delta).sqrMagnitude > 0.0001f) r.sizeDelta = delta;
            if ((r.anchoredPosition - pos).sqrMagnitude > 0.0001f) r.anchoredPosition = pos;
            var scaleV = new Vector3(s * _scale.x, s * _scale.y, _scale.z);
            if (r.localScale != scaleV) r.localScale = scaleV;
        }

        private void ReadAuthored(RectTransform r)
        {
            _aMin = r.anchorMin; _aMax = r.anchorMax; _pivot = r.pivot;
            _pos = r.anchoredPosition; _size = r.sizeDelta; _scale = r.localScale;
            if (TryGetComponent(out _le)) { _leW = _le.preferredWidth; _leH = _le.preferredHeight; _leMinW = _le.minWidth; _leMinH = _le.minHeight; }
            if (TryGetComponent(out _group)) { _groupAlpha = _group.alpha; _groupInteractable = _group.interactable; _groupBlocks = _group.blocksRaycasts; }
            if (TryGetComponent(out _grid))
            {
                _gridCell = _grid.cellSize; _gridSpace = _grid.spacing;
                _gridConstraint = _grid.constraint; _gridCount = _grid.constraintCount;
            }
            if (TryGetComponent(out _hv)) _hvSpacing = _hv.spacing;
            _authoredRead = true;
        }

        private void ApplyChildren(bool upright)
        {
            if (hideInPortrait && _group)
            {
                _group.alpha = upright ? 0f : _groupAlpha;
                _group.interactable = !upright && _groupInteractable;
                _group.blocksRaycasts = !upright && _groupBlocks;
            }
            if (_grid)
            {
                if (gridCellSize.x > 0f && gridCellSize.y > 0f) _grid.cellSize = upright ? gridCellSize : _gridCell;
                if (gridColumns > 0)
                {
                    _grid.constraint = upright ? GridLayoutGroup.Constraint.FixedColumnCount : _gridConstraint;
                    _grid.constraintCount = upright ? gridColumns : _gridCount;
                }
                if (gridSpacing.x >= 0f && gridSpacing.y >= 0f) _grid.spacing = upright ? gridSpacing : _gridSpace;
            }
            if (_hv && layoutSpacing >= 0f) _hv.spacing = upright ? layoutSpacing : _hvSpacing;
        }

        /// <summary>Set the upright pose from code (tests and editor tooling).</summary>
        public void SetPose(Vector2 aMin, Vector2 aMax, Vector2 pv, Vector2 pos, Vector2 size, float uprightScale = 0f)
        {
            movesRect = true; anchorMin = aMin; anchorMax = aMax; pivot = pv; anchoredPosition = pos; sizeDelta = size;
            scale = uprightScale;
        }

        /// <summary>Make this a PC-layout block fitted to the width from code (tests and editor tooling).</summary>
        public void SetDrawAtPcScale(bool on, float referenceWidth = 1920f)
        {
            drawAtPcScale = on; pcWidth = referenceWidth;
        }

        /// <summary>Set the upright grid from code (tests and editor tooling).</summary>
        public void SetGrid(int columns, Vector2 cell)
        {
            movesRect = false; gridColumns = columns; gridCellSize = cell;
        }
    }
}
