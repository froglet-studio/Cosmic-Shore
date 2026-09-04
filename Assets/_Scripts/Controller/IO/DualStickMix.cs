using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Dual-stick mix shared by keyboard (and matching <see cref="GamepadInputStrategy.Reparameterize"/>).
    /// XSum = yaw, YSum = pitch, XDiff = speed, YDiff = roll.
    /// InvertY / InvertThrottle apply after the mix.
    /// </summary>
    public readonly struct DualStickMixResult
    {
        public readonly float XSum;
        public readonly float YSum;
        public readonly float XDiff;
        public readonly float YDiff;
        public readonly Vector2 EasedLeft;
        public readonly Vector2 EasedRight;

        public DualStickMixResult(float xSum, float ySum, float xDiff, float yDiff,
            Vector2 easedLeft, Vector2 easedRight)
        {
            XSum = xSum;
            YSum = ySum;
            XDiff = xDiff;
            YDiff = yDiff;
            EasedLeft = easedLeft;
            EasedRight = easedRight;
        }
    }

    public static class DualStickMix
    {
        const float PiOverFour = 0.785f;

        public static float Ease(float input)
        {
            return input < 0
                ? (Mathf.Cos(input * PiOverFour) - 1)
                : -(Mathf.Cos(input * PiOverFour) - 1);
        }

        public static DualStickMixResult Mix(Vector2 left, Vector2 right,
            bool invertY = false, bool invertThrottle = false)
        {
            float xSum = Ease(right.x + left.x);
            float ySum = -Ease(right.y + left.y);
            float xDiff = (right.x - left.x + 2f) / 4f;
            float yDiff = Ease(right.y - left.y);

            if (invertY)
            {
                ySum *= -1f;
                yDiff *= -1f;
            }

            if (invertThrottle)
                xDiff = 1f - xDiff;

            return new DualStickMixResult(
                xSum, ySum, xDiff, yDiff,
                new Vector2(Ease(2f * left.x), Ease(2f * left.y)),
                new Vector2(Ease(2f * right.x), Ease(2f * right.y)));
        }
    }
}
