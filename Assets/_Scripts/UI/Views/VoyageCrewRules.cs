using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// What the HyperSea page's "Open now" column and the crew screen show, with no drawing in it (Frogma Rev 113 Sea and
    /// Crew; Docs/MENU_REDESIGN_B.md section 209.1): the voyages that sail now (the escort and the jobs Milestone 1 leaves
    /// open, <see cref="VoyageToyDefinitionSO.IsJobOpen"/>), and every other crew voyage under the faction of the pilot who
    /// brings it, read from that pilot's record (<see cref="PilotRecord.FactionType"/>).
    /// </summary>
    public static class VoyageCrewRules
    {
        /// <summary>One faction's locked voyages, as indices into the offers.</summary>
        public sealed class Group
        {
            public FactionType House;
            public readonly List<int> Offers = new();
        }

        /// <summary>The offers that sail now (not locked), in the board's order.</summary>
        public static void OpenNow(IReadOnlyList<VoyageBoard.Offer> offers, List<int> into)
        {
            into.Clear();
            if (offers == null) return;
            for (int i = 0; i < offers.Count; i++)
                if (offers[i] != null && !offers[i].Locked) into.Add(i);
        }

        /// <summary>How many offers are locked for Milestone 1.</summary>
        public static int LockedCount(IReadOnlyList<VoyageBoard.Offer> offers)
        {
            int n = 0;
            if (offers == null) return 0;
            foreach (var o in offers)
                if (o != null && o.Locked) n++;
            return n;
        }

        /// <summary>
        /// The locked offers grouped by faction, the factions in the game's order (<see cref="FactionType"/>); a faction
        /// with no locked voyage has no group (the popup leaves it out). A voyage whose pilot has no faction falls under
        /// <see cref="FactionType.None"/>, listed last.
        /// </summary>
        public static void Groups(IReadOnlyList<VoyageBoard.Offer> offers, List<Group> into)
        {
            into.Clear();
            if (offers == null) return;
            var byHouse = new SortedDictionary<int, Group>();
            for (int i = 0; i < offers.Count; i++)
            {
                var o = offers[i];
                if (o == null || !o.Locked) continue;
                // None sorts after every faction.
                int order = o.House == FactionType.None ? int.MaxValue : (int)o.House;
                if (!byHouse.TryGetValue(order, out var g)) byHouse[order] = g = new Group { House = o.House };
                g.Offers.Add(i);
            }
            into.AddRange(byHouse.Values);
        }

        /// <summary>
        /// Your crew across the top of the crew screen (Frogma Rev 114 C2, pilot first): every offer once, the ones that sail
        /// now first and then the locked ones, each in the board's order.
        /// </summary>
        public static void Crew(IReadOnlyList<VoyageBoard.Offer> offers, List<int> into)
        {
            into.Clear();
            if (offers == null) return;
            for (int i = 0; i < offers.Count; i++)
                if (offers[i] != null && !offers[i].Locked) into.Add(i);
            for (int i = 0; i < offers.Count; i++)
                if (offers[i] != null && offers[i].Locked) into.Add(i);
        }

        /// <summary>The voyage the Y button offers instead of <paramref name="picked"/>: the first other one that can set
        /// sail now, or -1.</summary>
        public static int OtherOpen(IReadOnlyList<VoyageBoard.Offer> offers, int picked)
        {
            if (offers == null) return -1;
            for (int i = 0; i < offers.Count; i++)
                if (i != picked && offers[i] is { SetSail: not null }) return i;
            return -1;
        }

        /// <summary>A pilot's first name for their tile ("Tamako" of "Tamako Deephook").</summary>
        public static string FirstName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            name = name.Trim();
            int space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }

        /// <summary>A faction's group name in the content's words (Hunters, Pirates...).</summary>
        public static string FactionName(VoyageLines lines, FactionType house)
        {
            if (lines == null) return null;
            return house switch
            {
                FactionType.Merchant => lines.FactionMerchant,
                FactionType.Hunter => lines.FactionHunter,
                FactionType.Aristocrat => lines.FactionAristocrat,
                FactionType.Pirate => lines.FactionPirate,
                FactionType.BountyHunter => lines.FactionBountyHunter,
                FactionType.Explorer => lines.FactionExplorer,
                FactionType.Activist => lines.FactionActivist,
                FactionType.Thief => lines.FactionThief,
                FactionType.Gatherer => lines.FactionGatherer,
                FactionType.Contractor => lines.FactionContractor,
                FactionType.Sport => lines.FactionSport,
                FactionType.Researcher => lines.FactionResearcher,
                FactionType.Preservationist => lines.FactionPreservationist,
                FactionType.FirstResponder => lines.FactionFirstResponder,
                FactionType.Priest => lines.FactionPriest,
                FactionType.Diplomat => lines.FactionDiplomat,
                _ => lines.FactionNone,
            };
        }
    }
}
