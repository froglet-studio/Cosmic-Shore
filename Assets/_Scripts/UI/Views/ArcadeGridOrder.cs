using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The ALL GAMES grid's order and filters (Docs/MENU_REDESIGN_B.md sections 85 and 97), as a pure function of the
    /// roster and two questions about a game, so the view owns no ordering rule of its own and the rules can be tested
    /// without a scene. The order is fixed: favourites first, then A-Z under them (Shombith 2026-10-27: "Favorites to
    /// the top by default, A-Z under. Drop the sort controls"). The two filters are a category (Time, Charge,
    /// Mass-Space or Party: <see cref="ModeGenre.CategoryFor"/>, Garrett 2026-11-17) and a vessel (one the card seats);
    /// <see cref="ArcadeCategory.None"/> and <see cref="VesselClassType.Any"/> mean all. A game a
    /// guided path points at is never filtered out: the guide must always find the one door it leads to.
    /// </summary>
    public static class ArcadeGridOrder
    {
        public static List<SO_ArcadeGame> Apply(IEnumerable<SO_ArcadeGame> games, ArcadeCategory category, VesselClassType vessel,
                                                Func<SO_ArcadeGame, bool> isFavourite,
                                                Func<SO_ArcadeGame, ArcadeCategory, bool> inCategory,
                                                Func<SO_ArcadeGame, bool> alwaysShown = null)
        {
            var list = new List<SO_ArcadeGame>();
            foreach (var g in games)
                if (g && ((alwaysShown != null && alwaysShown(g)) || Passes(g, category, vessel, inCategory)))
                    list.Add(g);

            list.Sort((x, y) =>
            {
                int first = isFavourite(y).CompareTo(isFavourite(x));
                return first != 0 ? first : string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal);
            });
            return list;
        }

        static bool Passes(SO_ArcadeGame g, ArcadeCategory category, VesselClassType vessel, Func<SO_ArcadeGame, ArcadeCategory, bool> inCategory) =>
            (category == ArcadeCategory.None || inCategory(g, category)) && (vessel == VesselClassType.Any || Seats(g, vessel));

        /// <summary>Whether the card seats this vessel class.</summary>
        public static bool Seats(SO_ArcadeGame game, VesselClassType vessel)
        {
            if (!game || game.Vessels == null) return false;
            foreach (var v in game.Vessels)
                if (v && v.Class == vessel) return true;
            return false;
        }

        /// <summary>
        /// A mode's genre elements as the card's flower draws them. A party game draws no petal: it trains no element,
        /// even where its intensity ladder still reads one (<see cref="ModeGenre.IsParty"/>).
        /// </summary>
        public static bool TryGenres(GameModes mode, out Element primary, out Element secondary)
        {
            if (ModeGenre.IsParty(mode))
            {
                primary = secondary = Element.None;
                return false;
            }
            return ModePreviewLibrarySO.TryGenres(mode, out primary, out secondary);
        }

        /// <summary>Whether the card files under this arcade category.</summary>
        public static bool InCategory(SO_ArcadeGame game, ArcadeCategory category) =>
            game && ModePreviewLibrarySO.CategoryOf(game.Mode) == category;
    }
}
