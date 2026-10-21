using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One phone button that is not a totem card (a profile's <see cref="TouchExtraButton"/>): the Brittle
    /// Star's fire and ring, the Shrike's trigger and zoom, the Grizzly's bombs, the Scarab's drift, the
    /// Angler's squad and recall, the Stoat's hold and right dipole.
    ///
    /// <para>It presses on the pilot's <see cref="IInputStatus"/>, the channel every input strategy and
    /// every ability card raises on, so it inherits the autopilot / mute / suppression gates and the press
    /// replicates with its device like the control it stands in for (vessel rule 56). A touch that starts
    /// on it never flies (<see cref="FlightTouchClaims"/>); an AIM button's drag is the right stick while it
    /// is held (<see cref="FlightTouchClaims.AddAim"/>).</para>
    /// </summary>
    public class TouchButtonView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        IInputStatus _input;
        TouchExtraButton _definition;
        RectTransform _region;
        Image _plate;
        Image _rim;
        bool _holding;
        int _pointerId;
        Color _tint;

        public TouchExtraButton Definition => _definition;

        /// <summary>Builds a button under <paramref name="parent"/>, <paramref name="size"/> canvas units across.</summary>
        public static TouchButtonView Create(RectTransform parent, TouchExtraButton definition, Color tint,
                                             Sprite disc, Sprite rim, float size)
        {
            var go = new GameObject($"TouchButton_{definition.input}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            var view = go.AddComponent<TouchButtonView>();
            view._definition = definition;
            view._region = rt;
            view._tint = tint;

            view._plate = Child(rt, "Plate", disc, size);
            view._plate.raycastTarget = true;   // the whole disc takes the press
            view._rim = Child(rt, "Rim", rim, size);
            var icon = Child(rt, "Icon", definition.icon, size * 0.56f);
            icon.preserveAspect = true;
            icon.color = Color.white;

            if (!string.IsNullOrEmpty(definition.label))
            {
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var label = labelGo.GetComponent<TextMeshProUGUI>();
                var lrt = label.rectTransform;
                lrt.SetParent(rt, false);
                lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(size * 1.6f, size * 0.3f);
                lrt.anchoredPosition = new Vector2(0f, -size * 0.66f);
                label.text = definition.label;
                label.fontSize = size * 0.2f;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;
            }

            view.Paint();
            // AddComponent ran OnEnable before the region existed, so claim now.
            view.Claim();
            return view;
        }

        static Image Child(RectTransform parent, string name, Sprite sprite, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var image = go.GetComponent<Image>();
            var rt = image.rectTransform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            image.sprite = sprite;
            image.enabled = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Who the button presses for.</summary>
        public void Bind(IInputStatus input)
        {
            if (_holding && _input != input) Release();
            _input = input;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_definition == null || _input == null || !isActiveAndEnabled) return;
            if (_definition.behaviour == TouchButtonBehaviour.Toggle && _holding)
            {
                Release();
                return;
            }
            if (_holding) return;
            _holding = true;
            _pointerId = eventData.pointerId;
            _input.OnButtonPressed.Raise(_definition.input);
            Paint();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_definition == null || _definition.behaviour == TouchButtonBehaviour.Toggle) return;
            if (_holding && eventData.pointerId == _pointerId) Release();
        }

        void OnEnable() => Claim();

        void Claim()
        {
            if (!_region || _definition == null || !isActiveAndEnabled) return;
            if (_definition.aimsWhileHeld) FlightTouchClaims.AddAim(_region);
            else FlightTouchClaims.Add(_region);
        }

        // A hidden HUD, a vessel swap or a teardown must never leave the input held.
        void OnDisable()
        {
            Release();
            FlightTouchClaims.Remove(_region);
        }

        void Release()
        {
            if (!_holding) return;
            _holding = false;
            _input?.OnButtonReleased.Raise(_definition.input);
            Paint();
        }

        void Paint()
        {
            if (_plate) _plate.color = new Color(_tint.r * 0.25f, _tint.g * 0.25f, _tint.b * 0.25f, _holding ? 0.85f : 0.55f);
            if (_rim) _rim.color = new Color(_tint.r, _tint.g, _tint.b, _holding ? 1f : 0.8f);
            transform.localScale = Vector3.one * (_holding ? 0.94f : 1f) * _baseScale;
        }

        float _baseScale = 1f;

        /// <summary>The layout's scale; the press dip multiplies it.</summary>
        public void SetBaseScale(float scale)
        {
            _baseScale = scale;
            Paint();
        }
    }
}
