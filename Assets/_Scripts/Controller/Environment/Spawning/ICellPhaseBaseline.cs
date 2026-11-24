namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An environment whose laid mass is not known when its cell config's <c>PhaseThresholds</c> were measured, and
    /// which tells its cell how much to raise them. A cell's phase ladder rides its environment's laid VOLUME
    /// (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md): a generator measures it into the config, and an environment whose
    /// content changes per course (a Regatta course genome's placements) adds what it laid on top, so a course with
    /// 12,000 extra prisms starts as calm as the measured one instead of opening in Frenzy.
    ///
    /// <para>Implemented by <see cref="SpawnableRegattaRails"/>: the volume and count of its genome's placement prisms
    /// (0 on every shipped course, whose ladder already holds the rails).</para>
    /// </summary>
    public interface ICellPhaseBaseline
    {
        /// <summary>Laid volume the measured thresholds do not hold (Σ |x·y·z| of the extra prisms).</summary>
        float PhaseBaselineVolume { get; }

        /// <summary>Laid prism count the measured count backstop does not hold.</summary>
        int PhaseBaselinePrisms { get; }
    }
}
