namespace CosmicShore.Core
{
    /// <summary>
    /// TEMPORARY kill-switch for progression/quest cloud sync while the FTUE is stabilized.
    ///
    /// While <see cref="CloudEnabled"/> is false:
    ///  • Quest Graph progress lives ONLY in the local PlayerPrefs mirror (no UGS reads/writes),
    ///    so the editor's reset button clears everything instantly without a signed-in session.
    ///  • GameModeProgressionService neither loads from nor saves to the GAME_MODE_PROGRESSION
    ///    cloud record — every play session starts from a fresh progression state (first quest
    ///    mode unlocked, intensities 1–2). The Froglet Toolbox can still unlock things live.
    ///  • Vessel unlock changes are not persisted to the hangar cloud record.
    ///
    /// Flip to true once the FTUE flow is signed off to restore full cloud persistence.
    /// (readonly, not const, so gated branches don't produce unreachable-code warnings.)
    /// </summary>
    public static class ProgressionBackendGate
    {
        public static readonly bool CloudEnabled = false;
    }
}
