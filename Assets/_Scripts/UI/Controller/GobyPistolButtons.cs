using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Each Goby &amp; Pistol seat's own buttons on a phone (Garrett, 2026-10-21: "each seat has its own ui and controls"),
    /// round the right thumb, laid out by the fleet's <see cref="TouchButtonLayout"/> in millimetres inside the safe area -
    /// the Orca's gunner buttons (<see cref="OrcaHUDController"/>) are the pattern:
    /// <list type="bullet">
    /// <item><b>The Goby</b> (the hull; one thumb aims it while it perches): Dart (hold to charge the distance, let go to
    /// dart; dimmed while it cannot) and Lock (lit while a lock is held). They press the pilot's <see cref="IInputStatus"/>
    /// with the inputs the hull binds to <see cref="CrewButtonActionSO"/> (Button3Action, Button4Action: a pad's RT / LT and
    /// X / Y, keys Q and F), so a phone, a pad and a keyboard reach the seat by one path.</item>
    /// <item><b>The Pistol</b> (the seat; a finger on its view dives and drags to jump): Ghost (held), Snap (dimmed while
    /// cooling) and Back (at a perch, head back toward the older dens; lit while heading back). They press the seat
    /// directly, because the seat's AI flies the hull while a person flies the Pistol and an InputStatus press is dropped
    /// under autopilot.</item>
    /// </list>
    /// Only the seat the person is in shows its buttons, and none show while a pad is connected. A touch that starts on a
    /// button never flies (<see cref="FlightTouchClaims"/>). Owned by the seat (<see cref="GobyPistolDuo"/>), so the
    /// Play-mode menu that adds the crew to any hull gets them too. The buttons live under the hull HUD's ability row and
    /// die with it.
    /// </summary>
    public sealed class GobyPistolButtons
    {
        public const int Dart = 0, Lock = 1, Ghost = 2, Snap = 3, Back = 4;
        const int Count = 5;
        static readonly float[] GobyAspects = { 1f, 1f }, PistolAspects = { 1f, 1f, 1f };   // the first takes the easiest reach

        readonly TouchButtonView[] _buttons = new TouchButtonView[Count];
        readonly int[] _shown = new int[Count];
        readonly Color[] _tints = new Color[Count];
        AbilityLockupView _lockup;
        int _laidSeat = -1, _screenWidth, _screenHeight;
        Rect _safeArea;

        /// <summary>True once the buttons exist (the hull's HUD row is built and this is a touch device).</summary>
        public bool Built => _buttons[Dart];

        /// <summary>
        /// Builds the buttons under the ability row of <paramref name="hull"/>'s HUD. False (and nothing built) when the
        /// device is not touch-first or the row is not built yet; the seat tries again on a later frame.
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
            _buttons[Dart] = Make(Dart, row, InputEvents.Button3Action, cfg.DartButtonIcon, cfg.DartButtonLabel, disc, rim, Element.Time);
            _buttons[Lock] = Make(Lock, row, InputEvents.Button4Action, cfg.LockButtonIcon, cfg.LockButtonLabel, disc, rim, Element.Charge);
            _buttons[Ghost] = Make(Ghost, row, InputEvents.Button3Action, cfg.GhostButtonIcon, cfg.GhostButtonLabel, disc, rim, Element.Space);
            _buttons[Snap] = Make(Snap, row, InputEvents.Button4Action, cfg.SnapButtonIcon, cfg.SnapButtonLabel, disc, rim, Element.Charge);
            _buttons[Back] = Make(Back, row, InputEvents.Button2Action, cfg.BackButtonIcon, cfg.BackButtonLabel, disc, rim, Element.Mass);
            _buttons[Dart].Bind(pilotInput);
            _buttons[Lock].Bind(pilotInput);
            _buttons[Ghost].Bind(down => pistolSink?.Invoke(0, down));
            _buttons[Snap].Bind(down => pistolSink?.Invoke(1, down));
            _buttons[Back].Bind(down => pistolSink?.Invoke(2, down));
            for (int i = 0; i < Count; i++) _shown[i] = -1;
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

        /// <summary>
        /// Shows the seat's buttons (none while a pad is connected): the Goby's Dart dimmed while it cannot dart and its Lock
        /// bright while locked; the Pistol's Ghost and Snap dimmed while cooling and its Back bright while heading back.
        /// Re-laid when the seat, the screen or the safe area changes.
        /// </summary>
        public void Paint(bool humanInSeat, bool dartReady, bool locked, bool ghostReady, bool snapReady, bool headingBack)
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
                PaintBright(Ghost, ghostReady);
                PaintBright(Snap, snapReady);
                PaintBright(Back, headingBack);
            }
            else
            {
                PaintBright(Dart, dartReady);
                PaintBright(Lock, locked);
            }

            int seat = humanInSeat ? 1 : 0;
            if (seat != _laidSeat || Screen.width != _screenWidth || Screen.height != _screenHeight || Screen.safeArea != _safeArea)
                LayOut(seat);
        }

        void PaintBright(int i, bool bright)
        {
            int b = bright ? 1 : 0;
            if (b == _shown[i]) return;
            _shown[i] = b;
            var c = _tints[i];
            _buttons[i].SetTint(bright ? c : new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, c.a));
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

            var aspects = seat == 1 ? PistolAspects : GobyAspects;
            float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
            var layout = TouchButtonLayout.Arrange(aspects, new Vector2(Screen.width, Screen.height), _safeArea, dpi,
                TouchControlsHud.LayoutSettings(TouchControlProfileLibrarySO.Instance));
            float scale = layout.ButtonWidthPixels * TouchControlsHud.UnitsPerPixel(row, cam) / Mathf.Max(1f, _lockup.CardWidth);
            int first = seat == 1 ? Ghost : Dart;
            for (int k = 0; k < aspects.Length && k < layout.Centres.Count; k++)
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
            }
            _lockup = null;
            _laidSeat = -1;
        }
    }
}
