using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Arrows that step a horizontal row on a phone (Docs/MENU_REDESIGN_B.md section 135: the Voyage window's Quests page,
    /// one row of quest cards with three in view). On a phone (its canvas's <see cref="AdaptiveCanvasScaler"/> at the
    /// phone layout) the arrows and the dashes show; each press moves the row by <see cref="step"/>, an arrow goes
    /// non-interactable at its end of the row, and the dash for the part of the row in view is lit. On PC the controls
    /// are hidden and take no input (the row fits there, nothing scrolls). A swipe still scrolls the row on its own.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Phone Scroll Stepper")]
    public class PhoneScrollStepper : MonoBehaviour
    {
        [Header("The row")]
        [Tooltip("The horizontal ScrollRect the arrows step.")]
        [SerializeField] private ScrollRect scroll;
        [Tooltip("How far one press moves the row, in the row content's units (one card and the gap after it).")]
        [SerializeField, Min(1f)] private float step = 216f;

        [Header("Phone-only controls")]
        [Tooltip("The arrows' and dashes' CanvasGroup: shown on a phone, hidden and not interactable on PC.")]
        [SerializeField] private CanvasGroup controls;
        [Tooltip("Steps the row back (left).")]
        [SerializeField] private Button previous;
        [Tooltip("Steps the row on (right).")]
        [SerializeField] private Button next;
        [Tooltip("One dash per part of the row, left to right; the dash for the part in view keeps its authored alpha.")]
        [SerializeField] private Graphic[] dashes = System.Array.Empty<Graphic>();
        [Tooltip("A dash that is not lit draws at its authored alpha times this.")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.3f;

        private AdaptiveCanvasScaler _scaler;
        private bool _phone;
        private float[] _litAlpha;

        /// <summary>Whether the phone controls are on.</summary>
        public bool OnPhone => _phone;

        private void Awake()
        {
            ReadDashes();
            if (previous) previous.onClick.AddListener(Previous);
            if (next) next.onClick.AddListener(Next);
        }

        private void OnEnable()
        {
            _scaler = GetComponentInParent<AdaptiveCanvasScaler>(true);
            if (_scaler) _scaler.PhoneLayoutChanged += OnLayout;
            if (scroll) scroll.onValueChanged.AddListener(OnScrolled);
            OnLayout(_scaler && _scaler.UsesPhoneLayout);
        }

        private void OnDisable()
        {
            if (_scaler) _scaler.PhoneLayoutChanged -= OnLayout;
            if (scroll) scroll.onValueChanged.RemoveListener(OnScrolled);
        }

        private void OnDestroy()
        {
            if (previous) previous.onClick.RemoveListener(Previous);
            if (next) next.onClick.RemoveListener(Next);
        }

        /// <summary>Show the controls on a phone, hide them on PC.</summary>
        public void OnLayout(bool phone)
        {
            _phone = phone;
            if (controls)
            {
                controls.alpha = phone ? 1f : 0f;
                controls.interactable = phone;
                controls.blocksRaycasts = phone;
            }
            Refresh();
        }

        public void Previous() => Step(-1);

        public void Next() => Step(1);

        /// <summary>Move the row one step back (-1) or on (+1), clamped to its ends.</summary>
        public void Step(int direction)
        {
            if (!_phone || !scroll || !scroll.content) return;
            float range = ScrollRange();
            if (range <= 0f) return;
            scroll.StopMovement();
            scroll.horizontalNormalizedPosition = Advance(scroll.horizontalNormalizedPosition, range, step, direction);
            Refresh();
        }

        /// <summary>The normalized position one step on from <paramref name="normalized"/>, for a row that scrolls
        /// <paramref name="range"/> content units; 0 when the row does not scroll.</summary>
        public static float Advance(float normalized, float range, float step, int direction)
        {
            if (range <= 0f) return 0f;
            return Mathf.Clamp01(normalized + Mathf.Sign(direction) * step / range);
        }

        /// <summary>The dash lit at <paramref name="normalized"/> of <paramref name="count"/> dashes (-1 with none).</summary>
        public static int DashFor(float normalized, int count)
        {
            if (count <= 0) return -1;
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(normalized) * count), 0, count - 1);
        }

        private void OnScrolled(Vector2 _) => Refresh();

        private float ScrollRange()
        {
            var view = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            return scroll.content.rect.width - view.rect.width;
        }

        private void ReadDashes()
        {
            if (_litAlpha != null && _litAlpha.Length == dashes.Length) return;
            _litAlpha = new float[dashes.Length];
            for (int i = 0; i < dashes.Length; i++) _litAlpha[i] = dashes[i] ? dashes[i].color.a : 1f;
        }

        private void Refresh()
        {
            ReadDashes();
            float range = scroll && scroll.content ? ScrollRange() : 0f;
            float pos = range > 0f ? scroll.horizontalNormalizedPosition : 0f;
            if (previous) previous.interactable = _phone && range > 0f && pos > 0.001f;
            if (next) next.interactable = _phone && range > 0f && pos < 0.999f;
            int lit = DashFor(pos, dashes.Length);
            for (int i = 0; i < dashes.Length; i++)
            {
                if (!dashes[i]) continue;
                var c = dashes[i].color;
                c.a = i == lit ? _litAlpha[i] : _litAlpha[i] * dimAlpha;
                dashes[i].color = c;
            }
        }
    }
}
