using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Put me in the next seat." The one swap gesture (<see cref="CrewStations"/>): <b>D-pad left / right</b> on a pad,
    /// <b>1 / 2</b> on the keyboard, a <b>two-finger swipe</b> left / right on a touch screen (Garrett, 2026-10-23). Left
    /// steps to the PREVIOUS station, right to the NEXT, round a ring every person on the domain sees in the same order:
    /// the seats of the hull you are on, then the next hull's.
    ///
    /// <para>One predicate, in one place, for the same reason <see cref="RearViewGesture"/> is. It is read here rather
    /// than routed through <c>InputEvents</c> on purpose: it moves the PERSON between seats and hulls, it is not an
    /// ability, and putting it in a vessel's ability map would make it something a hull could fail to author. Answered
    /// once per frame and cached, so every reader in a frame sees the same press (the swipe tracks touches across
    /// frames and fires once).</para>
    ///
    /// <para><b>Why these controls.</b> The D-pad is unread in flight - no strategy binds it and no vessel ability map
    /// names it - which is what makes it a free shelf. On the keyboard the flight scheme eats WASD, P ; L ', QWER,
    /// Space, both Shifts and C; the number row is the shelf nothing flies with, and 1 / 2 sit where the left hand
    /// already is. On a phone the two thumbs fly, so the swipe is two fingers of ONE hand: both land together, close
    /// to each other, and travel the same way quickly (<see cref="TwoFingerSwipe"/>); two flying thumbs land apart in
    /// time and space and never read as one.</para>
    /// </summary>
    public static class PilotSwapGesture
    {
        static int _frame = -1, _direction;

        /// <summary>-1 (previous station), +1 (next), or 0 when the gesture did not fire this frame.</summary>
        public static int DirectionThisFrame()
        {
            if (Time.frameCount == _frame) return _direction;
            _frame = Time.frameCount;
            _direction = Read();
            return _direction;
        }

        static int Read()
        {
            // polled every frame so the swipe sees each touch begin, even on a frame a key or the pad fires
            int swipe = TwoFingerSwipe.Poll();

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) return -1;
                if (keyboard.digit2Key.wasPressedThisFrame) return +1;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) return -1;
                if (pad.dpad.right.wasPressedThisFrame) return +1;
            }
            return swipe;
        }
    }

    /// <summary>
    /// Two fingers of one hand swiped sideways across a touch screen: -1 left, +1 right, once per swipe. Both fingers
    /// must land within <see cref="LandWindowSeconds"/> of each other, closer than <see cref="MaxApartOfWidth"/> of the
    /// screen's width, on no control that owns a touch (<see cref="FlightTouchClaims"/>), and travel the same way at
    /// least <see cref="TravelOfWidth"/> of the width, mostly sideways, within <see cref="MaxSeconds"/> of landing.
    /// </summary>
    public static class TwoFingerSwipe
    {
        public const float LandWindowSeconds = 0.3f, MaxSeconds = 0.6f, MaxApartOfWidth = 0.35f, TravelOfWidth = 0.08f;

        static int _a = int.MinValue, _b = int.MinValue;
        static Vector2 _fromA, _fromB;
        static double _landedAt;
        static bool _spent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _a = _b = int.MinValue;
            _spent = false;
        }

        public static int Poll()
        {
            var ts = Touchscreen.current;
            if (ts == null) return 0;

            TouchControl first = null, second = null;
            int pressed = 0;
            var touches = ts.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (!t.press.isPressed) continue;
                pressed++;
                if (first == null) first = t;
                else if (second == null) second = t;
            }
            if (pressed != 2)
            {
                _a = _b = int.MinValue;
                return 0;
            }

            int a = first.touchId.ReadValue(), b = second.touchId.ReadValue();
            if (a != _a || b != _b)
            {
                // a new pair of fingers: is it one hand's two fingers?
                _a = a; _b = b;
                _fromA = first.startPosition.ReadValue();
                _fromB = second.startPosition.ReadValue();
                double startA = first.startTime.ReadValue(), startB = second.startTime.ReadValue();
                _landedAt = System.Math.Max(startA, startB);
                _spent = System.Math.Abs(startA - startB) > LandWindowSeconds
                         || Vector2.Distance(_fromA, _fromB) > MaxApartOfWidth * Screen.width
                         || FlightTouchClaims.Claims(_fromA) || FlightTouchClaims.Claims(_fromB);
            }
            if (_spent) return 0;
            if (InputState.currentTime - _landedAt > MaxSeconds) { _spent = true; return 0; }

            var dA = first.position.ReadValue() - _fromA;
            var dB = second.position.ReadValue() - _fromB;
            if (dA.x * dB.x <= 0f) return 0;   // the two must travel the same way
            float dx = 0.5f * (dA.x + dB.x), dy = 0.5f * (dA.y + dB.y);
            if (Mathf.Abs(dx) < TravelOfWidth * Screen.width || Mathf.Abs(dx) < 1.5f * Mathf.Abs(dy)) return 0;
            _spent = true;
            return dx > 0f ? +1 : -1;
        }
    }
}
