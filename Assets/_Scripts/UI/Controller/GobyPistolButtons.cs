using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Each Goby &amp; Pistol seat's own buttons on a phone (Garrett, 2026-10-21: "each seat has its own ui and controls"),
    /// round the right thumb, laid out by the fleet's <see cref="TouchButtonLayout"/> in millimetres inside the safe area -
    /// the Orca's gunner buttons (<see cref="OrcaHUDController"/>) are the pattern:
    /// <list type="bullet">
    /// <item><b>The Goby</b> (the hull, flown on two thumbs like the Squirrel; its four cards are the Squirrel's): Primer and
    /// Flares, each with its charges as a number. They press the pilot's <see cref="IInputStatus"/> with the inputs the
    /// hull binds to <see cref="CrewButtonActionSO"/> (Button3Action, Button4Action: a pad's X and Y, keys Q and F), so a
    /// phone, a pad and a keyboard reach the seat by one path.</item>
    /// <item><b>The Pistol</b> (the seat; the left thumb's drag launches, a held finger dives): Ghost (held) and Snap. They
    /// press the seat directly, because the hull's AI flies the Goby while a person flies the Pistol and an InputStatus
    /// press is dropped under autopilot. A button that is cooling down is dimmed.</item>
    /// </list>
    /// Only the seat the person is in shows its buttons, and none show while a pad is connected. A touch that starts on a
    /// button never flies (<see cref="FlightTouchClaims"/>). Owned by the seat (<see cref="GobyPistolDuo"/>), so the
    /// Play-mode menu that adds the duo to any hull gets them too. The buttons live under the hull HUD's ability row and
    /// die with it.
    /// </summary>
    public sealed class GobyPistolButtons
    {
        public const int Primer = 0, Flares = 1, Ghost = 2, Snap = 3;
        const int Count = 4;
        const string CountName = "SeatCount";
        static readonly float[] Aspects = { 1f, 1f };   // two per seat: the first takes the easiest reach

        readonly TouchButtonView[] _buttons = new TouchButtonView[Count];
        readonly TMP_Text[] _counts = new TMP_Text[Count];
        readonly int[] _shownCount = new int[Count];
        readonly int[] _shownReady = new int[Count];
        readonly Color[] _tints = new Color[Count];
        AbilityLockupView _lockup;
        int _laidSeat = -1, _screenWidth, _screenHeight;
        Rect _safeArea;

        /// <summary>True once the buttons exist (the hull's HUD row is built and this is a touch device).</summary>
        public bool Built => _buttons[Primer];

        /// <summary>
        /// Builds the four buttons under the ability row of <paramref name="hull"/>'s HUD. False (and nothing built) when
        /// the device is not touch-first or the row is not built yet; the seat tries again on a later frame.
        /// </summary>
        public bool TryBuild(Transform hull, GobyPistolConfigSO cfg, IInputStatus pilotInput, System.Action<int, bool> pistolSink)
        {
            if (Built) return true;
            if (!hull || !cfg || pilotInput == null || !KitInputDevice.IsTouchPrimary) return false;
            var view = hull.GetComponentInChildren<VesselHUDView>(true);
            _lockup = view ? view.GetComponent<AbilityLockupView>() : null;
            var row = _lockup && _lockup.IsBuilt ? _lockup.Row : null;
            if (!row) return false;

            var library = TouchControlProfileLibrarySO.Instance;
            var disc = library ? library.ButtonDisc : null;
            var rim = library ? library.ButtonRim : null;
            _buttons[Primer] = Make(Primer, row, InputEvents.Button3Action, cfg.PrimerButtonIcon, cfg.PrimerButtonLabel, disc, rim, Element.Mass);
            _buttons[Flares] = Make(Flares, row, InputEvents.Button4Action, cfg.FlaresButtonIcon, cfg.FlaresButtonLabel, disc, rim, Element.Charge);
            _buttons[Ghost] = Make(Ghost, row, InputEvents.Button3Action, cfg.GhostButtonIcon, cfg.GhostButtonLabel, disc, rim, Element.Space);
            _buttons[Snap] = Make(Snap, row, InputEvents.Button4Action, cfg.SnapButtonIcon, cfg.SnapButtonLabel, disc, rim, Element.Time);
            _buttons[Primer].Bind(pilotInput);
            _buttons[Flares].Bind(pilotInput);
            _buttons[Ghost].Bind(down => pistolSink?.Invoke(0, down));
            _buttons[Snap].Bind(down => pistolSink?.Invoke(1, down));
            for (int i = 0; i < Count; i++)
            {
                _counts[i] = i == Primer || i == Flares ? MakeCount((RectTransform)_buttons[i].transform, _lockup.CardWidth) : null;
                _shownCount[i] = _shownReady[i] = -1;
            }
            _laidSeat = -1;
            return true;
        }

        TouchButtonView Make(int i, RectTransform row, InputEvents input, Sprite icon, string label, Sprite disc, Sprite rim, Element tint)
        {
            _tints[i] = _lockup.ColourFor(tint);
            var definition = new TouchExtraButton { input = input, icon = icon, label = label, primary = true };
            var view = TouchButtonView.Create(row, definition, _tints[i], disc, rim, _lockup.CardWidth);
            view.gameObject.SetActive(false);
            return view;
        }

        static TMP_Text MakeCount(RectTransform button, float size)
        {
            var go = new GameObject(CountName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var text = go.GetComponent<TextMeshProUGUI>();
            var rt = text.rectTransform;
            rt.SetParent(button, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size * 0.5f, size * 0.3f);
            rt.anchoredPosition = new Vector2(size * 0.36f, size * 0.36f);   // the upper right of the disc
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = size * 0.24f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Shows the seat's two buttons (none while a pad is connected), with the Goby's charges as numbers and a cooling
        /// Pistol button dimmed; re-laid when the seat, the screen or the safe area changes.
        /// </summary>
        public void Paint(bool humanInSeat, string countTemplate, int primers, int flares, bool ghostReady, bool snapReady)
        {
            if (!Built) return;
            bool show = !KitInputDevice.ShowsControlHints;
            for (int i = 0; i < Count; i++)
            {
                bool mine = (i >= Ghost) == humanInSeat;
                var go = _buttons[i].gameObject;
                if (go.activeSelf != (show && mine)) go.SetActive(show && mine);
            }
            if (!show) return;

            if (humanInSeat)
            {
                PaintReady(Ghost, ghostReady);
                PaintReady(Snap, snapReady);
            }
            else
            {
                PaintCount(Primer, countTemplate, primers);
                PaintCount(Flares, countTemplate, flares);
                PaintReady(Primer, primers > 0);
                PaintReady(Flares, flares > 0);
            }

            int seat = humanInSeat ? 1 : 0;
            if (seat != _laidSeat || Screen.width != _screenWidth || Screen.height != _screenHeight || Screen.safeArea != _safeArea)
                LayOut(seat);
        }

        void PaintCount(int i, string template, int count)
        {
            var text = _counts[i];
            if (!text || count == _shownCount[i]) return;
            _shownCount[i] = count;
            text.text = TemplateText.Fill(template, "count", count.ToString());
        }

        void PaintReady(int i, bool ready)
        {
            int r = ready ? 1 : 0;
            if (r == _shownReady[i]) return;
            _shownReady[i] = r;
            var c = _tints[i];
            _buttons[i].SetTint(ready ? c : new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, c.a));
        }

        void LayOut(int seat)
        {
            _laidSeat = seat;
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _safeArea = Screen.safeArea;
            var row = _lockup ? _lockup.Row : null;
            if (!row) return;
            var canvas = row.GetComponentInParent<Canvas>();
            var root = canvas ? canvas.rootCanvas : null;
            Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;

            float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
            var layout = TouchButtonLayout.Arrange(Aspects, new Vector2(Screen.width, Screen.height), _safeArea, dpi,
                TouchControlsHud.LayoutSettings(TouchControlProfileLibrarySO.Instance));
            float scale = layout.ButtonWidthPixels * TouchControlsHud.UnitsPerPixel(row, cam) / Mathf.Max(1f, _lockup.CardWidth);
            int first = seat == 1 ? Ghost : Primer;
            for (int k = 0; k < 2 && k < layout.Centres.Count; k++)
            {
                var button = _buttons[first + k];
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(row, layout.Centres[k], cam, out var local)) continue;
                ((RectTransform)button.transform).anchoredPosition = local;
                button.SetBaseScale(scale);
            }
        }

        /// <summary>Destroys the buttons (the seat let go of the hull). Disabling one releases a held press.</summary>
        public void Dispose()
        {
            for (int i = 0; i < Count; i++)
            {
                if (_buttons[i]) Object.Destroy(_buttons[i].gameObject);
                _buttons[i] = null;
                _counts[i] = null;
            }
            _lockup = null;
            _laidSeat = -1;
        }
    }
}
