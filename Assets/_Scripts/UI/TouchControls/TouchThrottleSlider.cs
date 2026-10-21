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
    public class TouchThrottleSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        IInputStatus _input;
        RectTransform _region;
        Image _fill;
        Image _knob;
        float _value;
        Camera _eventCamera;

        public float Value => _value;

        public static TouchThrottleSlider Create(RectTransform parent, Sprite disc, Color tint, float width, float height)
        {
            var go = new GameObject("TouchThrottleSlider", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);

            var slider = go.AddComponent<TouchThrottleSlider>();
            slider._region = rt;

            var track = Bar(rt, "Track", width * 0.42f, height, new Color(tint.r * 0.25f, tint.g * 0.25f, tint.b * 0.25f, 0.55f));
            track.raycastTarget = true;   // the whole column takes the drag
            slider._fill = Bar(rt, "Fill", width * 0.42f, height, new Color(tint.r, tint.g, tint.b, 0.75f));
            slider._fill.type = Image.Type.Filled;
            slider._fill.fillMethod = Image.FillMethod.Vertical;
            slider._fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            slider._fill.sprite = null;

            var knobGo = new GameObject("Knob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            slider._knob = knobGo.GetComponent<Image>();
            var krt = slider._knob.rectTransform;
            krt.SetParent(rt, false);
            krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(0.5f, 0.5f);
            krt.sizeDelta = new Vector2(width, width);
            slider._knob.sprite = disc;
            slider._knob.color = Color.white;
            slider._knob.raycastTarget = false;
            // AddComponent ran OnEnable before the region existed, so claim now.
            if (slider.isActiveAndEnabled) FlightTouchClaims.Add(rt);
            return slider;
        }

        static Image Bar(RectTransform parent, string name, float width, float height, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var image = go.GetComponent<Image>();
            var rt = image.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
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
            Follow(eventData.position);
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
            if (_fill) _fill.fillAmount = _value;
            if (_knob && _region)
                _knob.rectTransform.anchoredPosition = new Vector2(0f, (_value - 0.5f) * _region.rect.height);
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
