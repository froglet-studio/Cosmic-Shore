using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The fleet's trail stretch at very high speed: past <see cref="StartSpeed"/> the gap between prisms grows, each
    /// prism lengthens in Z and thins in Y, and at <see cref="TopSpeed"/> the stretch is complete, so production falls
    /// behind the vessel. It is the stretch half of the Manta's trail (<see cref="TrailSpeedShape"/>, Caleb's fly
    /// test 2, <c>Docs/Manta/POLISH.md</c>), taken apart from the lay pacing and from the Manta's own shape.
    ///
    /// <para>Caleb, 2026-11-17: only vessels fast enough to make thematic sense of stretched prisms stretch, and only
    /// when they go VERY fast, at the same speed limits as the Manta. So the limits are absolute speeds (u/s), the
    /// Manta's (its Soar 600 and its Top Speed 1100), and the multipliers are relative to each hull's own prism: a hull
    /// that never passes 600 u/s lays exactly its authored prism at its authored wavelength. <c>TrailStretchTests</c>
    /// holds these defaults to the Manta's numbers.</para>
    ///
    /// <para>A vessel that authors <see cref="TrailSpeedShape"/> (the Manta) stretches through that instead. Pure: no
    /// Unity state is read here.</para>
    /// </summary>
    [Serializable]
    public class TrailStretch
    {
        [Tooltip("Off: this hull never stretches, at any speed.")]
        public bool Enabled = true;

        [Tooltip("The speed (u/s) where the stretch starts. The Manta's Soar Speed (600); the same for every hull.")]
        [Min(0f)] public float StartSpeed = 600f;
        [Tooltip("The speed (u/s) where the stretch is complete. The Manta's Top Speed (1100); the same for every hull.")]
        [Min(0f)] public float TopSpeed = 1100f;

        [Tooltip("At Top Speed the gap between prisms is the wavelength times this. 1 = no stretch. The Manta's 3.")]
        [Min(1f)] public float SpacingMultiplier = 3f;
        [Tooltip("At Top Speed a prism's Z is its own length times this. The Manta's Top Length over Soar Length (20 / 10).")]
        [Min(1f)] public float LengthMultiplier = 2f;
        [Tooltip("At Top Speed a prism's Y is its own height times this. The Manta's 0.4.")]
        [Range(0.05f, 1f)] public float HeightMultiplier = 0.4f;
        [Tooltip("A stretched prism stays under this fraction of the gap to the next one, so neighbours never touch. " +
                 "The Manta's 0.9. Never shortens a prism below its own length.")]
        [Min(0f)] public float MaxLengthOfSpacing = 0.9f;

        /// <summary>0 at or below <see cref="StartSpeed"/>, 1 at or above <see cref="TopSpeed"/>, smooth between.</summary>
        public float Amount(float speed) => Enabled ? TrailSpeedShape.Smooth(StartSpeed, TopSpeed, speed) : 0f;

        /// <summary>The gap between prisms on one rail at this speed: the wavelength until <see cref="StartSpeed"/>.</summary>
        public float LaySpacing(float wavelength, float speed) =>
            wavelength * Mathf.Lerp(1f, SpacingMultiplier, Amount(speed));

        /// <summary>
        /// The hull's own prism stretched for this speed. Unchanged at or below <see cref="StartSpeed"/>.
        /// </summary>
        /// <param name="scale">The prism as the hull lays it (BaseScale x its scalers and rules).</param>
        /// <param name="speed">Vessel speed, u/s (unwarped).</param>
        /// <param name="spacing">The lay spacing at this speed (<see cref="LaySpacing"/>), m.</param>
        public Vector3 Apply(Vector3 scale, float speed, float spacing)
        {
            float t = Amount(speed);
            if (t <= 0f) return scale;
            float z = scale.z * Mathf.Lerp(1f, LengthMultiplier, t);
            z = Mathf.Min(z, Mathf.Max(scale.z, spacing * MaxLengthOfSpacing));
            return new Vector3(scale.x, scale.y * Mathf.Lerp(1f, HeightMultiplier, t), z);
        }
    }
}
