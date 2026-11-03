using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A set-and-hold throttle for a phone (Garrett, 2026-10-21: "scarab can have a throttle appear on
    /// mobile as a slider that is set"). Drag it to a speed and let go: the speed stays, so neither thumb
    /// is spent holding the throttle the way a pad's trigger is. It writes <c>InputStatus.Throttle</c>
    /// (replicated, owner-write) - the manual throttle channel no touch strategy writes - which the
    /// Scarab reads on touch in place of its trigger depth.
    /// </summary>
    public class TouchThrottleSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>A quick upward flick that started on the slider (the Scarab's Snap Dash). The slider
        /// is put back where it was set before the flick, so a dash never moves the throttle.</summary>
        public event System.Action Flicked;

        // A flick: up at least this far, this fast, in at most this long. Millimetres, so it reads the
        // same on every screen.
        const float FlickMinMillimetres = 8f;
        const float FlickMaxSeconds = 0.25f;
        const float FallbackDpi = 160f;

        float _downTime;
        Vector2 _downPosition;
        float _valueAtDown;
        bool _down;
        IInputStatus _input;
        RectTransform _region;
        Image _fill;
        Image _knob;
        float _value;
        Camera _eventCamera;

        public float Value => _value;

        // The bars take this middle share of the column's width; the column itself takes the touch.
        const float BarLeft = 0.29f;
        const float BarRight = 0.71f;

        public static TouchThrottleSlider Create(RectTransform parent, Sprite disc, Color tint, float width, float height)
        {
            var go = new GameObject("TouchThrottleSlider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);

            // The whole column takes the drag, however TouchControlsHud later sizes it: an invisible
            // graphic on the root, not on a child sized once at build time. The old Track child kept the
            // build-time size when LayOutSlider resized the root, so the drag area (a few millimetres
            // wide) and the span the value was measured over disagreed.
            var hit = go.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            var slider = go.AddComponent<TouchThrottleSlider>();
            slider._region = rt;

            Bar(rt, "Track", new Color(tint.r * 0.25f, tint.g * 0.25f, tint.b * 0.25f, 0.55f));
            // The fill is the bar's lower part: its top anchor follows the value (SetValue). An Image set to
            // Filled with no sprite draws as a plain quad and ignores fillAmount, so it never showed a level.
            slider._fill = Bar(rt, "Fill", new Color(tint.r, tint.g, tint.b, 0.75f));

            var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            slider._knob = knobGo.GetComponent<Image>();
            var krt = slider._knob.rectTransform;
            krt.SetParent(rt, false);
            krt.pivot = new Vector2(0.5f, 0.5f);
            krt.anchoredPosition = Vector2.zero;
            slider._knob.sprite = disc;
            slider._knob.color = Color.white;
            slider._knob.raycastTarget = false;
            slider.OnRectTransformDimensionsChange();
            slider.SetValue(slider._value);
            // AddComponent ran OnEnable before the region existed, so claim now.
            if (slider.isActiveAndEnabled) FlightTouchClaims.Add(rt);
            return slider;
        }

        /// <summary>A bar that follows the column: stretched to its height, centred in its width.</summary>
        static Image Bar(RectTransform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var image = go.GetComponent<Image>();
            var rt = image.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(BarLeft, 0f);
            rt.anchorMax = new Vector2(BarRight, 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Whose throttle it sets, and where it starts.</summary>
        public void Bind(IInputStatus input, float start)
        {
            _input = input;
            SetValue(start);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _eventCamera = eventData.pressEventCamera;
            _down = true;
            _downTime = Time.unscaledTime;
            _downPosition = eventData.position;
            _valueAtDown = _value;
            Follow(eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_down) return;
            _down = false;
            float pxPerMm = (Screen.dpi > 1f ? Screen.dpi : FallbackDpi) / 25.4f;
            float rise = (eventData.position.y - _downPosition.y) / pxPerMm;
            float across = Mathf.Abs(eventData.position.x - _downPosition.x) / pxPerMm;
            bool flick = Time.unscaledTime - _downTime <= FlickMaxSeconds
                         && rise >= FlickMinMillimetres && rise > across;
            if (!flick) return;
            SetValue(_valueAtDown);
            Flicked?.Invoke();
        }

        public void OnDrag(PointerEventData eventData) => Follow(eventData.position);

        void Follow(Vector2 screenPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_region, screenPoint, _eventCamera, out var local))
                return;
            float height = _region.rect.height;
            SetValue(height > 0f ? local.y / height + 0.5f : 0f);
        }

        void SetValue(float value)
        {
            _value = Mathf.Clamp01(value);
            if (_input != null) _input.Throttle = _value;
            if (_fill) _fill.rectTransform.anchorMax = new Vector2(BarRight, _value);
            if (_knob)
            {
                var krt = _knob.rectTransform;
                krt.anchorMin = krt.anchorMax = new Vector2(0.5f, _value);
                krt.anchoredPosition = Vector2.zero;
            }
        }

        // The knob is a disc as wide as the column, whatever size the HUD lays the column out at.
        void OnRectTransformDimensionsChange()
        {
            if (!_knob || !_region) return;
            float side = _region.rect.width;
            _knob.rectTransform.sizeDelta = new Vector2(side, side);
        }

        void OnEnable()
        {
            if (_region) FlightTouchClaims.Add(_region);
            if (_input != null) _input.Throttle = _value;
        }

        void OnDisable() => FlightTouchClaims.Remove(_region);

        // The slider is the only writer while it shows - but a pad's bumper writes Throttle every frame it
        // flies, and InputStatus.ResetForReplay zeroes it, so the set value is re-asserted rather than
        // trusted to have stuck. Equal writes do not dirty the replicated field.
        void Update()
        {
            if (_input != null && !Mathf.Approximately(_input.Throttle, _value)) _input.Throttle = _value;
        }
    }
}
