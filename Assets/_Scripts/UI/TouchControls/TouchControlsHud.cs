using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// A vessel HUD's phone controls (Garrett, 2026-10-21; Docs/Reports/MOBILE_VESSELS.md). Added by
    /// <see cref="VesselHUDController"/> on a touch-first device only, and active only while no pad is
    /// connected (<see cref="KitInputDevice.ShowsControlHints"/> false) - connect a pad and the fleet row
    /// comes back exactly as it was.
    ///
    /// <para><b>ThumbLifts hulls</b> keep the fleet row. A card whose ability is on a thumb lift is not a
    /// button (reaching for it would lift a thumb and fire that ability anyway); it shows the GLYPH of the
    /// lift instead, where a pad shows its trigger.</para>
    ///
    /// <para><b>TotemButtons hulls</b> lose the lifts (TouchInputStrategy raises nothing for them), so the
    /// cards ARE the controls: every pressable card and the profile's extra buttons are laid out round the
    /// right thumb by <see cref="TouchButtonLayout"/> at a size in millimetres, and the cards that press
    /// nothing (passive, locked, replaced by a slider) step down to a small strip along the bottom edge,
    /// left of the cluster, where they still show their gauges and upgrades but take no touch.</para>
    ///
    /// <para>Re-laid whenever the screen, the safe area or the pilot's device changes.</para>
    /// </summary>
    public class TouchControlsHud : MonoBehaviour
    {
        const float DisplayStripMillimetres = 6f;
        const float FallbackDpi = 160f;

        AbilityLockupView _lockup;
        IVesselStatus _status;
        TouchControlProfile _profile;
        TouchControlProfileLibrarySO _library;

        readonly List<AbilityLockupView.CardInfo> _cards = new();
        readonly List<AbilityLockupView.CardInfo> _buttonCards = new();
        readonly List<AbilityLockupView.CardInfo> _displayCards = new();
        readonly List<TouchButtonView> _extras = new();
        readonly List<float> _aspects = new();
        TouchThrottleSlider _slider;

        bool _laidOut;
        bool _active;
        int _screenWidth;
        int _screenHeight;
        Rect _safeArea;
        InputDeviceType _device;

        /// <summary>Gives <paramref name="view"/>'s HUD its phone controls, on a touch-first device.</summary>
        public static void Attach(VesselHUDView view, IVesselStatus status)
        {
            if (!view || status == null || !KitInputDevice.IsTouchPrimary) return;
            if (!view.TryGetComponent(out TouchControlsHud hud)) hud = view.gameObject.AddComponent<TouchControlsHud>();
            hud.Initialize(view, status);
        }

        void Initialize(VesselHUDView view, IVesselStatus status)
        {
            _lockup = view.GetComponent<AbilityLockupView>();
            _status = status;
            _library = TouchControlProfileLibrarySO.Instance;
            _profile = TouchControlProfileLibrarySO.For(status.VesselType);
            BuildExtras();
            _laidOut = false;
            _active = false;
            Refresh(force: true);
        }

        void OnDestroy()
        {
            ClearExtras();
        }

        void Update() => Refresh(force: false);

        void Refresh(bool force)
        {
            if (!_lockup || _status == null || _profile == null) return;

            bool active = !KitInputDevice.ShowsControlHints;
            var device = _status.InputStatus != null ? _status.InputStatus.ActiveInputDevice : InputDeviceType.Touch;
            bool screenChanged = Screen.width != _screenWidth || Screen.height != _screenHeight || Screen.safeArea != _safeArea;

            if (!force && active == _active && device == _device && !(active && screenChanged)) return;

            _active = active;
            _device = device;
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _safeArea = Screen.safeArea;

            if (active) Apply();
            else Revert();
        }

        // -----------------------------------------------------------------
        // Building
        // -----------------------------------------------------------------

        void BuildExtras()
        {
            ClearExtras();
            var row = _lockup ? _lockup.Row : null;
            if (!row) return;

            var disc = _library ? _library.ButtonDisc : null;
            var rim = _library ? _library.ButtonRim : null;
            float size = _lockup.CardWidth;

            if (_profile.scheme == TouchScheme.TotemButtons)
            {
                foreach (var definition in _profile.extraButtons)
                {
                    if (definition == null) continue;
                    var tint = definition.tint == Element.None ? _lockup.NeutralColour : _lockup.ColourFor(definition.tint);
                    var button = TouchButtonView.Create(row, definition, tint, disc, rim, size);
                    button.Bind(_status.InputStatus);
                    _extras.Add(button);
                }
            }

            if (_profile.throttleSlider)
            {
                _slider = TouchThrottleSlider.Create(row, disc, _lockup.ColourFor(Element.Time), size * 0.8f, size * 3f);
                _slider.Bind(_status.InputStatus, _profile.throttleSliderStart);
                _slider.Flicked += OnSliderFlicked;
            }
        }

        /// <summary>An upward flick on the throttle slider: the hull's phone Snap Dash, if it has one.</summary>
        void OnSliderFlicked()
        {
            if (_status?.VesselTransformer is ScarabVesselTransformer scarab) scarab.TryTouchSnapDash();
        }

        void ClearExtras()
        {
            for (int i = 0; i < _extras.Count; i++)
                if (_extras[i]) Destroy(_extras[i].gameObject);
            _extras.Clear();
            if (_slider) Destroy(_slider.gameObject);
            _slider = null;
        }

        // -----------------------------------------------------------------
        // Applying the phone layout, and undoing it
        // -----------------------------------------------------------------

        void Apply()
        {
            _lockup.GetCards(_cards);
            bool lifts = _profile.UsesThumbLifts;

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (!card.Press) continue;

                bool displayOnly = !card.IsCore && _profile.IsDisplayOnly(card.Element);
                bool onALift = lifts && IsLiftInput(card.Press.BoundInput);
                card.Press.SetToggle(!lifts && !card.IsCore && _profile.IsToggle(card.Element));
                card.Press.SetBlocked(displayOnly || onALift);

                var glyph = lifts
                    ? (card.IsCore ? _profile.GlyphFor(card.Core) : _profile.GlyphFor(card.Element))
                    : TouchGlyph.None;
                var sprite = _library ? _library.GlyphSprite(glyph) : null;
                if (card.IsCore) _lockup.SetCoreTouchGlyph(card.Core, sprite);
                else _lockup.SetTouchGlyph(card.Element, sprite);
            }

            for (int i = 0; i < _extras.Count; i++)
                if (_extras[i]) _extras[i].gameObject.SetActive(true);
            if (_slider) _slider.gameObject.SetActive(true);

            if (_profile.scheme == TouchScheme.TotemButtons) LayOutButtons();
            else if (_laidOut)
            {
                _lockup.RestoreRowLayout();
                _laidOut = false;
            }
        }

        void Revert()
        {
            _lockup.GetCards(_cards);
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (!card.Press) continue;
                card.Press.SetToggle(false);
                card.Press.SetBlocked(false);
                if (card.IsCore) _lockup.SetCoreTouchGlyph(card.Core, null);
                else _lockup.SetTouchGlyph(card.Element, null);
            }

            for (int i = 0; i < _extras.Count; i++)
                if (_extras[i]) _extras[i].gameObject.SetActive(false);
            if (_slider) _slider.gameObject.SetActive(false);

            if (_laidOut)
            {
                _lockup.RestoreRowLayout();
                _laidOut = false;
            }
        }

        /// <summary>The events a thumb lift raises: the pad's triggers, alone or together, and letting go.</summary>
        static bool IsLiftInput(InputEvents input) => input is InputEvents.LeftStickAction
            or InputEvents.RightStickAction or InputEvents.OnlyLeftStickAction or InputEvents.OnlyRightStickAction
            or InputEvents.BothSticksAction or InputEvents.IdleAction;

        void LayOutButtons()
        {
            var row = _lockup.Row;
            if (!row) return;
            var canvas = row.GetComponentInParent<Canvas>();
            var root = canvas ? canvas.rootCanvas : null;
            Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;

            // Who is a button: primary extras first (a fire button takes the best reach), then the
            // pressable cards in the row's own order, then the other extras. Everything else is display.
            _buttonCards.Clear();
            _displayCards.Clear();
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (!card.Host) continue;
                if (card.Press && card.Press.Pressable) _buttonCards.Add(card);
                else _displayCards.Add(card);
            }
            _buttonCards.Sort((a, b) => Order(a).CompareTo(Order(b)));
            _displayCards.Sort((a, b) => Order(a).CompareTo(Order(b)));

            float totemAspect = _lockup.CardHeight / Mathf.Max(1f, _lockup.CardWidth);
            _aspects.Clear();
            for (int i = 0; i < _extras.Count; i++) if (_extras[i].Definition.primary) _aspects.Add(1f);
            for (int i = 0; i < _buttonCards.Count; i++) _aspects.Add(totemAspect);
            for (int i = 0; i < _extras.Count; i++) if (!_extras[i].Definition.primary) _aspects.Add(1f);

            float dpi = Screen.dpi > 1f ? Screen.dpi : FallbackDpi;
            var screen = new Vector2(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            var layout = TouchButtonLayout.Arrange(_aspects, screen, safe, dpi, LayoutSettings(_library));

            float unitsPerPixel = UnitsPerPixel(row, cam);
            float buttonUnits = layout.ButtonWidthPixels * unitsPerPixel;
            float cardScale = buttonUnits / Mathf.Max(1f, _lockup.CardWidth);

            int slot = 0;
            float clusterLeft = safe.xMax;
            float clusterTop = safe.yMin;
            for (int i = 0; i < _extras.Count; i++)
                if (_extras[i].Definition.primary) PlaceExtra(_extras[i], layout, slot++, row, cam, cardScale, ref clusterLeft, ref clusterTop);
            for (int i = 0; i < _buttonCards.Count; i++)
            {
                var card = _buttonCards[i];
                if (slot < layout.Centres.Count)
                {
                    var centre = layout.Centres[slot];
                    PlaceCard(card.Host, centre, row, cam, cardScale);
                    clusterLeft = Mathf.Min(clusterLeft, centre.x - layout.ButtonWidthPixels * 0.5f);
                    clusterTop = Mathf.Max(clusterTop, centre.y + layout.ButtonWidthPixels * totemAspect * 0.5f);
                }
                else _displayCards.Add(card);   // did not fit: shown, smaller, with the display strip
                slot++;
            }
            for (int i = 0; i < _extras.Count; i++)
                if (!_extras[i].Definition.primary) PlaceExtra(_extras[i], layout, slot++, row, cam, cardScale, ref clusterLeft, ref clusterTop);

            LayOutDisplayStrip(row, cam, dpi, unitsPerPixel, clusterLeft, safe);
            LayOutSlider(row, cam, dpi, unitsPerPixel, clusterTop, safe);
            _laidOut = true;
        }

        /// <summary>The library's button sizes and corner box (the shipped defaults without one). Shared with the
        /// buttons a seat lays out itself (<c>OrcaHUDController</c>).</summary>
        public static TouchButtonLayout.Settings LayoutSettings(TouchControlProfileLibrarySO library) => library
            ? new TouchButtonLayout.Settings
            {
                ButtonMillimetres = library.ButtonMillimetres,
                GapMillimetres = library.ButtonGapMillimetres,
                InnerArcMillimetres = library.InnerArcMillimetres,
                ArcSpacingMillimetres = library.ArcSpacingMillimetres,
                MaxHeightFraction = library.MaxHeightFraction,
                MaxWidthFraction = library.MaxWidthFraction,
            }
            : new TouchButtonLayout.Settings
            {
                ButtonMillimetres = 10f, GapMillimetres = 1.5f, InnerArcMillimetres = 22f,
                ArcSpacingMillimetres = 12f, MaxHeightFraction = 0.55f, MaxWidthFraction = 0.36f,
            };

        static int Order(AbilityLockupView.CardInfo card)
        {
            if (card.IsCore) return -10 + (int)card.Core;
            var order = VesselHUDView.AbilityDisplayOrder;
            for (int i = 0; i < order.Length; i++) if (order[i] == card.Element) return i;
            return 100;
        }

        void PlaceCard(RectTransform host, Vector2 totemCentreScreen, RectTransform row, Camera cam, float scale)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(row, totemCentreScreen, cam, out var local)) return;
            host.localScale = Vector3.one * scale;
            // The totem's centre sits CardCentreAboveHost above its host's centre (the element plate is on top).
            host.anchoredPosition = local - new Vector2(0f, _lockup.CardCentreAboveHost * scale);
        }

        void PlaceExtra(TouchButtonView button, TouchButtonLayout.Result layout, int slot, RectTransform row, Camera cam,
                        float scale, ref float clusterLeft, ref float clusterTop)
        {
            if (slot >= layout.Centres.Count)
            {
                button.gameObject.SetActive(false);   // reported by Fits; the layout already shrank to 7 mm
                return;
            }
            var centre = layout.Centres[slot];
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(row, centre, cam, out var local)) return;
            var rt = (RectTransform)button.transform;
            rt.anchoredPosition = local;
            button.SetBaseScale(scale);
            clusterLeft = Mathf.Min(clusterLeft, centre.x - layout.ButtonWidthPixels * 0.5f);
            clusterTop = Mathf.Max(clusterTop, centre.y + layout.ButtonWidthPixels * 0.5f);
        }

        /// <summary>The cards that press nothing, small, along the bottom edge left of the buttons.</summary>
        void LayOutDisplayStrip(RectTransform row, Camera cam, float dpi, float unitsPerPixel, float clusterLeft, Rect safe)
        {
            if (_displayCards.Count == 0) return;
            float pxPerMm = dpi / 25.4f;
            float width = DisplayStripMillimetres * pxPerMm;
            float height = width * _lockup.CardHeight / Mathf.Max(1f, _lockup.CardWidth);
            float gap = 1f * pxPerMm;
            float scale = width * unitsPerPixel / Mathf.Max(1f, _lockup.CardWidth);
            float x = clusterLeft - gap - width * 0.5f;
            float y = safe.yMin + gap + height * 0.5f;
            for (int i = _displayCards.Count - 1; i >= 0; i--)
            {
                PlaceCard(_displayCards[i].Host, new Vector2(x, y), row, cam, scale);
                x -= width + gap;
            }
        }

        /// <summary>The throttle slider stands at the right edge, above the buttons.</summary>
        void LayOutSlider(RectTransform row, Camera cam, float dpi, float unitsPerPixel, float clusterTop, Rect safe)
        {
            if (!_slider || !_library) return;
            float pxPerMm = dpi / 25.4f;
            float width = _library.SliderWidthMillimetres * pxPerMm;
            float height = _library.SliderHeightMillimetres * pxPerMm;
            float top = Mathf.Min(safe.yMax - 2f * pxPerMm, clusterTop + 3f * pxPerMm + height);
            var centre = new Vector2(safe.xMax - 2f * pxPerMm - width * 0.5f, top - height * 0.5f);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(row, centre, cam, out var local)) return;
            var rt = (RectTransform)_slider.transform;
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(width, height) * unitsPerPixel;
            rt.anchoredPosition = local;
            _slider.Bind(_status.InputStatus, _slider.Value);
        }

        /// <summary>Canvas units under <paramref name="rect"/> per screen pixel, measured rather than assumed
        /// (overlay, camera and scaled canvases all answer it the same way).</summary>
        public static float UnitsPerPixel(RectTransform rect, Camera cam)
        {
            const float probe = 100f;
            var origin = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, origin, cam, out var a)) return 1f;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, origin + new Vector2(probe, 0f), cam, out var b)) return 1f;
            float units = (b - a).magnitude;
            return units > 0f ? units / probe : 1f;
        }
    }
}
