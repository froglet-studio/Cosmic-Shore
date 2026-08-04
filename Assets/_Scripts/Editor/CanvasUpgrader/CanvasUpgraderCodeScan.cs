using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Greps first-party C# for code that writes RectTransform / text metrics (sizeDelta,
    /// anchoredPosition, offsets, fontSize, SetSizeWithCurrentAnchors, ...) and writes a markdown
    /// report to <c>Assets/CanvasUpgrader/CodeReferences.md</c> so hardcoded 800x450-era constants
    /// can be fixed by hand after the canvas upgrade. Deliberately report-only: the Canvas
    /// Upgrader never auto-edits code.
    /// </summary>
    public static class CanvasUpgraderCodeScan
    {
        /// <summary>Where the markdown report is written.</summary>
        public const string ReportPath = "Assets/CanvasUpgrader/CodeReferences.md";

        // First-party code roots; third-party trees (Plugins, PlayFabSDK, Wwise, NiceVibrations, ...)
        // are excluded by not being listed.
        static readonly string[] ScanRoots = { "Assets/_Scripts", "Assets/FTUE", "Assets/Editor" };

        // This tool's own sources legitimately mention every scanned property name.
        const string SelfFolder = "/Editor/CanvasUpgrader/";

        static readonly Regex PropertyWrite = new(
            @"\.(sizeDelta|anchoredPosition3D|anchoredPosition|offsetMin|offsetMax|fontSizeMin|fontSizeMax|fontSize)\s*[+\-*/]?=(?!=)",
            RegexOptions.Compiled);

        static readonly Regex MethodCall = new(
            @"\b(SetSizeWithCurrentAnchors|SetInsetAndSizeFromParentEdge)\s*\(",
            RegexOptions.Compiled);

        static readonly Regex HasDigit = new(@"\d", RegexOptions.Compiled);

        // `foo = Vector2.zero;` style resets are resolution-independent by definition.
        static readonly Regex ZeroAssignment = new(@"=\s*Vector[234]\.zero\s*[;,)]", RegexOptions.Compiled);

        /// <summary>
        /// Runs the scan and writes <see cref="ReportPath"/>. Returns a one-line summary for the
        /// window report / console.
        /// </summary>
        public static string Generate()
        {
            var hardcoded = new List<string>();   // markdown lines, grouped by file
            var dynamicWrites = new List<string>();
            int hardcodedCount = 0, dynamicCount = 0, fileCount = 0;

            foreach (var root in ScanRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (path.Contains(SelfFolder)) continue;

                    List<string> fileHardcoded = null;
                    List<string> fileDynamic = null;
                    string[] lines = File.ReadAllLines(path);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        string trimmed = line.TrimStart();
                        if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("///")) continue;
                        if (!PropertyWrite.IsMatch(line) && !MethodCall.IsMatch(line)) continue;
                        if (ZeroAssignment.IsMatch(line)) continue;

                        string entry = $"- Line {i + 1}: `{trimmed.TrimEnd()}`";
                        if (HasDigit.IsMatch(trimmed))
                        {
                            (fileHardcoded ??= new List<string>()).Add(entry);
                            hardcodedCount++;
                        }
                        else
                        {
                            (fileDynamic ??= new List<string>()).Add(entry);
                            dynamicCount++;
                        }
                    }

                    if (fileHardcoded != null)
                    {
                        hardcoded.Add($"\n### {path}");
                        hardcoded.AddRange(fileHardcoded);
                    }
                    if (fileDynamic != null)
                    {
                        dynamicWrites.Add($"\n### {path}");
                        dynamicWrites.AddRange(fileDynamic);
                    }
                    if (fileHardcoded != null || fileDynamic != null) fileCount++;
                }
            }

            var md = new StringBuilder();
            md.AppendLine("# Canvas Upgrader — Code References Report");
            md.AppendLine();
            md.AppendLine($"Generated {System.DateTime.Now:yyyy-MM-dd HH:mm} for the 800x450 -> 1920x1080 reference-resolution migration (scale x{CanvasUpgradeProcessor.UpgradeScale}).");
            md.AppendLine();
            md.AppendLine("These code sites write RectTransform / text metrics from C#. Values authored in the old");
            md.AppendLine("canvas space must be multiplied by 2.4 **by hand** — the Canvas Upgrader never edits code.");
            md.AppendLine("`= VectorN.zero` resets are omitted (resolution-independent). Sites on canvases that were");
            md.AppendLine("never 800x450 (e.g. the ConstantPixelSize DiagnosticsHUD overlay) can be ignored.");
            md.AppendLine();
            md.AppendLine($"## Hardcoded numeric constants — fix these ({hardcodedCount})");
            if (hardcoded.Count == 0) md.AppendLine("\n(none found)");
            foreach (var line in hardcoded) md.AppendLine(line);
            md.AppendLine();
            md.AppendLine($"## Dynamic writes — verify the data source ({dynamicCount})");
            md.AppendLine();
            md.AppendLine("No literal on the line, but the serialized field / SO asset / computed value feeding the");
            md.AppendLine("write may still hold old-space pixels. Many are safe (measured rects, relative math) — triage.");
            foreach (var line in dynamicWrites) md.AppendLine(line);

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, md.ToString());
            AssetDatabase.ImportAsset(ReportPath);
            var asset = AssetDatabase.LoadAssetAtPath<Object>(ReportPath);
            if (asset) EditorGUIUtility.PingObject(asset);

            return $"Code reference report: {hardcodedCount} hardcoded + {dynamicCount} dynamic write(s) across {fileCount} file(s) -> {ReportPath}";
        }
    }
}
