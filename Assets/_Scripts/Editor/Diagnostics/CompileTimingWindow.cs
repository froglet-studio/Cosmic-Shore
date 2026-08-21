using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The switch and the readout for <see cref="CompileTimingMonitor"/>. Deliberately small: the
    /// measurement protocol lives in Docs/ASSEMBLY_SPLIT.md, not in a wizard.
    /// </summary>
    public sealed class CompileTimingWindow : EditorWindow
    {
        Vector2 _scroll;

        public static void Open()
        {
            var window = GetWindow<CompileTimingWindow>(true, "Compile Timing");
            window.minSize = new Vector2(560f, 320f);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Compile Timing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Records compile seconds + domain-reload seconds for every edit, and which " +
                "assemblies Unity rebuilt. Enable it, make the same one-line edit a few times, " +
                "then disable it. Protocol: Docs/ASSEMBLY_SPLIT.md § Measuring.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            var enabled = EditorGUILayout.ToggleLeft(
                "Recording enabled (this machine)", CompileTimingMonitor.Enabled);
            if (EditorGUI.EndChangeCheck())
                CompileTimingMonitor.Enabled = enabled;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Log", CompileTimingMonitor.LogPath);

            var exists = File.Exists(CompileTimingMonitor.LogPath);
            using (new EditorGUI.DisabledScope(!exists))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reveal"))
                        EditorUtility.RevealInFinder(CompileTimingMonitor.LogPath);

                    if (GUILayout.Button("Clear") && EditorUtility.DisplayDialog(
                            "Clear compile timing log",
                            $"Delete {CompileTimingMonitor.LogPath}?", "Delete", "Cancel"))
                    {
                        File.Delete(CompileTimingMonitor.LogPath);
                    }
                }
            }

            EditorGUILayout.Space(6f);
            if (!exists)
            {
                EditorGUILayout.LabelField(
                    "No cycles recorded yet.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(CompileTimingMonitor.LogPath);
            }
            catch (IOException e)
            {
                EditorGUILayout.HelpBox(e.Message, MessageType.Warning);
                return;
            }

            var rows = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            EditorGUILayout.LabelField($"{rows.Length} cycle(s) recorded", EditorStyles.miniBoldLabel);
            DrawMedian(rows);

            EditorGUILayout.Space(4f);
            using var scroll = new EditorGUILayout.ScrollViewScope(_scroll);
            _scroll = scroll.scrollPosition;
            foreach (var row in rows.Reverse().Take(50))
                EditorGUILayout.LabelField(row, EditorStyles.miniLabel);
        }

        // Median rather than mean: the first compile of a session and any compile that raced a
        // background import are outliers big enough to swamp an average over a handful of samples.
        static void DrawMedian(IReadOnlyCollection<string> rows)
        {
            var totals = rows
                .Select(r => r.Split(','))
                .Where(c => c.Length >= 4)
                .Select(c => double.TryParse(
                    c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
                .Where(v => !double.IsNaN(v))
                .OrderBy(v => v)
                .ToArray();

            if (totals.Length == 0) return;

            var median = totals.Length % 2 == 1
                ? totals[totals.Length / 2]
                : (totals[totals.Length / 2 - 1] + totals[totals.Length / 2]) / 2.0;

            EditorGUILayout.LabelField(
                $"Median total: {median:F2}s   (min {totals[0]:F2}s, max {totals[^1]:F2}s)",
                EditorStyles.miniLabel);
        }
    }
}
