using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Everything about the guided-path spotlight (<see cref="MenuSpotlight"/>) that decides
    /// whether it is DRAWN, dumped from the live frame.
    ///
    /// <para><b>Why this exists.</b> The first-login guide blocked presses in the Unity editor
    /// (so the dim existed and its raycast filter ran) but drew no dim and no CTA frame, while
    /// the same code drew both in Prisma. "Is it on screen" is answered by the On-Screen UI
    /// report; this one answers "why is a Graphic that exists not drawn": the overlay canvas, the
    /// CanvasGroup, each spotlight Graphic's CanvasRenderer state and material, the cut-outs (and
    /// whether any is not finite), the mesh the dim and frame actually generate, every other
    /// overlay canvas sorted at or above it, and what a press on the target hits first.</para>
    ///
    /// <para>READER TOOL: reports only, writes no assets, so it carries no ship panel or change
    /// ledger (<c>Docs/TOOLING.md</c> § "Tool output is a deliverable").</para>
    /// </summary>
    public static class MenuSpotlightReport
    {
        [MenuItem("FrogletTools/Diagnostics/Report Menu Spotlight")]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 3,
                     Description = "In play mode, with the first-login guide's spotlight up: dump " +
                                   "why its dim and CTA frame are or are not drawn (canvas, alpha, " +
                                   "cut-outs, generated mesh, material, what a press hits).")]
        static void Run()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Report Menu Spotlight",
                    "Enter play mode, get to the frame where the guide's caption is showing, then run this again.",
                    "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== MENU SPOTLIGHT REPORT === screen {Screen.width}x{Screen.height}  " +
                          $"guide active={MenuGuide.IsActive} target={MenuGuide.TargetMode?.ToString() ?? "-"}");

            var spots = Object.FindObjectsByType<MenuSpotlight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"MenuSpotlight instances: {spots.Length}");
            foreach (var spot in spots) ReportSpotlight(sb, spot);

            ReportOverlayCanvases(sb);
            ReportAllowed(sb);

            Debug.Log(sb.ToString());
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log("[MenuSpotlightReport] Report copied to the clipboard.");
        }

        static void ReportSpotlight(StringBuilder sb, MenuSpotlight spot)
        {
            sb.AppendLine();
            sb.AppendLine($"-- {Path(spot.transform)}  active={spot.gameObject.activeInHierarchy} " +
                          $"layer={LayerMask.LayerToName(spot.gameObject.layer)} scene='{spot.gameObject.scene.name}'");

            if (spot.TryGetComponent(out Canvas canvas))
                sb.AppendLine($"   canvas: enabled={canvas.enabled} mode={canvas.renderMode} root={canvas.isRootCanvas} " +
                              $"order={canvas.sortingOrder} override={canvas.overrideSorting} " +
                              $"layer='{canvas.sortingLayerName}' display={canvas.targetDisplay} " +
                              $"scale={canvas.scaleFactor:0.###} pixelRect={canvas.pixelRect}");
            else
                sb.AppendLine("   canvas: NONE");

            if (spot.TryGetComponent(out CanvasGroup group))
                sb.AppendLine($"   group: alpha={group.alpha:0.###} blocksRaycasts={group.blocksRaycasts}");

            var rt = (RectTransform)spot.transform;
            sb.AppendLine($"   root rect={rt.rect} scale={rt.localScale} pos={rt.position}");

            foreach (var g in spot.GetComponentsInChildren<Graphic>(true))
            {
                var cr = g.canvasRenderer;
                var mat = g.materialForRendering;
                var shader = mat ? mat.shader : null;
                sb.AppendLine($"   [{g.GetType().Name}] '{g.name}' active={g.gameObject.activeInHierarchy} enabled={g.enabled} " +
                              $"color={g.color} raycast={g.raycastTarget} depth={g.depth} absDepth={cr.absoluteDepth}");
                sb.AppendLine($"        rect={g.rectTransform.rect} anchored={g.rectTransform.anchoredPosition} " +
                              $"cull={cr.cull} cullTransparent={cr.cullTransparentMesh} inheritedAlpha={cr.GetInheritedAlpha():0.###} " +
                              $"crColor={cr.GetColor()}");
                sb.AppendLine($"        material={(mat ? mat.name : "NONE")} shader={(shader ? shader.name : "NONE")} " +
                              $"supported={(shader ? shader.isSupported : false)} mainTex={(g.mainTexture ? g.mainTexture.name : "NONE")} " +
                              $"crMaterials={cr.materialCount}");

                if (g is SpotlightDimGraphic dim)
                {
                    var holes = dim.Holes;
                    sb.AppendLine($"        holes: {(holes == null ? "null" : holes.Count.ToString())}");
                    if (holes != null)
                        foreach (var h in holes)
                            sb.AppendLine($"          {h}{(Finite(h) ? "" : "   <-- NOT FINITE")}");

                    // The first cut-out is the target's. A press at its centre must fall through
                    // the dim to the real control.
                    if (holes is { Count: > 0 } && Finite(holes[0]))
                    {
                        var world = dim.rectTransform.TransformPoint(holes[0].center);
                        var screen = RectTransformUtility.WorldToScreenPoint(null, world);
                        bool passes = dim.IsRaycastLocationValid(screen, null) == false;
                        sb.AppendLine($"        press at target centre {screen}: dim lets it through={passes}");
                        ReportHits(sb, screen);
                    }
                }

                if (g is SpotlightDimGraphic or SpotlightFrameGraphic)
                    ReportMesh(sb, g);
            }
        }

        static readonly MethodInfo PopulateMesh = typeof(Graphic).GetMethod(
            "OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(VertexHelper) }, null);

        /// <summary>Regenerate the Graphic's mesh exactly as a canvas rebuild would, and describe it.</summary>
        static void ReportMesh(StringBuilder sb, Graphic g)
        {
            if (PopulateMesh == null)
            {
                sb.AppendLine("        mesh: (OnPopulateMesh(VertexHelper) not found)");
                return;
            }

            using var vh = new VertexHelper();
            PopulateMesh.Invoke(g, new object[] { vh });
            var verts = new List<UIVertex>();
            vh.GetUIVertexStream(verts);

            int bad = 0;
            byte minA = 255, maxA = 0;
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var v in verts)
            {
                if (!float.IsFinite(v.position.x) || !float.IsFinite(v.position.y) || !float.IsFinite(v.position.z)) { bad++; continue; }
                min = Vector2.Min(min, v.position);
                max = Vector2.Max(max, v.position);
                if (v.color.a < minA) minA = v.color.a;
                if (v.color.a > maxA) maxA = v.color.a;
            }
            sb.AppendLine($"        mesh: {vh.currentVertCount} verts, {verts.Count / 3} tris, non-finite verts={bad}" +
                          (verts.Count > bad ? $", bounds=({min.x:0.#},{min.y:0.#})..({max.x:0.#},{max.y:0.#}), vertex alpha {minA}..{maxA}" : ""));
        }

        static void ReportOverlayCanvases(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("-- ROOT CANVASES, highest order first (anything at or above the spotlight's 32000 draws over it) --");
            var canvases = new List<Canvas>(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            canvases.RemoveAll(c => !c.isRootCanvas);
            canvases.Sort((a, b) => b.sortingOrder.CompareTo(a.sortingOrder));
            foreach (var c in canvases)
                sb.AppendLine($"   order={c.sortingOrder,6} {c.renderMode,-20} enabled={c.enabled} display={c.targetDisplay} " +
                              $"cam={(c.worldCamera ? c.worldCamera.name : "-")}  {Path(c.transform)}");
        }

        static void ReportAllowed(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("-- ALWAYS-AVAILABLE CONTROLS (Settings) and what a press at each centre hits --");
            var allowed = new List<RectTransform>();
            MenuGuide.CollectAlwaysAvailable(allowed);
            if (allowed.Count == 0) sb.AppendLine("   (none found)");
            foreach (var a in allowed)
            {
                bool ok = MenuSpotlight.TryScreenRect(a, out var screen);
                sb.AppendLine($"   {Path(a)}  screen={(ok ? screen.ToString() : "not on screen")}" +
                              (ok && !Finite(screen) ? "   <-- NOT FINITE" : ""));
                if (ok) ReportHits(sb, screen.center);
            }
        }

        static readonly List<RaycastResult> Hits = new();

        static void ReportHits(StringBuilder sb, Vector2 at)
        {
            var es = EventSystem.current;
            if (!es)
            {
                sb.AppendLine("        (no EventSystem)");
                return;
            }
            Hits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = at }, Hits);
            for (int i = 0; i < Mathf.Min(4, Hits.Count); i++)
                sb.AppendLine($"        hit {i}: {Path(Hits[i].gameObject.transform)}");
            if (Hits.Count == 0) sb.AppendLine("        (nothing hit)");
        }

        static bool Finite(Rect r) =>
            float.IsFinite(r.x) && float.IsFinite(r.y) && float.IsFinite(r.width) && float.IsFinite(r.height);

        static string Path(Transform t)
        {
            var parts = new List<string>();
            for (var cur = t; cur != null; cur = cur.parent) parts.Add(cur.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
