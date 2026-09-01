using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The <b>2D face of a toy</b>, for the app shell's Toy Box.
    ///
    /// <para>It resolves the portrait the in-game encyclopedia already bakes rather than authoring
    /// a second set of icons: <c>FrogletTools &gt; Interface &gt; Codex</c> draws every toy from
    /// its own <c>ToyEmblem</c> grammar (core + satellites inside the switch ring) and bakes the
    /// result to <c>Assets/_Graphics/Codex/</c>, so the flat card in the menu is a picture of the
    /// same emblem the player flies at in freestyle - and re-baking the emblem re-skins the menu
    /// with nothing to re-wire. See <c>Docs/CODEX.md</c> §3.5.</para>
    ///
    /// <para>Matched on the toy's own DEFINITION ASSET (<see cref="CodexEntry.SourceConfig"/>),
    /// never on a name: a toy has no prefab, and the config is the thing the codex harvester
    /// itself keyed the entry on. The display-name pass is a fallback for the code-built default
    /// toybox, whose definitions are <c>CreateInstance</c>d at runtime and so match no asset
    /// reference at all - it degrades to no portrait rather than to the wrong one.</para>
    /// </summary>
    public static class ToyPortraitLibrary
    {
        /// <summary>
        /// The codex page for a toy, or null when the codex has no entry for it (a toy authored
        /// since the last codex scan, or a runtime-synthesised default).
        /// </summary>
        public static CodexEntry Entry(ToyDefinitionSO definition)
        {
            if (!definition) return null;

            var codex = CodexSO.Load();
            if (!codex) return null;

            var tools = codex.EntriesOf(CodexKingdom.Tool);

            for (int i = 0; i < tools.Count; i++)
                if (tools[i] != null && tools[i].SourceConfig == definition) return tools[i];

            // Fallback for a definition with no asset behind it - see the class remarks.
            for (int i = 0; i < tools.Count; i++)
                if (tools[i] != null &&
                    string.Equals(tools[i].DisplayName, definition.DisplayName,
                                  System.StringComparison.OrdinalIgnoreCase))
                    return tools[i];

            return null;
        }

        /// <summary>The toy's baked portrait, or null. A card with none draws its accent instead.</summary>
        public static Sprite Portrait(ToyDefinitionSO definition) => Entry(definition)?.Image;

        /// <summary>
        /// The toy's one-line description: the codex tagline when there is one, else the
        /// definition's own. Both are authored, so neither is a guess.
        /// </summary>
        public static string Tagline(ToyDefinitionSO definition)
        {
            var entry = Entry(definition);
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Tagline)) return entry.Tagline;
            return definition ? definition.Description : "";
        }

        /// <summary>
        /// The section a toy belongs to - the FUNDAMENTAL it changes (Pilot / World / Creation).
        /// Read off <see cref="ToyDefinitionSO.Category"/>, which is declared in code on each toy
        /// and therefore cannot disagree with the behaviour underneath it; the codex's own Group
        /// string is derived from the same place and carries an ordering prefix this does not
        /// need.
        /// </summary>
        public static string Section(ToyDefinitionSO definition) =>
            definition == null ? "" : Section(definition.Category);

        /// <summary>The player-facing name of a toy category.</summary>
        public static string Section(ToyCategory category) => category switch
        {
            ToyCategory.Pilot => "Pilot",
            ToyCategory.World => "World",
            ToyCategory.Creation => "Creation",
            _ => "",
        };
    }
}
