using Unity.Entities;
using Unity.Mathematics;

namespace CosmicShore.ECS
{
    /// <summary>
    /// A body under a black hole's gravity (Docs/BLACK_HOLE.md). Carried by every prism's
    /// companion render entity (added on the prototype so admitting a prism to a gravity field
    /// is a non-structural <c>SetComponentData</c> + <c>SetComponentEnabled</c>, never an
    /// archetype move), DISABLED until a hole's influence sphere reaches the prism.
    ///
    /// The state is the body's velocity and nothing else about its mass: every body is a test
    /// particle (the equivalence principle — trajectories do not depend on the mass that
    /// follows them), so there is no mass field to author and nothing for a heavy prism to do
    /// differently from a light one. Position is the entity's <c>LocalToWorld</c> translation,
    /// which the gravity job reads from the prism's own transform and writes back through the
    /// movers contract, so the component never carries a second copy of where the prism is.
    ///
    /// <see cref="Flags"/> is written by the job and read by the main thread: a CAPTURED body has
    /// crossed a horizon this frame and is consumed into the singularity; a RELEASED body has
    /// coasted clear of every hole and slowed below the release speed, and goes back to being
    /// static mass. Both are one-shot verdicts the owner acts on and clears.
    /// </summary>
    public struct GravityBody : IComponentData, IEnableableComponent
    {
        public const uint FlagCaptured = 1u << 0;
        public const uint FlagReleased = 1u << 1;

        /// <summary>World-space velocity, u/s.</summary>
        public float3 Velocity;

        /// <summary>Index of the hole that captured this body (valid only with <see cref="FlagCaptured"/>).</summary>
        public int CapturedBy;

        /// <summary><see cref="FlagCaptured"/> / <see cref="FlagReleased"/>, one-shot verdicts from the job.</summary>
        public uint Flags;

        public bool IsCaptured => (Flags & FlagCaptured) != 0;
        public bool IsReleased => (Flags & FlagReleased) != 0;
    }
}
