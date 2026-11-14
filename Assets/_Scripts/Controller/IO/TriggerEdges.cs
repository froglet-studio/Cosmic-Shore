using System;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pad's two-trigger rule, in one place: which <see cref="InputEvents"/> a change in the two
    /// triggers raises, in the order the pad has always raised them. The gamepad and the keyboard read
    /// their triggers through it, and so does touch with no thumb on the glass (Garrett, 2026-11-14:
    /// "both thumbs off should read as both triggers pulled and switch to gyro controls"), so a phone's
    /// both-thumbs-off is exactly a pad with both triggers held, not a touch-only imitation of one.
    /// Pure, so a test can read the events without a scene or an event asset.
    /// </summary>
    public static class TriggerEdges
    {
        /// <summary>A trigger above this reads as pulled.</summary>
        public const float Deadzone = 0.05f;

        /// <summary>Raises every edge the move from (<paramref name="wasLeft"/>, <paramref name="wasRight"/>)
        /// to (<paramref name="left"/>, <paramref name="right"/>) makes: <c>raise(event, true)</c> for a
        /// press, <c>raise(event, false)</c> for a release.</summary>
        public static void Dispatch(bool wasLeft, bool wasRight, bool left, bool right, Action<InputEvents, bool> raise)
        {
            bool leftJustPressed = left && !wasLeft;
            bool leftJustReleased = !left && wasLeft;
            bool rightJustPressed = right && !wasRight;
            bool rightJustReleased = !right && wasRight;

            if (leftJustPressed) raise(InputEvents.LeftStickAction, true);
            if (leftJustReleased) raise(InputEvents.LeftStickAction, false);
            if (rightJustPressed) raise(InputEvents.RightStickAction, true);
            if (rightJustReleased) raise(InputEvents.RightStickAction, false);

            if ((leftJustReleased && rightJustReleased)
                || (leftJustReleased && right)
                || (rightJustReleased && left))
                raise(InputEvents.BothSticksAction, false);

            if ((leftJustReleased && !right) || (rightJustPressed && left))
                raise(InputEvents.OnlyLeftStickAction, false);
            if ((rightJustReleased && !left) || (leftJustPressed && right))
                raise(InputEvents.OnlyRightStickAction, false);

            if ((leftJustPressed && !right) || (rightJustReleased && left))
                raise(InputEvents.OnlyLeftStickAction, true);
            if ((rightJustPressed && !left) || (leftJustReleased && right))
                raise(InputEvents.OnlyRightStickAction, true);

            if ((leftJustPressed && rightJustPressed)
                || (leftJustPressed && right)
                || (rightJustPressed && left))
                raise(InputEvents.BothSticksAction, true);
        }
    }
}
