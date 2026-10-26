using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>How the Arcade's ALL GAMES grid is ordered (Docs/MENU_REDESIGN_B.md section 85). Values are serialized
    /// nowhere, but they index the authored option names, so they stay explicit.</summary>
    public enum ArcadeGridSort
    {
        FavouritesFirst = 0,   // favourites, then A-Z: the grid's order before section 85
        AToZ = 1,
        UnlockedFirst = 2,
    }

    /// <summary>Which games the ALL GAMES grid shows (section 85). Charge..Time keep their order so the element is
    /// <c>filter - Charge + Element.Charge</c>.</summary>
    public enum ArcadeGridFilter
    {
        All = 0,
        Favourites = 1,
        Unlocked = 2,
        Solo = 3,          // one player at most
        Multiplayer = 4,   // more than one player
        Charge = 5,        // the card's genre (ModeGenre: the element its flower shows)
        Mass = 6,
        Space = 7,
        Time = 8,
    }

    /// <summary>
    /// The ALL GAMES grid's sort and filter, as a pure function of the roster and three questions about a game, so the
    /// view owns no ordering rule of its own and the rules can be tested without a scene. A game a guided path points
    /// at is never filtered out: the guide must always find the one door it leads to.
    /// </summary>
    public static class ArcadeGridOrder
    {
        public static List<SO_ArcadeGame> Apply(IEnumerable<SO_ArcadeGame> games, ArcadeGridSort sort, ArcadeGridFilter filter,
                                                Func<SO_ArcadeGame, bool> isFavourite, Func<SO_ArcadeGame, bool> isUnlocked,
                                                Func<SO_ArcadeGame, Element, bool> hasGenre,
                                                Func<SO_ArcadeGame, bool> alwaysShown = null)
        {
            var list = new List<SO_ArcadeGame>();
            foreach (var g in games)
                if (g && ((alwaysShown != null && alwaysShown(g)) || Passes(g, filter, isFavourite, isUnlocked, hasGenre)))
                    list.Add(g);

            list.Sort((x, y) =>
            {
                int first = sort switch
                {
                    ArcadeGridSort.FavouritesFirst => isFavourite(y).CompareTo(isFavourite(x)),
                    ArcadeGridSort.UnlockedFirst => isUnlocked(y).CompareTo(isUnlocked(x)),
                    _ => 0,
                };
                return first != 0 ? first : string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal);
            });
            return list;
        }

        static bool Passes(SO_ArcadeGame g, ArcadeGridFilter filter, Func<SO_ArcadeGame, bool> isFavourite,
                           Func<SO_ArcadeGame, bool> isUnlocked, Func<SO_ArcadeGame, Element, bool> hasGenre) => filter switch
        {
            ArcadeGridFilter.Favourites => isFavourite(g),
            ArcadeGridFilter.Unlocked => isUnlocked(g),
            ArcadeGridFilter.Solo => g.MaxPlayersAllowed <= 1,
            ArcadeGridFilter.Multiplayer => g.MaxPlayersAllowed > 1,
            >= ArcadeGridFilter.Charge and <= ArcadeGridFilter.Time =>
                hasGenre(g, Element.Charge + (filter - ArcadeGridFilter.Charge)),
            _ => true,
        };

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
