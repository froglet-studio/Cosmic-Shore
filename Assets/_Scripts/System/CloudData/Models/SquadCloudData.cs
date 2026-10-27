using System;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// The player's saved squads and which one is active (Docs/PILOTS_AND_ARTIFACTS.md §1).
    /// Cloud key: "SQUAD_DATA".
    ///
    /// <para>Schema 1 replaces the earlier one-squad mirror (leader + two rogues, class + element
    /// each). That mirror was loaded and reset but never written by anything, so no record carries
    /// data worth migrating; its fields deserialize into nothing and the book starts empty. The
    /// local <c>squad.data</c> file that <c>SquadSystem</c> writes for the retired captain screen is
    /// a separate store and is not read here.</para>
    /// </summary>
    [Serializable]
    public class SquadCloudData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public SquadBook Book = new();
    }
}
