using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The standard element ladder every arcade card's intensities follow unless the card authors its own
    /// (Garrett, 2026-11-16; Docs/ElementalLoop/PLAYBOOK.md step 2). Each rung adds one thing to the rung below:
    ///
    /// <list type="bullet">
    /// <item><b>I1</b> nothing: every hull starts at rest. Slower is not easier - "winning is relative to other
    /// players, and they will be slower too", and a fast hull is harder to control.</item>
    /// <item><b>I2</b> every hull starts at level 5 in the game's element (<see cref="ModeGenre"/>), the level the
    /// fleet's level-5 upgrades open at.</item>
    /// <item><b>I3</b> adds team crystals of the game's element on the map, one set per team, each taken only by its
    /// team.</item>
    /// <item><b>I4</b> adds fauna of the game's element, in the cell's colour (the locked one-colour fauna rule).</item>
    /// </list>
    ///
    /// <para>This class owns the rung numbers and the starting-element half, which is a pure table. A card that
    /// authors ANY <c>SO_ArcadeGame.StartingElements</c> row owns its whole table and gets no default, and an arena
    /// card is never defaulted (Regatta and Broadside solve their own handicaps; arena cards play at one intensity).
    /// Pure and Unity-free, so it compiles into <c>CosmicShore.Data</c> beside <see cref="ModeGenre"/>.</para>
    /// </summary>
    public static class ElementLadder
    {
        /// <summary>The first rung that starts every hull at level 5 in the game's element (and every rung above).</summary>
        public const int LevelFiveFrom = 2;

        /// <summary>The first rung that lays team crystals of the game's element.</summary>
        public const int TeamCrystalsFrom = 3;

        /// <summary>The first rung that adds fauna of the game's element in the cell's colour.</summary>
        public const int FaunaFrom = 4;

        /// <summary>Level 5, normalized (<see cref="ResourceCollection"/>: 1 = level 10).</summary>
        public const float LevelFive = 0.5f;

        /// <summary>Whether <paramref name="intensity"/> starts hulls at level 5 in the game's element.</summary>
        public static bool StartsAtLevelFive(int intensity) => intensity >= LevelFiveFrom;

        /// <summary>Whether <paramref name="intensity"/> lays team crystals of the game's element.</summary>
        public static bool HasTeamCrystals(int intensity) => intensity >= TeamCrystalsFrom;

        /// <summary>Whether <paramref name="intensity"/> adds fauna of the game's element.</summary>
        public static bool HasFauna(int intensity) => intensity >= FaunaFrom;

        /// <summary>
        /// Levels with <paramref name="primary"/> (and <paramref name="secondary"/>, for a two-element game) at
        /// level 5 and every other element at rest. <see cref="Element.None"/> and <see cref="Element.Omni"/> name
        /// no single element and add nothing.
        /// </summary>
        public static ResourceCollection LevelFiveIn(Element primary, Element secondary = Element.None)
        {
            var levels = default(ResourceCollection);
            Raise(ref levels, primary);
            Raise(ref levels, secondary);
            return levels;
        }

        static void Raise(ref ResourceCollection levels, Element element)
        {
            switch (element)
            {
                case Element.Mass: levels.Mass = LevelFive; break;
                case Element.Charge: levels.Charge = LevelFive; break;
                case Element.Space: levels.Space = LevelFive; break;
                case Element.Time: levels.Time = LevelFive; break;
            }
        }

        /// <summary>
        /// The default starting-element table for a card that authors none: one row per distinct hull per rung from
        /// <see cref="LevelFiveFrom"/> to 4, each at level 5 in the game's element. No row at I1, so I1 hulls start
        /// at rest (<see cref="VesselStartingElements.TryResolve"/> leaves an unlisted rung alone). Writes nothing
        /// when the game has no single element to name. Rows are explicit per hull and rung so they ride the
        /// existing config-sync arrays with no wildcard and no new wire field.
        /// </summary>
        public static void AppendDefaultStartingElements(IList<VesselClassType> hulls, Element primary,
                                                         Element secondary, List<VesselStartingElements> into)
        {
            if (hulls == null || into == null) return;
            var levels = LevelFiveIn(primary, secondary);
            if (levels.Mass == 0f && levels.Charge == 0f && levels.Space == 0f && levels.Time == 0f) return;

            for (int h = 0; h < hulls.Count; h++)
            {
                var hull = hulls[h];
                if (hull == VesselClassType.Any || hull == VesselClassType.Random) continue;
                bool seen = false;
                for (int k = 0; k < h; k++) if (hulls[k] == hull) { seen = true; break; }
                if (seen) continue;

                for (int rung = LevelFiveFrom; rung <= 4; rung++)
                    into.Add(new VesselStartingElements(hull, rung, levels));
            }
        }
    }
}
