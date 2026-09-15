using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Data
{
    /// <summary>
    /// The element levels ONE vessel class starts a match at, authored on an arcade card
    /// (<c>SO_ArcadeGame.StartingElements</c>) - the platform's handicap dial for a card that
    /// seats several hulls.
    ///
    /// <para><b>Why this exists.</b> Element levels are the one thing that reaches a hull's
    /// SPEED without touching the vessel (Time is Soar on the Manta, the throttle ceiling on the
    /// Scarab, the boost on the Sparrow and the Serpent, the charge fill rate on the Dolphin).
    /// A card that lets a 35 u/s Sparrow and a 1210 u/s Rhino start the same race has no other
    /// per-hull lever the platform already owns: a mode-local speed multiplier would be the
    /// bespoke system CLAUDE.md forbids, and a per-vessel prefab edit would move every other
    /// mode. Starting levels are already a concept (<c>SO_Vessel.InitialResourceLevels</c>,
    /// the training games' element picks) - this is that concept keyed by CARD and HULL, which
    /// is where a handicap belongs, because it is a fact about the race and not about the
    /// vessel.</para>
    ///
    /// <para><b>Resolution.</b> A row with <see cref="Intensity"/> 0 applies at every intensity;
    /// a row naming an intensity wins over it for that intensity only, so a card can author a
    /// baseline plus per-rung corrections. A hull with no row keeps the platform default (every
    /// element at rest, level 0). See <see cref="TryResolve"/>.</para>
    ///
    /// <para>Levels are NORMALIZED (<c>ResourceCollection</c>: 0 = rest, 1 = level 10, the
    /// -0.5..1.5 band the resource system clamps to). The sustained ceiling still applies -
    /// nothing above 1.0 is held (Docs/ECOSYSTEM.md 15), so a row above it buys a transient.</para>
    ///
    /// <para>Lives in <c>CosmicShore.Data</c> deliberately: it depends on nothing but two
    /// sibling types, so the config-sync RPC, the card and the vessel can all name it without
    /// the Data assembly reaching back into gameplay.</para>
    /// </summary>
    [Serializable]
    public struct VesselStartingElements
    {
        [Tooltip("The hull this row seeds. A hull the card does not list is never asked.")]
        public VesselClassType Class;

        [Tooltip("0 = every intensity. 1-4 = this intensity only, and wins over a 0 row for it.")]
        [Range(0, 4)] public int Intensity;

        [Tooltip("Normalized element levels at match start: 0 = rest, 1 = level 10, -0.5 = level -5.")]
        public ResourceCollection Levels;

        public VesselStartingElements(VesselClassType vesselClass, int intensity, ResourceCollection levels)
        {
            Class = vesselClass;
            Intensity = intensity;
            Levels = levels;
        }

        /// <summary>
        /// The row that applies to <paramref name="vesselClass"/> at <paramref name="intensity"/>:
        /// the intensity-specific row when the table has one, else the intensity-0 row, else
        /// nothing. Pure and allocation-free, so it is safe on a spawn path.
        /// </summary>
        public static bool TryResolve(IList<VesselStartingElements> table, VesselClassType vesselClass,
                                      int intensity, out ResourceCollection levels)
        {
            levels = default;
            if (table == null) return false;

            bool found = false;
            int bestSpecificity = -1;
            for (int i = 0; i < table.Count; i++)
            {
                var row = table[i];
                if (row.Class != vesselClass) continue;
                if (row.Intensity != 0 && row.Intensity != intensity) continue;

                int specificity = row.Intensity == 0 ? 0 : 1;
                if (specificity <= bestSpecificity) continue;

                bestSpecificity = specificity;
                levels = row.Levels;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Flatten a table for the wire (Netcode RPCs take primitive arrays, not structs):
        /// one class and one intensity per row, four levels per row in
        /// <c>Mass, Charge, Space, Time</c> order - the <see cref="ResourceCollection"/>
        /// constructor's order, restated here so the two halves cannot disagree.
        /// </summary>
        public static void Pack(IList<VesselStartingElements> table,
                                out int[] classes, out int[] intensities, out float[] levels)
        {
            int n = table?.Count ?? 0;
            classes = new int[n];
            intensities = new int[n];
            levels = new float[n * 4];
            for (int i = 0; i < n; i++)
            {
                var row = table[i];
                classes[i] = (int)row.Class;
                intensities[i] = row.Intensity;
                levels[i * 4 + 0] = row.Levels.Mass;
                levels[i * 4 + 1] = row.Levels.Charge;
                levels[i * 4 + 2] = row.Levels.Space;
                levels[i * 4 + 3] = row.Levels.Time;
            }
        }

        /// <summary>The inverse of <see cref="Pack"/>. Rows whose arrays disagree in length are
        /// dropped rather than guessed at - a table that arrived torn is a table nobody authored.</summary>
        public static void Unpack(int[] classes, int[] intensities, float[] levels,
                                  List<VesselStartingElements> into)
        {
            into.Clear();
            if (classes == null || intensities == null || levels == null) return;
            int n = Math.Min(classes.Length, intensities.Length);
            n = Math.Min(n, levels.Length / 4);
            for (int i = 0; i < n; i++)
            {
                into.Add(new VesselStartingElements(
                    (VesselClassType)classes[i], intensities[i],
                    new ResourceCollection(levels[i * 4 + 0], levels[i * 4 + 1],
                                           levels[i * 4 + 2], levels[i * 4 + 3])));
            }
        }
    }
}
