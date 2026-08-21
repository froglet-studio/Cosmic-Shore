using System;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Section a tool is filed under in the Froglet Master Tool board. Purely presentational -
    /// it decides which section of the board the tool sits in and its accent colour, never what the
    /// tool does.
    /// </summary>
    public enum FrogletToolCategory
    {
        /// <summary>Ship it: build pipeline, release gates.</summary>
        Build = 0,
        /// <summary>Scene / prefab authoring and setup.</summary>
        SceneSetup = 1,
        /// <summary>Game-mode construction and end conditions.</summary>
        GameModes = 2,
        /// <summary>Vessels, HUDs, abilities, elemental wiring.</summary>
        Vessels = 3,
        /// <summary>Prisms, cells, flora / fauna, ecology.</summary>
        Ecology = 4,
        /// <summary>Profiling, benchmarks, memory.</summary>
        Performance = 5,
        /// <summary>Asset auditing, validation, integrity checks.</summary>
        Validation = 6,
        /// <summary>UI, toasts, dialogue, canvases.</summary>
        Interface = 7,
        /// <summary>Backend, PlayFab, UGS, data plumbing.</summary>
        Services = 8,
        /// <summary>Everything else / uncategorised.</summary>
        Misc = 9,
        /// <summary>Editor health: crash watchdog, bug ledger, triage.</summary>
        Diagnostics = 10,
    }

    /// <summary>
    /// Optional metadata for a tool surfaced by the Froglet Master Tool.
    ///
    /// You do NOT need this attribute for a tool to appear - the registry discovers every
    /// <c>[MenuItem("FrogletTools/...")]</c> in the editor assembly automatically. Add it when you
    /// want the tool in a specific section, carrying a description, or ranked above its neighbours.
    ///
    /// Put it on the same static method that carries the <see cref="UnityEditor.MenuItem"/>
    /// attribute:
    /// <code>
    /// [MenuItem("FrogletTools/Game Modes/Game Mode Prefab Kit")]
    /// [FrogletTool(FrogletToolCategory.GameModes, Importance = 5,
    ///              Description = "Add and validate the prefabs every game-mode scene needs.")]
    /// static void Open() { ... }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class FrogletToolAttribute : Attribute
    {
        public FrogletToolAttribute(FrogletToolCategory category = FrogletToolCategory.Misc)
        {
            Category = category;
        }

        /// <summary>Section of the board the tool is drawn in.</summary>
        public FrogletToolCategory Category { get; }

        /// <summary>
        /// 1 (niche) .. 5 (load-bearing, used every day). Drives both the sort order inside a section
        /// and the five-dot rating shown on the card. Defaults to 3.
        /// </summary>
        public int Importance { get; set; } = 3;

        /// <summary>One-line explanation shown under the tool name.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Overrides the display name (defaults to the last segment of the menu path).
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Repo-relative path of the tool's documentation, optionally with a heading anchor —
        /// e.g. <c>"Docs/DIAGNOSTICS.md#bug-ledger"</c>. When set, the tool's card on the board
        /// grows a DOCS chip and a context-menu entry that open the page on GitHub
        /// (<see cref="FrogletDocLinks"/>; falls back to the local file when no remote resolves).
        /// The authoring contract (Docs/TOOLING.md) requires this on every new documented tool.
        /// </summary>
        public string DocPath { get; set; }
    }
}
