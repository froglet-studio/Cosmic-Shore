using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Asset-side gate for the mass crystal's Shepard-tone screen door (Docs/SHEPARD_TONE.md).
    ///
    /// The effect has no scene wiring and no runtime component — every way it can be broken
    /// is an asset fact, and all of them are checked here:
    ///
    ///   1. ShepardToneDither.hlsl exists at the GUID the graph's Custom Function pins.
    ///   2. ShepardGraph carries the Custom Function node, fed by an OBJECT-space Position
    ///      and by _Start/_Stop, driving BOTH SurfaceDescription.Alpha and
    ///      SurfaceDescription.AlphaClipThreshold.
    ///   3. Every ShepardGraph material is OPAQUE with alpha clipping. A material left
    ///      transparent is the silent failure mode: it still gets the stipple (the shader
    ///      writes the threshold either way) but loses the depth ordering that is half the
    ///      reason to dither at all, so the crystal reads WORSE than before the change with
    ///      nothing in the console to say so.
    ///
    /// Checked against the graph TEXT rather than through Material.HasProperty/ShaderUtil:
    /// the same trap the corridor validator documents — a ShaderGraph property that is not
    /// exposed never enters the shader's property list, and a Custom Function node is not a
    /// property at all.
    ///
    /// READER ONLY — this tool reports and writes nothing, so it carries no
    /// FrogletToolChangeLedger / FrogletToolShipPanel (Docs/TOOLING.md § "Tool output is a
    /// deliverable"). The two things that DO write are repo-side Python, and their output
    /// is committed alongside them: Tools/Shaders/wire_shepard_tone_dither.py and
    /// Tools/Shaders/enable_shepard_alpha_clip.py.
    ///
    /// FrogletTools > Ecology > Prism Animation > Validate Shepard Tone Dither.
    /// </summary>
    public static class ShepardToneDitherValidator
    {
        const string GraphPath = "Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl";
        const string HlslGuid = "1af4b28d920441fd9ae968eaffac68c4";
        const string FunctionName = "ShepardToneDither";
        const string ShaderName = "Shader Graphs/ShepardGraph";
        const string AlphaTestKeyword = "_ALPHATEST_ON";

        // The Custom Function's input slots, by display name. Their presence in the graph
        // text is what proves the splice survived a merge or a hand-edit in the editor.
        static readonly string[] SlotNames =
            { "PositionOS", "BaseAlpha", "Start", "Stop", "Alpha", "ClipThreshold" };

        [MenuItem("FrogletTools/Ecology/Prism Animation/Validate Shepard Tone Dither")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 3,
            Description = "Mass-crystal Shepard tone - the four shells fade by screen-door coverage, and must stay opaque to keep their depth ordering.")]
        public static void Validate()
        {
            var report = new StringBuilder();
            report.AppendLine("[ShepardTone] MASS CRYSTAL SCREEN DOOR — Docs/SHEPARD_TONE.md");
            bool pass = true;

            // ---- 1. the HLSL asset, at the GUID the graph pins ----
            string guidOnDisk = AssetDatabase.AssetPathToGUID(HlslPath);
            if (string.IsNullOrEmpty(guidOnDisk))
            {
                report.AppendLine($"❌ {HlslPath} NOT FOUND");
                pass = false;
            }
            else if (guidOnDisk != HlslGuid)
            {
                report.AppendLine($"❌ {HlslPath} GUID drifted ({guidOnDisk} != {HlslGuid}) — the graph's Custom Function points at the old one");
                pass = false;
            }
            else
            {
                report.AppendLine($"✅ {HlslPath} (GUID pinned)");
            }

            // ---- 2. the graph splice ----
            if (!File.Exists(GraphPath))
            {
                report.AppendLine($"❌ {GraphPath} NOT FOUND");
                pass = false;
            }
            else
            {
                // Normalize CRLF before any block split — a Windows checkout otherwise
                // collapses the whole file into one block.
                string text = File.ReadAllText(GraphPath).Replace("\r\n", "\n");

                if (!text.Contains($"\"m_FunctionName\": \"{FunctionName}\""))
                {
                    report.AppendLine($"   ❌ {FunctionName} Custom Function MISSING — run `python3 Tools/Shaders/wire_shepard_tone_dither.py`");
                    pass = false;
                }
                else if (!text.Contains($"\"m_FunctionSource\": \"{HlslGuid}\""))
                {
                    report.AppendLine($"   ❌ {FunctionName} points at the wrong HLSL asset");
                    pass = false;
                }
                else
                {
                    var missing = SlotNames.Where(s => !text.Contains($"\"m_DisplayName\": \"{s}\"")).ToArray();
                    if (missing.Length > 0)
                    {
                        report.AppendLine($"   ❌ {FunctionName} slot(s) missing: {string.Join(", ", missing)} — signature drifted from the HLSL");
                        pass = false;
                    }
                    else
                    {
                        report.AppendLine($"   ✅ {FunctionName} node wired with all {SlotNames.Length} slots");
                    }
                }

                // The target must be OPAQUE. A transparent graph target hands every NEW
                // material the blended state this change exists to retire.
                if (text.Contains("\"m_SurfaceType\": 1"))
                {
                    report.AppendLine("   ❌ UniversalTarget is TRANSPARENT — the dither needs the opaque queue for its depth ordering");
                    pass = false;
                }
                else
                {
                    report.AppendLine("   ✅ UniversalTarget opaque");
                }
            }

            // ---- 3. every ShepardGraph material opaque + alpha-tested ----
            report.AppendLine("— ShepardGraph materials (all must be opaque + alpha-tested):");
            int ok = 0, failed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name != ShaderName) continue;

                bool transparent = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f;
                bool clipped = mat.IsKeywordEnabled(AlphaTestKeyword);
                if (transparent || !clipped)
                {
                    string fault = transparent
                        ? "still in the TRANSPARENT queue — it gets the stipple but no depth ordering, which reads worse than blending did"
                        : $"{AlphaTestKeyword} disabled — URP compiles the Alpha output away on an opaque surface, so the shell never thins at all";
                    report.AppendLine($"   ❌ {mat.name}: {fault}");
                    failed++;
                }
                else
                {
                    ok++;
                }
            }
            report.AppendLine(failed == 0
                ? $"   ✅ {ok} material(s) on contract"
                : $"   ❌ {failed} material(s) off contract — run `python3 Tools/Shaders/enable_shepard_alpha_clip.py`");
            pass &= failed == 0;

            if (ok + failed == 0)
            {
                report.AppendLine("   ❌ no ShepardGraph materials found at all — did the shader name change?");
                pass = false;
            }

            report.AppendLine(pass ? "\nRESULT: ✅ PASS" : "\nRESULT: ❌ FAIL");
            if (pass) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }
    }
}
