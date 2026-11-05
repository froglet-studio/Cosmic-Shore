using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A panel's place on a phone (Docs/UI_STYLE_GUIDE.md §8, D6; the approved phone boards, Docs/MENU_REDESIGN_B.md
    /// §124-125). The RectTransform keeps its authored 1920 x 1080 pose; this component holds the pose the panel takes
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

        private RectTransform _rect;
        private bool _authoredRead;
        private Vector2 _aMin, _aMax, _pivot, _pos, _size;
        private float _font, _fontMax, _leW, _leH;
        private Vector3 _scale;
        private TMP_Text _text;
        private LayoutElement _le;
        private AdaptiveCanvasScaler _scaler;
        private bool _scalerResolved;

        /// <summary>Whether the phone pose is on.</summary>
        public bool OnPhone { get; private set; }

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

        /// <summary>Put the panel in its phone pose, or back in its authored one.</summary>
        public void Apply(bool phone)
        {
            if (phone == OnPhone) return;
            var r = Rect;
            if (phone)
            {
                if (!_authoredRead) ReadAuthored(r);
                if (movesRect) Set(r, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
                if (scale > 0f) r.localScale = new Vector3(scale, scale, 1f);
                if (_text && fontSize > 0f)
                {
                    _text.fontSize = fontSize;
                    if (_text.enableAutoSizing) _text.fontSizeMax = fontSize;
                }
                if (_le)
                {
                    if (layoutSize.x > 0f) _le.preferredWidth = layoutSize.x;
                    if (layoutSize.y > 0f) _le.preferredHeight = layoutSize.y;
                }
            }
            else if (_authoredRead)
            {
                if (movesRect) Set(r, _aMin, _aMax, _pivot, _pos, _size);
                if (scale > 0f) r.localScale = _scale;
                if (_text && fontSize > 0f)
                {
                    _text.fontSize = _font;
                    if (_text.enableAutoSizing) _text.fontSizeMax = _fontMax;
                }
                if (_le)
                {
                    if (layoutSize.x > 0f) _le.preferredWidth = _leW;
                    if (layoutSize.y > 0f) _le.preferredHeight = _leH;
                }
            }
            OnPhone = phone;
        }

        private void ReadAuthored(RectTransform r)
        {
            _aMin = r.anchorMin; _aMax = r.anchorMax; _pivot = r.pivot;
            _pos = r.anchoredPosition; _size = r.sizeDelta; _scale = r.localScale;
            if (TryGetComponent(out _text)) { _font = _text.fontSize; _fontMax = _text.fontSizeMax; }
            if (TryGetComponent(out _le)) { _leW = _le.preferredWidth; _leH = _le.preferredHeight; }
            _authoredRead = true;
        }

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

        /// <summary>Set the phone text and layout sizes from code (tests and editor tooling).</summary>
        public void SetPhoneSizes(float phoneFontSize, Vector2 phoneLayoutSize, bool moves, float phoneScale = 0f)
        {
            fontSize = phoneFontSize; layoutSize = phoneLayoutSize; movesRect = moves; scale = phoneScale;
        }
    }
}
