using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The fleet's ONE answer to <b>which device is the player actually using?</b> — read by
    /// <see cref="InputController"/> to pick the input strategy and by
    /// <c>InputDeviceIconSetSwitcher</c> to pick the ability-chip glyphs.
    ///
    /// <para><b>It exists because those two disagreed.</b> The switcher had always detected by
    /// last meaningful ACTUATION, so a connected-but-idle pad never stole the chips from the
    /// keyboard — while <c>SelectStrategy</c> keyed on device PRESENCE (<c>Gamepad.current !=
    /// null</c>) and handed the pad every frame regardless. A player with a controller plugged in
    /// therefore watched the glyphs correctly follow their keyboard and mouse while the ship
    /// ignored both, and the only fix was to unplug the pad. Two systems answering the same
    /// question two ways is not a bug either one contains; it is a bug in there being two answers.</para>
    ///
    /// <para><b>Unambiguous acts rank above ambiguous held states.</b> A button press and a
    /// SUSTAINED mouse movement are things a player did; a stick sitting off centre is a thing a
    /// stick is doing, and a worn stick does it forever. So the order is: pad BUTTONS, then
    /// sustained mouse motion, then keys and mouse buttons, and only then pad STICKS and triggers.
    /// It deliberately polls explicit controls rather than <c>device.lastUpdateTime</c>: DualShock
    /// sensor noise updates that timestamp every frame and would pin the pad forever.</para>
    ///
    /// <para><b>Mouse MOTION counts, and leaving it out was a defect.</b> The first version took
    /// buttons and keys only, reasoning that a bumped desk must not steal the ship from a pad
    /// player. The consequence was that a desktop player with a controller merely plugged in could
    /// not fly with the mouse at all: <see cref="DetectInitial"/> hands a connected pad the ship,
    /// a mouse CLICK won it back for as long as it took the pad's resting stick to cross 0.25 —
    /// drift qualifies — and no amount of mouse movement could ever win it again, because movement
    /// was not evidence of anything. Reported as <i>"the cursor disappears, the mouse buttons work,
    /// the mouse doesn't fly the vessel"</i>, which is exactly that shape: the click engages, the
    /// pad takes it straight back, and every hand-over resets the virtual stick to centre.
    /// <see cref="MouseMotionActuation"/> keeps the desk-bump guarantee by requiring the movement
    /// to be SUSTAINED rather than merely large — a jolt is two frames, steering is not.</para>
    /// </summary>
    public static class InputDeviceActuation
    {
        /// <summary>Stick deflection that counts as "using the pad".</summary>
        public const float DefaultStickActuationThreshold = 0.25f;

        /// <summary>
        /// What a pad stick must reach to TAKE the input family from a player who is actively on
        /// the keyboard and mouse — far above <see cref="DefaultStickActuationThreshold"/>, and
        /// far above any drift.
        ///
        /// <para>The asymmetry is the whole point. Going mouse → pad worked; coming BACK did not,
        /// and the report was exact: <i>"I turned on my gamepad, which worked... then I tried to
        /// use the mouse again and I could not, even after turning off my controller."</i> A stick
        /// resting past 0.25 re-claimed the family every frame, and every claim ran
        /// <c>OnStrategyDeactivated → ResetStrategyState → stick = Vector2.zero</c>, so the mouse
        /// could never accumulate a deflection even on the frames it owned. Requiring a real push
        /// to take the ship from someone who is using it makes the handover symmetric: a pad
        /// player still takes over by steering or pressing anything, and a resting stick takes
        /// nothing from anyone.</para>
        /// </summary>
        public const float StickClaimThreshold = 0.6f;

        /// <summary>Mouse speed that counts as steering rather than settling.</summary>
        public const float DefaultMouseActuationSpeed = 120f;

        /// <summary>How long that speed must be sustained. Five frames at 60 fps — longer than a
        /// desk bump, shorter than a player can notice.</summary>
        public const float DefaultMouseActuationHold = 0.08f;

        /// <summary>
        /// The family the player was last seen using, or <see cref="InputDeviceFamily.None"/> when
        /// nothing meaningful happened this frame — in which case the caller keeps whatever it had.
        /// Sticky by construction: this only ever answers on a real actuation, so nothing thrashes
        /// between devices frame to frame.
        /// </summary>
        public static InputDeviceFamily DetectActuatedThisFrame(
            float stickActuationThreshold = DefaultStickActuationThreshold)
        {
            var motion = default(MouseMotionActuation);
            return DetectActuatedThisFrame(ref motion, 0f, InputDeviceFamily.None,
                                           stickActuationThreshold);
        }

        /// <summary>
        /// How far a stick must be pushed to count, given who currently holds the family. Taking
        /// the ship from an active keyboard-and-mouse player costs a real push
        /// (<see cref="StickClaimThreshold"/>); everything else is the ordinary threshold.
        /// </summary>
        public static float StickThresholdFor(InputDeviceFamily current, float baseThreshold)
            => current == InputDeviceFamily.KeyboardMouse
                ? Mathf.Max(baseThreshold, StickClaimThreshold)
                : baseThreshold;

        /// <summary>
        /// The family the player was last seen using, counting sustained mouse MOTION as using the
        /// mouse. <paramref name="mouseMotion"/> is the caller's rolling window - one per consumer,
        /// so two consumers cannot consume each other's evidence.
        /// </summary>
        public static InputDeviceFamily DetectActuatedThisFrame(
            ref MouseMotionActuation mouseMotion, float deltaTime, InputDeviceFamily current,
            float stickActuationThreshold = DefaultStickActuationThreshold)
        {
            var pad = Gamepad.current;

            // A pad BUTTON is unambiguous, so it outranks everything.
            if (pad != null && IsGamepadButtonActuated(pad))
                return InputDeviceFamily.Gamepad;

            var mouseDevice = Mouse.current;
            bool moving = mouseDevice != null
                       && mouseMotion.Tick(mouseDevice.delta.ReadValue(), deltaTime);
            if (moving)
                return InputDeviceFamily.KeyboardMouse;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return InputDeviceFamily.KeyboardMouse;

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame
                               || mouse.rightButton.wasPressedThisFrame
                               || mouse.middleButton.wasPressedThisFrame))
                return InputDeviceFamily.KeyboardMouse;

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                return InputDeviceFamily.Touch;

            // LAST, because a stick off centre is the one signal here that a worn device produces
            // on its own. Anything a player actually DID has already answered above.
            if (pad != null && IsGamepadAxisActuated(pad, stickActuationThreshold))
                return InputDeviceFamily.Gamepad;

            return InputDeviceFamily.None;
        }

        /// <summary>
        /// What to assume before the player has touched anything: a handheld is touch, a connected
        /// pad is the pad (so a console/couch session starts correct rather than starting on a
        /// keyboard nobody is holding), otherwise keyboard and mouse.
        /// </summary>
        public static InputDeviceFamily DetectInitial()
        {
            if (SystemInfo.deviceType == DeviceType.Handheld && Gamepad.current == null)
                return InputDeviceFamily.Touch;
            if (Gamepad.current != null)
                return InputDeviceFamily.Gamepad;
            return InputDeviceFamily.KeyboardMouse;
        }

        /// <summary>A press: unambiguous, so it outranks every other signal.</summary>
        public static bool IsGamepadButtonActuated(Gamepad pad)
        {
            if (pad == null) return false;

            return pad.buttonSouth.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame ||
                pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame ||
                pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
                pad.startButton.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame ||
                pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame ||
                pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame;
        }

        /// <summary>A stick or trigger held off centre. Ambiguous: this is what worn hardware
        /// produces with nobody touching it, which is why it is checked last.</summary>
        public static bool IsGamepadAxisActuated(Gamepad pad,
            float stickActuationThreshold = DefaultStickActuationThreshold)
        {
            if (pad == null) return false;

            float t = stickActuationThreshold;
            return pad.leftStick.ReadValue().sqrMagnitude > t * t
                || pad.rightStick.ReadValue().sqrMagnitude > t * t
                || pad.leftTrigger.ReadValue() > t
                || pad.rightTrigger.ReadValue() > t;
        }
    }

    /// <summary>
    /// A rolling window that answers "is the player STEERING with the mouse?" rather than "did the
    /// mouse move?". Movement must hold above <see cref="InputDeviceActuation.DefaultMouseActuationSpeed"/>
    /// for <see cref="InputDeviceActuation.DefaultMouseActuationHold"/> continuously, which is what
    /// keeps a knocked desk - a jolt of one or two frames, however violent - from taking the ship
    /// away from a pad player.
    ///
    /// <para>Held by the caller rather than statically, because two consumers ask this question
    /// (the strategy picker and the ability-chip glyphs) and a shared window would let whichever
    /// asked first consume the evidence.</para>
    /// </summary>
    public struct MouseMotionActuation
    {
        /// <summary>Seconds the mouse has been moving above the speed threshold, unbroken.</summary>
        public float MovingSeconds;

        public bool Tick(Vector2 pixelDelta, float deltaTime,
                         float pixelsPerSecond = InputDeviceActuation.DefaultMouseActuationSpeed,
                         float holdSeconds = InputDeviceActuation.DefaultMouseActuationHold)
        {
            if (deltaTime <= 0f) return false;

            bool fast = pixelDelta.magnitude >= pixelsPerSecond * deltaTime;
            MovingSeconds = fast ? MovingSeconds + deltaTime : 0f;
            return MovingSeconds >= holdSeconds;
        }
    }

    /// <summary>
    /// A class of input device, as far as "who is flying the ship" is concerned. Distinct from
    /// <c>InputDeviceType</c>, which names which STRATEGY is live (a keyboard-mouse family player
    /// may be on <c>Keyboard</c>, <c>MouseKeyboard</c> or <c>DualMouse</c>).
    /// </summary>
    public enum InputDeviceFamily
    {
        None = 0,
        Gamepad = 1,
        KeyboardMouse = 2,
        Touch = 3,
    }
}
