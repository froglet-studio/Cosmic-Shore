using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The standard element ladder's I4 rung, as a platform default (Garrett, 2026-11-16; <see cref="ElementLadder"/>):
    /// at I4, a cell whose own spawn profile carries NO fauna gets these species, every one born in the game's element
    /// and, like all cell fauna, in the cell's controlling colour (the locked one-colour rule). A cell that authors fauna
    /// of its own keeps exactly what it authored.
    ///
    /// <para>The species are Harvest's I4 set (Shombith, 2026-11-10: shark, piranha, brittle star, tadpole, 23 live at
    /// most), the one I4 fauna roster already play-tested against a mode's collider budget. Each is CLONED at runtime
    /// with its element pinned (<see cref="PinnedTo"/>), never edited, so Harvest and every other user of these assets
    /// is untouched. The clone order is the list order on every peer, so the fauna sync's config index means the same
    /// creature everywhere (<c>Cell.FaunaRoster</c>).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ElementLadderFauna", menuName = "ScriptableObjects/Game/Element Ladder Fauna")]
    public class ElementLadderFaunaSO : ScriptableObject
    {
        public const string ResourcePath = "ElementLadderFauna";

        [Tooltip("The species an I4 cell with no fauna of its own gets, each pinned to the game's element at runtime.")]
        public List<FaunaConfigurationSO> Species = new();

        static ElementLadderFaunaSO _cached;
        static bool _looked;

        /// <summary>The project's ladder fauna from Resources, or null.</summary>
        public static ElementLadderFaunaSO Load()
        {
            if (_looked) return _cached;
            _looked = true;
            _cached = Resources.Load<ElementLadderFaunaSO>(ResourcePath);
            return _cached;
        }

        /// <summary>
        /// A runtime copy of <paramref name="species"/> born only as <paramref name="element"/>: the element spread is
        /// switched off and, when the species' palette has a sibling for that element, that sibling's variant block (its
        /// body scale and tuning) is the copy's own. The caller owns and destroys the copy.
        /// </summary>
        public static FaunaConfigurationSO PinnedTo(FaunaConfigurationSO species, Element element)
        {
            var copy = Instantiate(species);
            copy.name = $"{species.name} (ladder {element})";
            var palette = species.ElementPalette;
            if (palette != null)
                for (int i = 0; i < palette.Count; i++)
                    if (palette[i] && palette[i].Element == element) { copy.Variant = palette[i].Variant; break; }
            copy.SpreadElements = false;
            copy.ElementPalette = new List<FaunaConfigurationSO>();
            copy.Element = element;
            return copy;
        }
    }
}
