using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Everything the Skim Race pilot is allowed to know on one decision tick. Built by
    /// <see cref="SkimRacePilot"/> from the vessel's own status, the authoritative crystal registry
    /// and the visible track; read by <see cref="SkimRaceDriver"/>.
    ///
    /// The world-space fields are what the controller steers with. <see cref="WriteFeatures"/>
    /// packs the same state into a fixed-length, normalised vector (<see cref="FeatureCount"/>
    /// entries, schema <see cref="SchemaVersion"/>) for learned policies and for the schema test
    /// that keeps the controller and any tooling reading the same layout. Every field is NaN/inf
    /// sanitised on the way in, so a destroyed target or a degenerate frame can never leak a NaN
    /// into the stick.
    /// </summary>
    public struct SkimRaceObservation
    {
        public const int SchemaVersion = 1;
        public const int FeatureCount = 30;

        /// <summary>Normalisation constants. Squirrel cruise is 60 u/s and its boost caps at 5x.</summary>
        public const float SpeedScale = 300f;
        public const float DistanceScale = 1000f;
        public const float TimeScale = 70f;

        // ── Vessel ──────────────────────────────────────────────
        public Vector3 Position;
        public Vector3 Forward;
        public Vector3 Right;
        public Vector3 Up;
        /// <summary>Forward of the orientation the stick has COMMANDED (the hull lags it).</summary>
        public Vector3 CommandedForward;
        /// <summary>The hull's rotation and the commanded rotation (planner rollouts start from these).</summary>
        public Quaternion Rotation;
        public Quaternion CommandedRotation;
        public Vector3 Velocity;
        /// <summary>World angular velocity of the hull, rad/s (finite-differenced).</summary>
        public Vector3 AngularVelocity;
        public float Speed;
        public float BoostMultiplier;
        public float MaxBoost;
        /// <summary>Max turn rate at full stick, deg/s (live: drift raises it).</summary>
        public float TurnRateDegrees;
        /// <summary>Per-second follow rate of hull rotation and cruise speed.</summary>
        public float FollowRate;
        /// <summary>Cruise speed at full throttle with no boost.</summary>
        public float ThrottleScaler;
        public bool IsDrifting;

        // ── Target (authoritative active crystal for this vessel's domain) ──
        public bool HasTarget;
        public Vector3 TargetPosition;
        public Vector3 ToTarget;
        public float TargetDistance;
        /// <summary>Unit direction to the target in hull-local space.</summary>
        public Vector3 TargetLocalDirection;
        /// <summary>dot(Forward, direction to target).</summary>
        public float TargetAlignment;
        public int TargetId;
        /// <summary>World radius of the crystal's pickup collider (0 = unknown).</summary>
        public float TargetRadius;

        // ── Course ──────────────────────────────────────────────
        public bool HasCourse;
        public float CourseProgress;
        public float CourseLength;
        public Vector3 CourseTangent;
        public float CourseDistance;
        /// <summary>Arc distance along the course from the vessel to the target (forward only).</summary>
        public float TargetAheadOnCourse;

        // ── Race ────────────────────────────────────────────────
        public float RaceTime;
        public float TimeSinceProgress;
        public float TimeSinceCollection;
        public int Collected;
        public int Remaining;

        /// <summary>Replace any non-finite component so the controller can never output NaN.</summary>
        public void Sanitize()
        {
            Position = Safe(Position);
            Forward = SafeDir(Forward, Vector3.forward);
            Right = SafeDir(Right, Vector3.right);
            Up = SafeDir(Up, Vector3.up);
            CommandedForward = SafeDir(CommandedForward, Forward);
            if (!IsFinite(Rotation) || (Rotation.x == 0f && Rotation.y == 0f && Rotation.z == 0f && Rotation.w == 0f))
                Rotation = Quaternion.LookRotation(Forward, Up);
            if (!IsFinite(CommandedRotation) || (CommandedRotation.x == 0f && CommandedRotation.y == 0f && CommandedRotation.z == 0f && CommandedRotation.w == 0f))
                CommandedRotation = Quaternion.LookRotation(CommandedForward, Up);
            Velocity = Safe(Velocity);
            AngularVelocity = Safe(AngularVelocity);
            Speed = Safe(Speed);
            BoostMultiplier = Safe(BoostMultiplier, 1f);
            MaxBoost = Mathf.Max(1f, Safe(MaxBoost, 5f));
            TurnRateDegrees = Mathf.Max(1f, Safe(TurnRateDegrees, 120f));
            FollowRate = Mathf.Max(0.01f, Safe(FollowRate, 1.5f));
            ThrottleScaler = Mathf.Max(1f, Safe(ThrottleScaler, 60f));
            TargetPosition = Safe(TargetPosition);
            ToTarget = Safe(ToTarget);
            TargetDistance = Safe(TargetDistance);
            TargetRadius = Mathf.Max(0f, Safe(TargetRadius));
            TargetLocalDirection = Safe(TargetLocalDirection);
            TargetAlignment = Mathf.Clamp(Safe(TargetAlignment), -1f, 1f);
            CourseProgress = Safe(CourseProgress);
            CourseLength = Safe(CourseLength);
            CourseTangent = SafeDir(CourseTangent, Forward);
            CourseDistance = Safe(CourseDistance);
            TargetAheadOnCourse = Safe(TargetAheadOnCourse);
            RaceTime = Safe(RaceTime);
            TimeSinceProgress = Safe(TimeSinceProgress);
            TimeSinceCollection = Safe(TimeSinceCollection);
            if (!HasTarget)
            {
                TargetPosition = ToTarget = TargetLocalDirection = Vector3.zero;
                TargetDistance = TargetAlignment = TargetAheadOnCourse = TargetRadius = 0f;
            }
        }

        /// <summary>
        /// Writes the normalised feature vector. Layout (schema 1):
        /// [0-2] velocity (hull-local) / SpeedScale, [3] speed, [4] boost / max,
        /// [5-7] angular velocity (hull-local) / pi, [8-10] commanded forward (hull-local),
        /// [11] has target, [12-14] target direction (hull-local), [15] target distance,
        /// [16] alignment, [17] has course, [18] course progress / length,
        /// [19-21] course tangent (hull-local), [22] distance from course,
        /// [23] target ahead on course, [24] race time, [25] time since progress,
        /// [26] time since collection, [27] collected fraction, [28] remaining fraction,
        /// [29] drifting.
        /// Every entry is clamped to [-1, 1] (or [0, 1] for flags and fractions).
        /// </summary>
        public void WriteFeatures(float[] f)
        {
            if (f == null || f.Length < FeatureCount)
                throw new System.ArgumentException($"feature buffer must hold {FeatureCount} floats");

            Vector3 v = ToLocal(Velocity) / SpeedScale;
            f[0] = C(v.x); f[1] = C(v.y); f[2] = C(v.z);
            f[3] = C01(Speed / SpeedScale);
            f[4] = C01(BoostMultiplier / Mathf.Max(1f, MaxBoost));
            Vector3 w = ToLocal(AngularVelocity) / Mathf.PI;
            f[5] = C(w.x); f[6] = C(w.y); f[7] = C(w.z);
            Vector3 cf = ToLocal(CommandedForward);
            f[8] = C(cf.x); f[9] = C(cf.y); f[10] = C(cf.z);
            f[11] = HasTarget ? 1f : 0f;
            f[12] = C(TargetLocalDirection.x); f[13] = C(TargetLocalDirection.y); f[14] = C(TargetLocalDirection.z);
            f[15] = C01(TargetDistance / DistanceScale);
            f[16] = C(TargetAlignment);
            f[17] = HasCourse ? 1f : 0f;
            f[18] = HasCourse && CourseLength > 0f ? C01(CourseProgress / CourseLength) : 0f;
            Vector3 ct = ToLocal(CourseTangent);
            f[19] = C(ct.x); f[20] = C(ct.y); f[21] = C(ct.z);
            f[22] = C01(CourseDistance / DistanceScale);
            f[23] = C01(TargetAheadOnCourse / DistanceScale);
            f[24] = C01(RaceTime / TimeScale);
            f[25] = C01(TimeSinceProgress / 10f);
            f[26] = C01(TimeSinceCollection / 10f);
            int total = Mathf.Max(1, Collected + Remaining);
            f[27] = C01((float)Collected / total);
            f[28] = C01((float)Remaining / total);
            f[29] = IsDrifting ? 1f : 0f;

            for (int i = 0; i < FeatureCount; i++)
                if (float.IsNaN(f[i]) || float.IsInfinity(f[i])) f[i] = 0f;
        }

        Vector3 ToLocal(Vector3 world) =>
            new(Vector3.Dot(world, Right), Vector3.Dot(world, Up), Vector3.Dot(world, Forward));

        static bool IsFinite(Quaternion q) =>
            !(float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w)
              || float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z) || float.IsInfinity(q.w));

        static float C(float x) => Mathf.Clamp(x, -1f, 1f);
        static float C01(float x) => Mathf.Clamp01(x);

        static float Safe(float x, float fallback = 0f) =>
            float.IsNaN(x) || float.IsInfinity(x) ? fallback : x;

        static Vector3 Safe(Vector3 v) =>
            new(Safe(v.x), Safe(v.y), Safe(v.z));

        static Vector3 SafeDir(Vector3 v, Vector3 fallback)
        {
            v = Safe(v);
            return v.sqrMagnitude > 1e-8f ? v.normalized : fallback;
        }
    }

    /// <summary>
    /// The pilot's output: exactly the controls a human Squirrel pilot has. Yaw/pitch/roll and
    /// throttle go to the vessel's <c>IInputStatus</c> sticks the same way every dual-stick input
    /// strategy writes them; <see cref="Drift"/> is pressed through the hull's own bound drift
    /// control (<c>PerformShipControllerActions</c>). Nothing here writes a transform, a speed,
    /// a course, a score or a crystal.
    /// </summary>
    public struct SkimRaceAction
    {
        public float Yaw;      // XSum, [-1, 1]
        public float Pitch;    // YSum, [-1, 1] (+ = nose down)
        public float Roll;     // YDiff, [-1, 1]
        public float Throttle; // XDiff, [0, 1]
        public bool Drift;
        /// <summary>Press the hull's Boost Ring ability this tick (a single press).</summary>
        public bool Ring;

        public static SkimRaceAction Neutral => default;

        /// <summary>Clamp every channel into its legal range and drop non-finite values.</summary>
        public SkimRaceAction Clamped()
        {
            return new SkimRaceAction
            {
                Yaw = Clamp(Yaw, -1f, 1f),
                Pitch = Clamp(Pitch, -1f, 1f),
                Roll = Clamp(Roll, -1f, 1f),
                Throttle = Clamp(Throttle, 0f, 1f),
                Drift = Drift,
                Ring = Ring,
            };
        }

        static float Clamp(float x, float lo, float hi) =>
            float.IsNaN(x) || float.IsInfinity(x) ? 0f : Mathf.Clamp(x, lo, hi);
    }
}
