using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the cell-change loading screen says and wears (Rev 7, Docs/MENU_REDESIGN_B.md §47.4; the owner, 2026-10-17:
    /// "Black loading screen when changing cell show something interesting"). <see cref="EnvironmentLoadVeil"/> is built
    /// in code, so it reads this one asset from Resources (<see cref="ResourcePath"/>). Every word is here: an empty field
    /// shows nothing.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentLoadVeilContent", menuName = "ScriptableObjects/UI/Environment Load Veil Content")]
    public sealed class EnvironmentLoadVeilContentSO : ScriptableObject
    {
        /// <summary>Where the veil loads this asset from (under a Resources folder).</summary>
        public const string ResourcePath = "UI/EnvironmentLoadVeilContent";

        /// <summary>A line under a cell's name.</summary>
        [Serializable]
        public struct CellLine
        {
            [Tooltip("The cell's name as its config states it (CellConfigDataSO.CellName).")]
            public string cellName;
            [Tooltip("One line under the name. Empty: no line.")]
            public string line;
        }

        [Header("Words")]
        [Tooltip("Over the cell's name.")]
        public string enteringWord = "Entering";
        [Tooltip("Over the slim bar at the bottom.")]
        public string loadingWord = "Loading";
        [Tooltip("Before the tip.")]
        public string tipLabel = "Tip";
        [Tooltip("The build's four steps, lit in turn, in their elements: Mass (laying), Time (growing), Charge " +
                 "(settling), Space (opening).")]
        public string[] stepLabels = { "Laying prisms", "Growing prisms", "Settling the cell", "Opening the cell" };
        [Tooltip("A line under each cell's name. A cell not listed shows its name alone.")]
        public CellLine[] cellLines = Array.Empty<CellLine>();
        [Tooltip("One is shown, at random, each time the screen comes up. Empty: no tip.")]
        public string[] tips = Array.Empty<string>();

        [Header("Look")]
        [Tooltip("The sea behind it all (Sea_Window.mat, Docs/MENU_REDESIGN_B.md §70). Empty: black.")]
        [FormerlySerializedAs("lattice")]
        public Material background;
        [Tooltip("The steps' glyphs, in step order: Mass, Time, Charge, Space.")]
        public Sprite[] stepGlyphs = Array.Empty<Sprite>();
        [Tooltip("The cell's name.")]
        public TMP_FontAsset displayFont;
        [Tooltip("Captions, steps and the tip.")]
        public TMP_FontAsset captionFont;

        /// <summary>The line authored for <paramref name="cellName"/>, or empty.</summary>
        public string LineFor(string cellName)
        {
            if (string.IsNullOrEmpty(cellName) || cellLines == null) return string.Empty;
            foreach (var entry in cellLines)
                if (string.Equals(entry.cellName, cellName, StringComparison.OrdinalIgnoreCase))
                    return entry.line ?? string.Empty;
            return string.Empty;
        }

        /// <summary>Step <paramref name="index"/>'s label, or empty.</summary>
        public string StepLabel(int index) =>
            stepLabels != null && index >= 0 && index < stepLabels.Length ? stepLabels[index] ?? string.Empty : string.Empty;

        /// <summary>Step <paramref name="index"/>'s glyph, or null.</summary>
        public Sprite StepGlyph(int index) =>
            stepGlyphs != null && index >= 0 && index < stepGlyphs.Length ? stepGlyphs[index] : null;

        /// <summary>A tip picked by <paramref name="roll"/> (0-1), or empty.</summary>
        public string TipAt(float roll)
        {
            if (tips == null || tips.Length == 0) return string.Empty;
            return tips[Mathf.Clamp((int)(roll * tips.Length), 0, tips.Length - 1)] ?? string.Empty;
        }
    }
}
