namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Canopy Run (GameModes.CanopyRun = 45): the Gibbon-only brachiation race. Skim Race's
    /// controller VERBATIM - the seeded track spawn, the domain crystal target, golf-timed finish,
    /// the server-authoritative winner and the scene-reload replay are exactly what a race through
    /// the canopy needs - so this class adds no behaviour. It exists because a scene's controller
    /// script is its identity: the generator swaps SkimRaceController's guid for this one in the
    /// cloned scene, mode-keyed platform switches (EndConditionOverridesSO, ElementalComebackSystem,
    /// UGSStatsManager, the objective provider, the track-impact SFX) already carry the CanopyRun
    /// rows, and any future Canopy-Run-only rule lands here rather than as a branch in Skim Race.
    ///
    /// What differs is DATA, not code: the arena is <see cref="SpawnableCanopyTrack"/> (boughs and
    /// hoops on the same waypoints), the card locks the roster to the Gibbon, and the crystal
    /// manager's anchor sets are re-authored onto the waypoints so every crystal sits inside its
    /// hoop. See _Scripts/Controller/Arcade/CANOPYRUN.md.
    /// </summary>
    public class CanopyRunController : SkimRaceController
    {
    }
}
