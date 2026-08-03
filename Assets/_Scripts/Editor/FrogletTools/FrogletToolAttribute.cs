using System;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Lane a tool is filed under in the Froglet Master Tool board. Purely presentational -
    /// it decides the swimlane and the accent colour, never what the tool does.
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
    }

    /// <summary>
    /// Optional metadata for a tool surfaced by the Froglet Master Tool.
    ///
    /// You do NOT need this attribute for a tool to appear - the registry discovers every
    /// <c>[MenuItem("FrogletTools/...")]</c> in the editor assembly automatically. Add it when you
    /// want the tool to sit in a specific lane, carry a description, or rank above its neighbours.
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

        /// <summary>Swimlane the tool is drawn in.</summary>
        public FrogletToolCategory Category { get; }

        /// <summary>
        /// 1 (niche) .. 5 (load-bearing, used every day). Drives both the sort order inside a lane
        /// and the length of the tool's bar on the board. Defaults to 3.
        /// </summary>
        public int Importance { get; set; } = 3;

        /// <summary>One-line explanation shown under the tool name.</summary>
        public string Description { get; set; }

        /// <summary>
        /// Overrides the display name (defaults to the last segment of the menu path).
        /// </summary>
        public string DisplayName { get; set; }
    }
}
