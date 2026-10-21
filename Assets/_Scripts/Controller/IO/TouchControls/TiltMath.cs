using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turning a phone's gravity reading into a stick: the pure half of <see cref="TouchTilt"/>, so the
    /// signs can be tested without a device.
    ///
    /// <para><b>Frames.</b> The reading is in the DEVICE's frame, Unity's accelerometer convention: x to
    /// the right and y up with the phone held in portrait, z out of the glass toward the face (flat on a
    /// table face up reads (0, 0, -1)). The game locks the screen to LandscapeLeft
    /// (<see cref="DeviceOrientationHandler"/>): the phone turned counter-clockwise, so the screen's up is
    /// the device's +x and the screen's right is the device's -y. LandscapeRight is the same turned
    /// half round, which flips both.</para>
    ///
    /// <para><b>Angles.</b> ROLL is the phone turned like a steering wheel, clockwise positive (a right
    /// turn). PITCH is the top edge tipped away from the face, positive (the stick pushed forward). Both
    /// are measured against a REFERENCE taken when tilt takes over, so the player's own holding angle
    /// is neutral rather than "upright".</para>
    /// </summary>
    public static class TiltMath
    {
        /// <summary>Roll and pitch, in degrees, of a gravity reading taken in the device frame.</summary>
        public static Vector2 Angles(Vector3 gravityDevice, bool landscapeRight = false)
        {
            float sign = landscapeRight ? -1f : 1f;
            float right = -gravityDevice.y * sign; // along the screen's right
            float up = gravityDevice.x * sign;     // along the screen's up
            float outOfGlass = gravityDevice.z;

            // Upright, gravity points straight down the screen: up = -1, right = 0 -> roll 0.
            // Turning the phone clockwise by t swings gravity, seen on the screen, toward the right.
            float roll = Mathf.Atan2(right, -up) * Mathf.Rad2Deg;

            // Pitch in the plane of the screen's up and the glass normal: upright 0, face up +90.
            float inPlane = Mathf.Sqrt(right * right + up * up);
            float pitch = Mathf.Atan2(-outOfGlass, inPlane) * Mathf.Rad2Deg;
            return new Vector2(roll, pitch);
        }

        /// <summary>
        /// A stick in [-1, 1] from how far the phone has turned since <paramref name="reference"/>:
        /// x = roll (right positive), y = pitch (forward positive). Inside the dead zone it reads 0, and
        /// it rescales from the dead zone's edge to <paramref name="fullScaleDegrees"/>, so leaving the
        /// zone ramps from 0 rather than jumping.
        /// </summary>
        public static Vector2 Stick(Vector2 angles, Vector2 reference, float fullScaleDegrees, float deadZoneDegrees)
        {
            return new Vector2(
                Axis(Mathf.DeltaAngle(reference.x, angles.x), fullScaleDegrees, deadZoneDegrees),
                Axis(Mathf.DeltaAngle(reference.y, angles.y), fullScaleDegrees, deadZoneDegrees));
        }

        static float Axis(float degrees, float fullScale, float deadZone)
        {
            float magnitude = Mathf.Abs(degrees);
            if (magnitude <= deadZone) return 0f;
            float span = Mathf.Max(0.001f, fullScale - deadZone);
            return Mathf.Sign(degrees) * Mathf.Clamp01((magnitude - deadZone) / span);
        }
    }
}
