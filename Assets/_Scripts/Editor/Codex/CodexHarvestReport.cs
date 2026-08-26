using System.Collections.Generic;

namespace CosmicShore.Editor.Codex
{
    /// <summary>
    /// What a scan actually did. Reported rather than logged because the point of the scan is that
    /// it is safe to run at any time - the human needs to see that it added two entries and
    /// touched nothing else, not trust that it did.
    /// </summary>
    public sealed class CodexHarvestReport
    {
        public readonly List<string> Added = new();
        public readonly List<string> Updated = new();

        /// <summary>Entries with <c>LockAutoHarvest</c> set - skipped whole, by request.</summary>
        public readonly List<string> Locked = new();

        /// <summary>
        /// Entries in the codex that the scan could no longer find a source asset for. NEVER
        /// deleted automatically: a species can go missing because someone is mid-refactor, and a
        /// tool that answers that by deleting an authored page with hand-written body copy is a
        /// tool nobody will run twice.
        /// </summary>
        public readonly List<string> Orphans = new();

        public readonly List<string> Warnings = new();

        public bool AnyChange => Added.Count > 0 || Updated.Count > 0;

        public string Summary =>
            $"{Added.Count} added, {Updated.Count} updated, {Locked.Count} locked, " +
            $"{Orphans.Count} orphaned, {Warnings.Count} warning(s)";
    }
}
