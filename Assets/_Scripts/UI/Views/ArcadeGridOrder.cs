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
    /// the top by default, A-Z under. Drop the sort controls"). The two filters are an element (the card's genre) and a
    /// vessel (one the card seats); <see cref="Element.None"/> and <see cref="VesselClassType.Any"/> mean all. A game a
    /// guided path points at is never filtered out: the guide must always find the one door it leads to.
    /// </summary>
    public static class ArcadeGridOrder
    {
        public static List<SO_ArcadeGame> Apply(IEnumerable<SO_ArcadeGame> games, Element element, VesselClassType vessel,
                                                Func<SO_ArcadeGame, bool> isFavourite,
                                                Func<SO_ArcadeGame, Element, bool> hasGenre,
                                                Func<SO_ArcadeGame, bool> alwaysShown = null)
        {
            var list = new List<SO_ArcadeGame>();
            foreach (var g in games)
                if (g && ((alwaysShown != null && alwaysShown(g)) || Passes(g, element, vessel, hasGenre)))
                    list.Add(g);

            list.Sort((x, y) =>
            {
                int first = isFavourite(y).CompareTo(isFavourite(x));
                return first != 0 ? first : string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal);
            });
            return list;
        }

        static bool Passes(SO_ArcadeGame g, Element element, VesselClassType vessel, Func<SO_ArcadeGame, Element, bool> hasGenre) =>
            (element == Element.None || hasGenre(g, element)) && (vessel == VesselClassType.Any || Seats(g, vessel));

        /// <summary>Whether the card seats this vessel class.</summary>
        public static bool Seats(SO_ArcadeGame game, VesselClassType vessel)
        {
            if (!game || game.Vessels == null) return false;
            foreach (var v in game.Vessels)
                if (v && v.Class == vessel) return true;
            return false;
        }

        static ModePreviewLibrarySO s_previews;

        /// <summary>A mode's genre elements - the one answer the card's flower and the grid's element filter share.</summary>
        public static bool TryGenres(GameModes mode, out Element primary, out Element secondary)
        {
            if (!s_previews) s_previews = Resources.Load<ModePreviewLibrarySO>(ModePreviewLibrarySO.ResourcePath);
            var definition = s_previews ? s_previews.Resolve(mode) : null;
            ScoringMetric? metric = definition ? definition.ObjectiveMetric : (ScoringMetric?)null;
            return ModeGenre.TryElementsFor(mode, metric, out primary, out secondary);
        }

        public static bool HasGenre(SO_ArcadeGame game, Element element) =>
            game && TryGenres(game.Mode, out var a, out var b) && (a == element || b == element);
    }
}
