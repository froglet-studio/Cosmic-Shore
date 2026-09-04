using System.Collections.Generic;
using CosmicShore.Utility;

namespace CosmicShore.Core
{
    /// <summary>
    /// Translates cloud-save keys written under a game mode's OLD enum member name.
    ///
    /// <para><b>Why this has to exist.</b> Enum VALUES are pinned forever, so a rename is free for
    /// anything that stores the number. Two stores do not store the number - they store
    /// <c>mode.ToString()</c>, the member NAME:</para>
    ///
    /// <list type="bullet">
    /// <item><see cref="GameModeProgressionData"/> - unlock state, quest completion, best stats,
    /// max unlocked intensity, and per-intensity play counts.</item>
    /// <item><see cref="ModeStatsCloudData"/> - best score, games played and games won, under
    /// <c>"{mode}:{intensity}"</c>.</item>
    /// </list>
    ///
    /// <para>Renaming <c>HexRace</c> to <c>SkimRace</c> without this would therefore re-lock the
    /// mode for every existing player, reset their quest completion and unlocked intensities, and
    /// orphan their bests - <b>silently</b>, because the new key simply finds nothing and every
    /// getter answers with its default. That reads as "the save is fine, the player just has no
    /// progress", which is the worst shape a data bug can take.</para>
    ///
    /// <para><b>This is a whole-key lookup, not a substring replace.</b> The stored value is
    /// exactly one enum member name, so unlike the source sweep in
    /// <c>Tools/Build/rename_game_modes.py</c> there is no ordering hazard and nothing to protect:
    /// <c>CrystalCaptureConfig</c> is not a mode name and can never appear here.</para>
    ///
    /// <para><b>It runs on every load, forever, and is idempotent.</b> There is no schema-version
    /// gate on purpose: a player can restore an old cloud save, or sign in on a device that has
    /// been offline since before the rename, at any point in the future. A version gate would
    /// migrate that save exactly once - after the version had already been bumped past it.</para>
    ///
    /// <para>Edit this together with <c>Tools/Build/rename_game_modes.py</c>; a rename in one
    /// without the other is what this class exists to prevent.</para>
    /// </summary>
    public static class GameModeRenameMigration
    {
        /// <summary>Old enum member name -> the name it carries now.</summary>
        static readonly Dictionary<string, string> Renames = new()
        {
            { "HexRace",                      "SkimRace" },
            { "MultiplayerCrystalCapture",    "Scurry" },
            { "Tournament",                   "Maelstrom" },
            { "NucleusRush",                  "BroodRush" },
            { "Ribcage",                      "PeelTheCage" },
            { "MultiplayerJoust",             "Joust" },
            { "MultiplayerCellularDuel",      "OnlineDuelForTheCell" },
            { "CellularDuel",                 "DuelForTheCell" },
            { "MultiplayerWildlifeBlitzGame", "CoOpWildlifeBlitz" },
            { "MazeRunner",                   "MazeRun" },
            { "Darts",                        "DolphinDarts" },
        };

        /// <summary>The map itself, so a test can assert every target is a real enum member and
        /// every source is not - the one check that catches a typo here, which would otherwise
        /// present as "that mode's progress just vanished for some players".</summary>
        public static IReadOnlyDictionary<string, string> Map => Renames;

        /// <summary>The new name for <paramref name="storedName"/>, or it unchanged.</summary>
        public static string Resolve(string storedName) =>
            storedName != null && Renames.TryGetValue(storedName, out var renamed) ? renamed : storedName;

        /// <summary>
        /// Splits a <c>"{mode}:{intensity}"</c> composite key, renames the mode half and puts it
        /// back. A key with no separator is treated as a bare mode name; anything unrecognised is
        /// returned untouched rather than dropped, because a key this does not understand is
        /// somebody else's data, not garbage.
        /// </summary>
        public static string ResolveCompositeKey(string storedKey)
        {
            if (string.IsNullOrEmpty(storedKey)) return storedKey;

            int sep = storedKey.IndexOf(':');
            if (sep < 0) return Resolve(storedKey);

            string mode = storedKey.Substring(0, sep);
            string renamed = Resolve(mode);
            return renamed == mode ? storedKey : renamed + storedKey.Substring(sep);
        }

        // ── Appliers ────────────────────────────────────────────────────────────────────

        /// <summary>Renames in place. Returns how many entries moved.</summary>
        public static int MigrateNames(List<string> names)
        {
            if (names == null) return 0;

            int moved = 0;
            for (int i = 0; i < names.Count; i++)
            {
                string renamed = Resolve(names[i]);
                if (renamed == names[i]) continue;

                // A list is a SET here (IsUnlocked is a Contains). Renaming onto a name already
                // present would duplicate it, so drop this entry instead of rewriting it.
                names[i] = names.Contains(renamed) ? null : renamed;
                moved++;
            }
            names.RemoveAll(string.IsNullOrEmpty);
            return moved;
        }

        /// <summary>
        /// Re-keys a mode-keyed dictionary. Where both the old and the new key exist - a save
        /// half-migrated by an interrupted session - the NEW key wins and the stale one is
        /// dropped, because the new key is the only one anything still writes to.
        /// </summary>
        public static int MigrateKeys<TValue>(Dictionary<string, TValue> map, bool composite = false)
        {
            if (map == null || map.Count == 0) return 0;

            List<(string OldKey, string NewKey, TValue Value)> moves = null;

            foreach (var pair in map)
            {
                string renamed = composite ? ResolveCompositeKey(pair.Key) : Resolve(pair.Key);
                if (renamed == pair.Key) continue;

                (moves ??= new List<(string, string, TValue)>()).Add((pair.Key, renamed, pair.Value));
            }

            if (moves == null) return 0;

            foreach (var (oldKey, newKey, value) in moves)
            {
                map.Remove(oldKey);
                if (!map.ContainsKey(newKey))
                    map[newKey] = value;
            }

            return moves.Count;
        }

        // ── Per-store entry points ──────────────────────────────────────────────────────

        public static void Migrate(GameModeProgressionData data)
        {
            if (data == null) return;

            int moved = MigrateNames(data.UnlockedModes)
                      + MigrateNames(data.CompletedQuests)
                      + MigrateKeys(data.BestStats)
                      + MigrateKeys(data.MaxUnlockedIntensity)
                      + MigrateKeys(data.IntensityPlayCounts, composite: true);

            if (moved > 0)
                CSDebug.Log($"[GameModeRename] Migrated {moved} progression entries onto the new mode names.");
        }

        public static void Migrate(ModeStatsCloudData data)
        {
            if (data?.Modes == null) return;

            int moved = MigrateKeys(data.Modes, composite: true);
            if (moved > 0)
                CSDebug.Log($"[GameModeRename] Migrated {moved} mode-stat records onto the new mode names.");
        }
    }
}
