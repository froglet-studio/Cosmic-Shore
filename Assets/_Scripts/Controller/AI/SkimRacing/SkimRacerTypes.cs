using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One prism of a ribbon a skim racer follows: its pose and the WORLD semi-axes of the shell it
    /// collides as. For a super-shielded track prism that is the stellated octahedron's bounding
    /// half-extents (<c>3 x collider half-size x scale</c> — the stella's spike tips reach the
    /// corners of that box), which is what a hull has to clear and what a skimmer has to reach.
    /// </summary>
    public readonly struct SkimRoutePrism
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 ShellSemiAxes;
        public readonly bool IsMarker;

        public SkimRoutePrism(Vector3 position, Quaternion rotation, Vector3 shellSemiAxes, bool isMarker)
        {
            Position = position;
            Rotation = rotation;
            ShellSemiAxes = shellSemiAxes;
            IsMarker = isMarker;
        }
    }

    /// <summary>
    /// Something a skim racer must not touch with its hull: an oriented box in world space. A
    /// rival's wake rail, a Boost Ring's danger prism. Shielded mass is handed over as the box that
    /// bounds its shell, so a single test covers both.
    /// </summary>
    public readonly struct SkimObstacle
    {
        public readonly Vector3 Center;
        public readonly Quaternion Rotation;
        public readonly Vector3 HalfExtents;

        public SkimObstacle(Vector3 center, Quaternion rotation, Vector3 halfExtents)
        {
            Center = center;
            Rotation = rotation;
            HalfExtents = halfExtents;
        }
    }

    /// <summary>
    /// Everything a skim racer is allowed to know about itself this frame — the same facts a human
    /// pilot has (their ship, their speed, their boost gauge, the crystal they can see) plus the
    /// vessel's own control constants, read live so a drift's raised turn rate is the one planned
    /// against (ARCHITECTURE.md R2, R6).
    /// </summary>
    public struct SkimRacerSensors
    {
        public float Time;
        public float DeltaTime;

        public Vector3 Position;
        /// <summary>The vessel TRANSFORM's rotation — what the camera and the hull colliders see.</summary>
        public Quaternion Rotation;
        /// <summary>The rotation the pilot's stick has already asked for
        /// (<c>VesselTransformer.CommandedRotation</c>); the transform follows it at
        /// <see cref="FollowRate"/>. Knowing both is what lets the controller lead the lag instead
        /// of fighting it.</summary>
        public Quaternion CommandedRotation;
        public Vector3 Course;
        public float Speed;

        public float BoostMultiplier;
        public float MaxBoost;
        public float ThrottleScaler;

        public float PitchRateDegrees;
        public float YawRateDegrees;
        public float RollRateDegrees;
        /// <summary>Fraction of the remaining transform-to-commanded rotation closed per second
        /// (<c>VesselTransformer.RotationFollowRate</c>).</summary>
        public float FollowRate;

        public float SkimRadius;
        public Vector3 HullHalfExtents;
        public bool IsDrifting;

        public bool HasCrystal;
        public Vector3 CrystalPosition;
        /// <summary>The radius of the crystal's collection trigger — a hull touching that sphere
        /// collects it.</summary>
        public float CrystalRadius;

        public bool RingReady;
    }

    /// <summary>
    /// A skim racer's hands for one frame: the same four stick axes and two buttons a human has
    /// (ARCHITECTURE.md R1). The driver writes them to <c>InputStatus</c> and presses nothing else.
    /// </summary>
    public struct SkimRacerCommand
    {
        public float XSum;
        public float YSum;
        public float YDiff;
        public float XDiff;
        public bool Drift;
        public bool PressRing;
    }
}
