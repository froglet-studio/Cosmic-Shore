using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Name every piece of UI that is actually ON SCREEN right now, biggest first.
    ///
    /// <para><b>Why this exists.</b> "There is UI in the way" is a report about a rendered frame,
    /// and a rendered frame is the one thing static analysis of scenes and prefabs cannot see. A
    /// panel is on screen because of the product of several things that live in different files -
    /// is its GameObject active, is its Graphic enabled, what did every CanvasGroup above it
    /// multiply its alpha by, which Canvas draws it and in what order, and did some controller
    /// leave it that way - and reading any one of them in isolation invites a confident wrong
    /// answer. Three of those in a row is what this tool is a reaction to.</para>
    ///
    /// <para><b>Use it while the bad frame is on screen.</b> Enter play mode, get to the state
    /// where the unwanted UI is showing, then run this. It reports every enabled
    /// <see cref="Graphic"/> whose screen rect covers at least <see cref="MinCoverage"/> of the
    /// display, with the full hierarchy path, the effective alpha and WHICH CanvasGroup set it,
    /// whether it eats clicks, its texture/sprite, and its canvas. The offender is normally the
    /// first line.</para>
    ///
    /// <para>Two things that are not Graphics get their own sections, because both draw over the
    /// game and neither shows up in a UI hierarchy: cameras rendering into a RenderTexture or
    /// restricted to a partial viewport rect, and VideoPlayers set to draw on a camera plane.</para>
    ///
    /// <para>READER TOOL: reports only, writes no assets, so it carries no ship panel or change
    /// ledger (<c>Docs/TOOLING.md</c> § "Tool output is a deliverable").</para>
    /// </summary>
    public static class OnScreenUIReport
    {
        /// <summary>Ignore anything smaller than this fraction of the screen - the question is
        /// "what is IN THE WAY", and a 2% badge never is.</summary>
        const float MinCoverage = 0.02f;

        [MenuItem("FrogletTools/Diagnostics/Report On-Screen UI")]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
                     Description = "In play mode: name every UI element actually covering the " +
                                   "screen right now, biggest first, with its alpha, its owning " +
                                   "CanvasGroup and its canvas. Run it while the bad frame is up.")]
        static void Run()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Report On-Screen UI",
                    "Enter play mode first, get to the frame where the unwanted UI is showing, " +
                    "then run this again.\n\nThis reads what is actually rendered - outside play " +
                    "mode there is no frame to read.",
                    "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== ON-SCREEN UI REPORT === {Screen.width}x{Screen.height}  " +
                          $"timeScale={Time.timeScale}");

            ReportGraphics(sb);
            ReportCameras(sb);
            ReportVideoPlayers(sb);

            Debug.Log(sb.ToString());
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log("[OnScreenUIReport] Report copied to the clipboard.");
        }

        static void ReportGraphics(StringBuilder sb)
        {
            var graphics = UnityEngine.Object
                .FindObjectsByType<Graphic>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var rows = new List<(float coverage, string line)>();

            foreach (var g in graphics)
            {
                if (!g || !g.enabled || !g.isActiveAndEnabled) continue;

                var canvas = g.canvas;
                if (!canvas) continue;

                float alpha = EffectiveAlpha(g.transform, out string dimmedBy);
                alpha *= g.color.a;
                if (alpha <= 0.01f) continue;

                if (!TryScreenRect(g.rectTransform, canvas, out Rect r)) continue;

                float coverage = ScreenCoverage(r);
                if (coverage < MinCoverage) continue;

                rows.Add((coverage,
                    $"  {coverage * 100f,5:0.0}%  a={alpha:0.00}  " +
                    $"{(g.raycastTarget ? "RAYCASTS" : "        ")}  " +
                    $"{g.GetType().Name,-14} {Describe(g),-28} " +
                    $"rect=({r.x:0},{r.y:0},{r.width:0}x{r.height:0})\n" +
                    $"            path: {Path(g.transform)}\n" +
                    $"            canvas: {canvas.name} ({canvas.renderMode}, order {canvas.sortingOrder})" +
                    (dimmedBy != null ? $"   alpha set by: {dimmedBy}" : "")));
            }

            sb.AppendLine();
            sb.AppendLine($"-- VISIBLE UI covering >= {MinCoverage * 100f:0}% of the screen " +
                          $"({rows.Count}) , biggest first --");
            if (rows.Count == 0)
                sb.AppendLine("  (none - whatever is in the way is not a UI Graphic; see the sections below)");
            foreach (var row in rows.OrderByDescending(x => x.coverage))
                sb.AppendLine(row.line);
        }

        static void ReportCameras(StringBuilder sb)
        {
            var cams = UnityEngine.Object
                .FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            sb.AppendLine();
            sb.AppendLine("-- CAMERAS (a partial viewport rect, or a leftover targetTexture, " +
                          "draws a band the UI hierarchy cannot explain) --");
            foreach (var c in cams.OrderByDescending(c => c.depth))
            {
                bool partialRect = Mathf.Abs(c.rect.x) > 0.001f || Mathf.Abs(c.rect.y) > 0.001f ||
                                   Mathf.Abs(c.rect.width - 1f) > 0.001f ||
                                   Mathf.Abs(c.rect.height - 1f) > 0.001f;
                string flags = (partialRect ? "  <-- PARTIAL VIEWPORT" : "") +
                               (c.targetTexture ? "  <-- RENDERS TO TEXTURE" : "");
                sb.AppendLine($"  {(c.enabled ? "on " : "off")} depth={c.depth,6:0.#}  " +
                              $"rect=({c.rect.x:0.00},{c.rect.y:0.00},{c.rect.width:0.00}x{c.rect.height:0.00})  " +
                              $"clear={c.clearFlags}/{ColorText(c.backgroundColor)}  " +
                              $"mask=0x{c.cullingMask:X}  target={(c.targetTexture ? c.targetTexture.name : "-")}" +
                              flags);
                sb.AppendLine($"        path: {Path(c.transform)}");
            }
        }

        static void ReportVideoPlayers(StringBuilder sb)
        {
            var players = UnityEngine.Object
                .FindObjectsByType<VideoPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            sb.AppendLine();
            sb.AppendLine("-- VIDEO PLAYERS (a camera-plane render mode draws over the game and " +
                          "is in no canvas) --");
            if (players.Length == 0) sb.AppendLine("  (none)");
            foreach (var v in players)
                sb.AppendLine($"  {(v.isPlaying ? "PLAYING" : "stopped")}  mode={v.renderMode}  " +
                              $"target={(v.targetTexture ? v.targetTexture.name : "-")}  " +
                              $"path: {Path(v.transform)}");
        }

        /// <summary>
        /// The alpha a graphic actually renders at, and the name of the LAST CanvasGroup that
        /// changed it - which is the thing you have to go and fix. Honours
        /// <c>ignoreParentGroups</c>, because a group that ignores its parents is where the walk
        /// really stops.
        /// </summary>
        static float EffectiveAlpha(Transform t, out string dimmedBy)
        {
            float alpha = 1f;
            dimmedBy = null;

            for (var cur = t; cur != null; cur = cur.parent)
            {
                if (!cur.TryGetComponent(out CanvasGroup group)) continue;

                if (group.alpha < 0.999f) dimmedBy = $"{Path(group.transform)} (alpha {group.alpha:0.00})";
                alpha *= group.alpha;
                if (group.ignoreParentGroups) break;
            }

            return alpha;
        }

        static bool TryScreenRect(RectTransform rt, Canvas canvas, out Rect rect)
        {
            rect = default;
            if (!rt) return false;

            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in corners)
            {
                var p = RectTransformUtility.WorldToScreenPoint(cam, c);
                if (float.IsNaN(p.x) || float.IsNaN(p.y)) return false;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }

            rect = new Rect(minX, minY, maxX - minX, maxY - minY);
            return true;
        }

        /// <summary>Fraction of the DISPLAY covered - clipped to the screen, so an element that
        /// overhangs is not reported as bigger than the thing it is covering.</summary>
        static float ScreenCoverage(Rect r)
        {
            float w = Mathf.Max(0f, Mathf.Min(r.xMax, Screen.width) - Mathf.Max(r.xMin, 0f));
            float h = Mathf.Max(0f, Mathf.Min(r.yMax, Screen.height) - Mathf.Max(r.yMin, 0f));
            return w * h / Mathf.Max(1f, Screen.width * (float)Screen.height);
        }

        static string Describe(Graphic g) => g switch
        {
            RawImage raw => raw.texture ? $"tex:{raw.texture.name}" : "tex:NONE",
            Image img    => img.sprite ? $"sprite:{img.sprite.name}" : $"flat:{ColorText(img.color)}",
            _            => g.name
        };

        static string ColorText(Color c) =>
            $"#{Mathf.RoundToInt(c.r * 255):X2}{Mathf.RoundToInt(c.g * 255):X2}{Mathf.RoundToInt(c.b * 255):X2}";

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
