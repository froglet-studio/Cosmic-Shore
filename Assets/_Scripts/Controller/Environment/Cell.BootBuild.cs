using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The cell's BOOT BUILD timing: when the boot world (Garland, in Menu_Main) may start laying.
    /// </summary>
    public partial class Cell
    {
        /// <summary>
        /// True while the boot world can be laid now instead of after the local player spawns: the boot is still
        /// creating the party session and no host or client is running yet (B36 loads the menu before the Relay
        /// host). The deferred build waited for the local player because the vessel spawn chain needs responsive
        /// frames, but before any host exists there is no spawn chain and no Relay peer to drop, and the 5-10 s
        /// of UGS calls would otherwise be followed by a 3 s lay (measured 2026-11-21). Doing the lay during the
        /// calls shortens the boot by about the lay.
        /// </summary>
        bool BuildsBeforeTheHost =>
            BuildsBeforeHost(cellConfigData && cellConfigData.BootDefault,
                             NetworkManager.Singleton is { IsListening: true });

        /// <summary>See <see cref="BuildsBeforeTheHost"/>: only the boot world, and only while nothing listens.</summary>
        internal static bool BuildsBeforeHost(bool bootWorld, bool networkListening) => bootWorld && !networkListening;
    }
}
