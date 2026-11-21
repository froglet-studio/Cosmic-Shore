namespace CosmicShore.Data
{
    /// <summary>
    /// The shape of a LIT volume - the region of space a force is acting on, or about to.
    ///
    /// There are exactly three because the platform's force volumes are exactly three, and each
    /// member names one that already ships with a Burst query, a CPU transcription and a trigger
    /// collider behind it (<c>PrismSpatialIndex.AOESpatialQueryJob</c> /
    /// <c>AOEConicSweepQueryJob</c> / <c>AOECylinderSweepQueryJob</c>). A fourth member is a
    /// promise that a fourth query exists; do not add one to describe a volume nothing sweeps.
    ///
    /// Numeric values are explicit to prevent Unity serialization drift (CLAUDE.md > Code Style),
    /// and they are the wire format: <c>PrismLit</c> packs the member straight into a shader
    /// global that <c>PrismDestructionSight.hlsl</c> switches on, so these numbers are mirrored by
    /// the <c>PRISM_LIT_SHAPE_*</c> defines in that file - change both together.
    /// </summary>
    public enum LitShape
    {
        /// <summary>
        /// A cone whose cross-section is a CAPSULE - the Dolphin crystal blast's swept volume.
        /// Params are (height, coreRadiusPerUnitDepth, halfLengthPerUnitDepth); the near clip is
        /// the apex, so a point behind the emitter is never inside even though the axis extends
        /// backwards mathematically.
        /// </summary>
        Cone = 0,

        /// <summary>
        /// A sphere about the origin - an ordinary AOE blast, and the Sparrow warhead's proximity
        /// fuze. Params are (radius, unused, unused); <c>Axis</c> and <c>GapeAxis</c> are ignored,
        /// which is why a producer of one need not invent a direction for it.
        /// </summary>
        Sphere = 1,

        /// <summary>
        /// A cylinder swept along its own face normal, flat end caps, radius CONSTANT along the
        /// sweep - the Scarab's cavitation plate. Params are (reach, radius, mirrored) where a
        /// non-zero third component reflects the volume through the START plane, claiming
        /// <c>|axial|</c> rather than <c>axial</c>.
        ///
        /// The mirror rides a PARAM rather than its own enum member because it is a property of
        /// one blast's authoring (<c>AOECylindricalExplosion.mirrorAboutStartPlane</c>), not a
        /// different shape with a different query - the Burst job tests <c>|s|</c> under one flag
        /// too, and splitting it here would put the same volume under two names.
        /// </summary>
        Cylinder = 2,
    }
}
