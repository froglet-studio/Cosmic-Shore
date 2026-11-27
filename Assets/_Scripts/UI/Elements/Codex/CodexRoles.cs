using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// ECOLOGY's VESSELS and ROLES stops (Shombith, 2026-11-26: the roles and archetypes on the ecology page), built from
    /// the one archetype table (<see cref="VesselArchetypesSO"/>, generated from Docs/Vessels/archetypes.json, the board's
    /// copy) as a pure function, like the rest of <see cref="CodexCatalog"/>:
    /// <list type="bullet">
    /// <item>VESSELS - every vessel in the Hangar's order: its role as the kicker, the role's goal as its gameplay, what
    /// its primary archetype counters and is countered by as facts, and its archetypes, primary first, as rows;</item>
    /// <item>ROLES - each role (its goal, its three archetypes), each followed by its three archetypes (its line, what it
    /// counters and is countered by, and every vessel that has it, by rank).</item>
    /// </list>
    /// Every name and line comes from the table or from <see cref="CodexWords"/>; nothing here is player text.
    /// </summary>
    public static class CodexRoles
    {
        static readonly VesselRole[] RoleOrder = { VesselRole.Cultivator, VesselRole.Destroyer, VesselRole.Guardian };

        public static List<CodexItem> Vessels(VesselArchetypesSO table, SO_VesselList vessels, CodexWords words)
        {
            var items = new List<CodexItem>();
            if (!table || !vessels || vessels.VesselList == null) return items;
            foreach (var vessel in vessels.VesselList)
            {
                if (!vessel) continue;
                var mine = table.ArchetypesOf(vessel.Class);
                if (mine == null || mine.Count == 0) continue;
                var role = table.RoleOf(vessel.Class);
                var page = new CodexPage
                {
                    Image = vessel.IconActive, Kicker = table.RoleName(role), Title = vessel.Name ?? string.Empty,
                    Gameplay = table.RoleLine(role), RowsHeading = words.vesselRowsHeading,
                };
                AddCounterFacts(page, table, role, mine[0], words);
                for (int i = 0; i < mine.Count; i++)
                    page.Rows.Add(new CodexRow(null,
                        TemplateText.Fill(words.vesselRowTemplate, "archetype", table.ArchetypeName(mine[i]), "rank", Rank(words, i)),
                        table.ArchetypeLine(mine[i])));
                items.Add(new CodexItem { Image = page.Image, Name = page.Title, Sub = table.ArchetypeName(mine[0]), Page = page });
            }
            return items;
        }

        public static List<CodexItem> Roles(VesselArchetypesSO table, SO_VesselList vessels, CodexWords words)
        {
            var items = new List<CodexItem>();
            if (!table) return items;
            foreach (var role in RoleOrder)
            {
                var archetypes = ArchetypesIn(role);
                var rolePage = new CodexPage
                {
                    Image = PictureOf(table, vessels, role, VesselArchetype.None), Kicker = words.roleKicker,
                    Title = table.RoleName(role), Gameplay = table.RoleLine(role), RowsHeading = words.roleRowsHeading,
                };
                foreach (var a in archetypes)
                    rolePage.Rows.Add(new CodexRow(null, table.ArchetypeName(a), table.ArchetypeLine(a)));
                items.Add(new CodexItem { Image = rolePage.Image, Name = rolePage.Title, Sub = table.RoleLine(role), Page = rolePage });

                foreach (var a in archetypes)
                {
                    var page = new CodexPage
                    {
                        Image = PictureOf(table, vessels, role, a), Kicker = table.RoleName(role), Title = table.ArchetypeName(a),
                        Gameplay = table.ArchetypeLine(a), RowsHeading = words.archetypeRowsHeading,
                    };
                    AddCounterFacts(page, table, role, a, words);
                    if (vessels && vessels.VesselList != null)
                        // Primary holders first, then secondary, then tertiary: who plays it best reads first.
                        for (int rank = 0; rank < 3; rank++)
                            foreach (var vessel in vessels.VesselList)
                            {
                                if (!vessel) continue;
                                var mine = table.ArchetypesOf(vessel.Class);
                                if (mine != null && rank < mine.Count && mine[rank] == a)
                                    page.Rows.Add(new CodexRow(vessel.IconActive, vessel.Name, Rank(words, rank)));
                            }
                    items.Add(new CodexItem { Image = page.Image, Name = page.Title, Sub = table.RoleName(role), Page = page });
                }
            }
            return items;
        }

        static void AddCounterFacts(CodexPage page, VesselArchetypesSO table, VesselRole role, VesselArchetype a, CodexWords words)
        {
            CodexCatalog.AddFact(page, words.roleLabel, table.RoleName(role));
            CodexCatalog.AddFact(page, words.countersLabel, Names(table, table.Counters(a), words));
            CodexCatalog.AddFact(page, words.counteredByLabel, Names(table, table.CounteredBy(a), words));
            if (page.Facts.Count > 0) page.FactsHeading = words.factsHeading;
        }

        static string Names(VesselArchetypesSO table, IReadOnlyList<VesselArchetype> list, CodexWords words)
        {
            if (list == null || list.Count == 0) return words.noneLine;
            var names = new string[list.Count];
            for (int i = 0; i < list.Count; i++) names[i] = table.ArchetypeName(list[i]);
            return string.Join(words.listSeparator, names);
        }

        static string Rank(CodexWords words, int i) =>
            words.rankNames != null && i >= 0 && i < words.rankNames.Length ? words.rankNames[i] : string.Empty;

        static List<VesselArchetype> ArchetypesIn(VesselRole role)
        {
            var list = new List<VesselArchetype>(3);
            for (int v = (int)VesselArchetype.Pumper; v <= (int)VesselArchetype.Enhancer; v++)
                if (VesselArchetypes.RoleOf((VesselArchetype)v) == role) list.Add((VesselArchetype)v);
            return list;
        }

        /// <summary>An archetype's picture is the first vessel (Hangar order) whose primary it is, else the first that has it
        /// at all, else its role's picture; a role's is the first vessel whose primary is in that role. None: no picture.</summary>
        static UnityEngine.Sprite PictureOf(VesselArchetypesSO table, SO_VesselList vessels, VesselRole role, VesselArchetype a)
        {
            if (!vessels || vessels.VesselList == null) return null;
            if (a != VesselArchetype.None)
                return FirstVessel(table, vessels, mine => mine[0] == a) ?? FirstVessel(table, vessels, mine => mine.Contains(a))
                       ?? PictureOf(table, vessels, role, VesselArchetype.None);
            return FirstVessel(table, vessels, mine => VesselArchetypes.RoleOf(mine[0]) == role);
        }

        static UnityEngine.Sprite FirstVessel(VesselArchetypesSO table, SO_VesselList vessels,
                                              System.Func<IReadOnlyList<VesselArchetype>, bool> match)
        {
            foreach (var vessel in vessels.VesselList)
            {
                if (!vessel) continue;
                var mine = table.ArchetypesOf(vessel.Class);
                if (mine is { Count: > 0 } && match(mine)) return vessel.IconActive;
            }
            return null;
        }
    }
}
