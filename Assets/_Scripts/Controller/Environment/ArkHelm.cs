using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How the Ark is sailed (<see cref="ArkHelm"/>): its way (acceleration and braking instead of a speed that snaps to
    /// the profile), its steering (a bow that swings with an eased turn rate and a gentle weave instead of a ruler line to
    /// the next core), its attitude (banking into turns, the nose lifting under acceleration and dipping when it brakes),
    /// its swell (a slow heave, roll and pitch that never stops, even at anchor) and its flinch (a hit plate rocks the hull
    /// and jinks the bow away from the side that was struck). Authored on the Ark's model
    /// (<see cref="CosmicShore.ScriptableObjects.ArkModelDefinitionSO"/>); an Ark with no model sails <see cref="Default"/>.
    /// </summary>
    [Serializable]
    public struct ArkHelmSettings
    {
        [Header("Way - how the speed changes")]
        [Min(0.1f), Tooltip("Most the Ark speeds up, u/s per second (lifting anchor, leaving a cell).")]
        public float Acceleration;
        [Min(0.1f), Tooltip("Most the Ark slows, u/s per second (braking onto a membrane, dropping anchor).")]
        public float Braking;
        [Min(0.05f), Tooltip("Seconds the Ark takes to close most of a gap between its speed and the profile's.")]
        public float SpeedResponse;
        [Min(0.1f), Tooltip("How fast the acceleration itself changes, u/s per second per second: low values ease every " +
                            "surge in and out instead of lurching.")]
        public float Jerk;

        [Header("Steering - how the bow swings")]
        [Min(1f), Tooltip("Fastest the bow turns, degrees per second.")]
        public float MaxTurnRate;
        [Min(0.05f), Tooltip("Turn rate asked per degree off course (1/s): higher swings onto a new heading sooner.")]
        public float TurnGain;
        [Min(1f), Tooltip("How fast the turn rate builds and eases, degrees per second per second: the turn is a curve, " +
                          "not a kink.")]
        public float TurnAcceleration;
        [Min(0f), Tooltip("Inside this range of the destination the Ark steers straight at it, so it always arrives.")]
        public float CaptureRange;

        [Header("Weave - a course that is sailed, not ruled")]
        [Range(0f, 30f), Tooltip("Degrees the bow wanders left and right of the straight course.")]
        public float WeaveYaw;
        [Range(0f, 15f), Tooltip("Degrees the bow wanders up and down.")]
        public float WeavePitch;
        [Min(50f), Tooltip("Units of travel per full weave. The weave follows distance sailed, not time, so every machine " +
                           "in a party draws the same course.")]
        public float WeaveWavelength;
        [Min(1f), Tooltip("Over this range before the destination the weave fades out, so the Ark lines up on the core.")]
        public float WeaveFadeRange;

        [Header("Attitude - how the hull leans")]
        [Range(0f, 5f), Tooltip("Degrees of bank per degree-per-second of turn.")]
        public float BankPerTurnRate;
        [Range(0f, 45f), Tooltip("Most the hull banks, degrees.")]
        public float MaxBank;
        [Range(0f, 5f), Tooltip("Degrees the nose lifts per u/s per second of acceleration (and dips when braking).")]
        public float PitchPerAcceleration;
        [Range(0f, 20f), Tooltip("Most the nose lifts or dips from acceleration, degrees.")]
        public float MaxPitch;
        [Min(0.01f), Tooltip("Seconds the bank and pitch take to follow (exponential).")]
        public float AttitudeResponse;

        [Header("Swell - the hull is alive even at anchor")]
        [Range(0f, 5f), Tooltip("Units the hull heaves up and down.")]
        public float SwellHeave;
        [Range(0f, 6f), Tooltip("Degrees the hull rolls on the swell.")]
        public float SwellRoll;
        [Range(0f, 6f), Tooltip("Degrees the hull pitches on the swell.")]
        public float SwellPitch;
        [Min(1f), Tooltip("Seconds per swell (the roll and pitch run on their own slightly different periods).")]
        public float SwellPeriod;

        [Header("Flinch - a struck hull rocks and jinks")]
        [Range(0f, 20f), Tooltip("Degrees the hull rocks away from a struck plate.")]
        public float FlinchRoll;
        [Range(0f, 30f), Tooltip("Degrees the bow jinks away from the side that was struck.")]
        public float FlinchJink;
        [Min(0f), Tooltip("Seconds before another hit can make the hull flinch again (a swarm gnawing many plates is one flinch).")]
        public float FlinchCooldown;
        [Min(0.05f), Tooltip("How fast the rock oscillates, per second.")]
        public float FlinchFrequency;
        [Range(0.05f, 1f), Tooltip("Damping ratio of the rock: low rings longer, 1 settles without a swing.")]
        public float FlinchDamping;

        public static ArkHelmSettings Default => new()
        {
            Acceleration = 5f, Braking = 8f, SpeedResponse = 1.4f, Jerk = 6f,
            MaxTurnRate = 22f, TurnGain = 1.2f, TurnAcceleration = 18f, CaptureRange = 40f,
            WeaveYaw = 9f, WeavePitch = 3.5f, WeaveWavelength = 1100f, WeaveFadeRange = 450f,
            BankPerTurnRate = 2.2f, MaxBank = 18f, PitchPerAcceleration = 0.8f, MaxPitch = 5f, AttitudeResponse = 0.6f,
            SwellHeave = 0.9f, SwellRoll = 1.6f, SwellPitch = 0.8f, SwellPeriod = 9f,
            FlinchRoll = 6f, FlinchJink = 8f, FlinchCooldown = 0.9f, FlinchFrequency = 1.1f, FlinchDamping = 0.3f,
        };

        /// <summary>The settings with every rate, time and range usable (a zeroed asset sails <see cref="Default"/>).</summary>
        public ArkHelmSettings Sanitized()
        {
            if (MaxTurnRate <= 0f && Acceleration <= 0f && SpeedResponse <= 0f) return Default;
            var s = this;
            s.Acceleration = Mathf.Max(0.1f, s.Acceleration);
            s.Braking = Mathf.Max(0.1f, s.Braking);
            s.SpeedResponse = Mathf.Max(0.05f, s.SpeedResponse);
            s.Jerk = Mathf.Max(0.1f, s.Jerk);
            s.MaxTurnRate = Mathf.Max(1f, s.MaxTurnRate);
            s.TurnGain = Mathf.Max(0.05f, s.TurnGain);
            s.TurnAcceleration = Mathf.Max(1f, s.TurnAcceleration);
            s.CaptureRange = Mathf.Max(0f, s.CaptureRange);
            s.WeaveWavelength = Mathf.Max(50f, s.WeaveWavelength);
            s.WeaveFadeRange = Mathf.Max(1f, s.WeaveFadeRange);
            s.AttitudeResponse = Mathf.Max(0.01f, s.AttitudeResponse);
            s.SwellPeriod = Mathf.Max(1f, s.SwellPeriod);
            s.FlinchFrequency = Mathf.Max(0.05f, s.FlinchFrequency);
            s.FlinchDamping = Mathf.Clamp(s.FlinchDamping, 0.05f, 1f);
            s.FlinchCooldown = Mathf.Max(0f, s.FlinchCooldown);
            return s;
        }
    }

    /// <summary>
    /// The Ark's helm (Shombith, 2026-11-24: "the Ark's movement is very uncanny and linear, there is no excitement,
    /// maneuverability, no animation"). Pure state and arithmetic, no Unity objects, so tests drive it directly.
    ///
    /// The Ark keeps two poses: the COURSE pose, which navigates, is synced across a party and is what every role reads
    /// as <see cref="Ark.Forward"/>, and the drawn pose, which is the course pose with the helm's attitude laid on top
    /// (bank, pitch, swell, flinch). The attitude never steers: it is the look of a ship being sailed.
    /// </summary>
    public sealed class ArkHelm
    {
        ArkHelmSettings _s;
        float _acceleration;          // u/s per second, this frame
        float _turnRate;              // degrees per second the bow is swinging now
        float _travelled;             // units of course sailed: the weave's phase
        float _bank, _pitch;          // smoothed attitude, degrees
        float _clock;                 // seconds of swell
        float _flinchRoll, _flinchRollVelocity, _flinchPitch, _flinchPitchVelocity;
        float _jink;                  // degrees of yaw the last flinch added to the course, easing out
        float _nextFlinchAt;

        public ArkHelm(in ArkHelmSettings settings) => Use(settings);

        public void Use(in ArkHelmSettings settings) => _s = settings.Sanitized();

        public ArkHelmSettings Settings => _s;
        public float Acceleration => _acceleration;
        public float TurnRate => _turnRate;
        public float Travelled => _travelled;

        /// <summary>
        /// The speed after <paramref name="dt"/>: toward <paramref name="target"/> under the acceleration and braking
        /// limits, the acceleration itself eased by the jerk limit. Never overshoots the target.
        /// </summary>
        public float StepSpeed(float speed, float target, float dt)
        {
            if (dt <= 0f) return speed;
            float want = Mathf.Clamp((target - speed) / _s.SpeedResponse, -_s.Braking, _s.Acceleration);
            _acceleration = Mathf.MoveTowards(_acceleration, want, _s.Jerk * dt);
            float next = Mathf.Max(0f, speed + _acceleration * dt);
            if ((speed <= target && next > target) || (speed >= target && next < target))
            {
                next = target;
                _acceleration = 0f;
            }
            return next;
        }

        /// <summary>
        /// The bow's new heading after <paramref name="dt"/>: it swings toward the destination's bearing
        /// <paramref name="toDestination"/> (unit), bent by the weave (faded out over the last stretch,
        /// <paramref name="distance"/> away) and by any jink, with a turn rate that builds and eases.
        /// <paramref name="step"/> is the distance sailed this frame (the weave's phase advances by it).
        /// </summary>
        public Vector3 Steer(Vector3 heading, Vector3 toDestination, float distance, float step, float dt)
        {
            _travelled += step;
            if (heading.sqrMagnitude < 1e-6f) heading = toDestination;
            var desired = WeavedBearing(toDestination, distance);

            float angle = Vector3.Angle(heading, desired);
            var axis = Vector3.Cross(heading, desired);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.up;
            float wantRate = Mathf.Min(_s.MaxTurnRate, angle * _s.TurnGain);
            _turnRate = Mathf.MoveTowards(_turnRate, wantRate, _s.TurnAcceleration * dt);
            float turn = Mathf.Min(_turnRate * dt, angle);
            var next = Quaternion.AngleAxis(turn, axis.normalized) * heading;
            return next.normalized;
        }

        /// <summary>The bearing the weave and the jink ask for: the destination's, bent by up to the weave's angles.</summary>
        public Vector3 WeavedBearing(Vector3 toDestination, float distance)
        {
            float fade = Smooth((distance - _s.CaptureRange) / _s.WeaveFadeRange);
            float phase = _travelled / _s.WeaveWavelength * (Mathf.PI * 2f);
            // Two harmonics at an irrational ratio: the weave never repeats a beat a player can count.
            float yaw = _s.WeaveYaw * fade * (Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 0.618f + 1.3f) * 0.3f) + _jink * fade;
            float pitch = _s.WeavePitch * fade * Mathf.Sin(phase * 0.786f + 0.6f);
            if (Mathf.Abs(yaw) < 1e-4f && Mathf.Abs(pitch) < 1e-4f) return toDestination;

            var right = Vector3.Cross(Vector3.up, toDestination);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();
            var up = Vector3.Cross(toDestination, right);
            return (Quaternion.AngleAxis(yaw, up) * Quaternion.AngleAxis(pitch, right) * toDestination).normalized;
        }

        /// <summary>
        /// The attitude laid on the course pose this frame: <paramref name="rotation"/> in the course's own frame and
        /// <paramref name="heave"/> along the course's up. <paramref name="yawRate"/> is how fast the course is turning
        /// (degrees per second, + to the right); <paramref name="sailing"/> false (a fallen hull) settles the lean and
        /// the flinch but keeps the swell.
        /// </summary>
        public void Attitude(float yawRate, bool sailing, float dt, out Quaternion rotation, out float heave)
        {
            _clock += dt;
            float follow = dt > 0f ? 1f - Mathf.Exp(-dt / _s.AttitudeResponse) : 0f;
            float bankWant = sailing ? Mathf.Clamp(-yawRate * _s.BankPerTurnRate, -_s.MaxBank, _s.MaxBank) : 0f;
            float pitchWant = sailing ? Mathf.Clamp(-_acceleration * _s.PitchPerAcceleration, -_s.MaxPitch, _s.MaxPitch) : 0f;
            _bank += (bankWant - _bank) * follow;
            _pitch += (pitchWant - _pitch) * follow;

            _jink = Mathf.MoveTowards(_jink, 0f, _s.FlinchJink * dt);   // a jink eases out over about a second
            StepSpring(ref _flinchRoll, ref _flinchRollVelocity, dt);
            StepSpring(ref _flinchPitch, ref _flinchPitchVelocity, dt);

            float w = Mathf.PI * 2f / _s.SwellPeriod;
            heave = _s.SwellHeave * (Mathf.Sin(_clock * w) * 0.8f + Mathf.Sin(_clock * w * 2.17f + 0.9f) * 0.2f);
            float swellRoll = _s.SwellRoll * Mathf.Sin(_clock * w * 0.83f + 1.7f);
            float swellPitch = _s.SwellPitch * Mathf.Sin(_clock * w * 1.13f + 0.4f);

            // Unity: +x tips the nose down, +z rolls the right side up. A bank to the right puts the right side down.
            rotation = Quaternion.Euler(_pitch + swellPitch + _flinchPitch, 0f, _bank + swellRoll + _flinchRoll);
        }

        /// <summary>
        /// A hull plate at <paramref name="arkLocal"/> (the Ark's own space) was struck: the hull rocks away from it and
        /// the bow jinks away from that side. True when it flinched (not within the cooldown of the last flinch).
        /// </summary>
        public bool Flinch(Vector3 arkLocal)
        {
            if (_clock < _nextFlinchAt) return false;
            _nextFlinchAt = _clock + _s.FlinchCooldown;
            var side = arkLocal.sqrMagnitude > 1e-6f ? arkLocal.normalized : Vector3.up;
            float kick = _s.FlinchRoll * Mathf.PI * 2f * _s.FlinchFrequency / PeakShare(_s.FlinchDamping);   // first swing = FlinchRoll
            // Struck on the right (x > 0): the right side is pushed up and away (+z), the bow jinks left (-yaw).
            _flinchRollVelocity += kick * side.x;
            // Struck on top (y > 0): the nose is pushed down at the bow, up at the stern.
            _flinchPitchVelocity += kick * 0.6f * side.y * Mathf.Sign(side.z == 0f ? 1f : side.z);
            _jink = Mathf.Clamp(_jink - _s.FlinchJink * Mathf.Sign(side.x == 0f ? 1f : side.x), -_s.FlinchJink, _s.FlinchJink);
            return true;
        }

        /// <summary>The flinch's rock right now (degrees of roll), for a test.</summary>
        public float FlinchRollAngle => _flinchRoll;

        /// <summary>The jink still bending the course (degrees of yaw).</summary>
        public float Jink => _jink;

        void StepSpring(ref float x, ref float v, float dt)
        {
            if (dt <= 0f) return;
            float w = Mathf.PI * 2f * _s.FlinchFrequency;
            // Semi-implicit Euler in substeps: stable at any frame rate a phone drops to.
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.02f), 1, 8);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += (-w * w * x - 2f * _s.FlinchDamping * w * v) * h;
                x += v * h;
            }
        }

        /// <summary>The share of v0/w a damped spring's first swing reaches (1 undamped, about 0.67 at a damping of 0.3).</summary>
        static float PeakShare(float damping)
        {
            float z = Mathf.Clamp(damping, 0.05f, 0.999f);
            float r = Mathf.Sqrt(1f - z * z);
            return Mathf.Exp(-z / r * Mathf.Atan2(r, z));
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
