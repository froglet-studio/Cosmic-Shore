using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Core logic for the Canvas Upgrader window (<see cref="CanvasUpgraderWindow"/>): scans one
    /// scene (or the open prefab stage) for screen-space canvases still authored at the mobile
    /// 800x450 reference resolution and converts them to 1920x1080 with pixel-identical rendered
    /// output — every canvas-space value multiplies by 1080/450 = 2.4.
    ///
    /// Scaling model: anchors are normalized, so they migrate for free. Scaling anchoredPosition
    /// and sizeDelta by k is exactly equivalent to scaling offsetMin and offsetMax by k
    /// (offsetMin = anchoredPosition - sizeDelta*pivot, offsetMax = anchoredPosition +
    /// sizeDelta*(1-pivot); both maps are linear), so one code path is correct for stretched,
    /// non-stretched, and per-axis mixed anchoring alike.
    ///
    /// Nested canvases (verified against uGUI source): CanvasScaler.Handle() early-outs unless
    /// the canvas is the ROOT canvas, so a CanvasScaler on a nested canvas is inert and nested
    /// canvases inherit the root's scale factor. Their RectTransforms live in the same authored
    /// unit space and are scaled together with everything else under the root.
    ///
    /// CanvasScaler.referencePixelsPerUnit is scaled too (100 -> 240): sliced/tiled Image borders
    /// render at border_px * refPPU / (spritePPU * multiplier) * canvasScaleFactor screen pixels,
    /// and the upgrade divides canvasScaleFactor by 2.4 — without the refPPU bump every 9-sliced
    /// border and tiled pattern would shrink 2.4x.
    /// </summary>
    public static class CanvasUpgradeProcessor
    {
        /// <summary>Multiplier applied to every canvas-space value: 1080 / 450.</summary>
        public const float UpgradeScale = 2.4f;

        /// <summary>The legacy mobile reference resolution being migrated away from.</summary>
        public static readonly Vector2 OldResolution = new(800f, 450f);

        /// <summary>The PC-ready reference resolution being migrated to.</summary>
        public static readonly Vector2 NewResolution = new(1920f, 1080f);

        // LayoutElement.flexibleWidth/Height are normally dimensionless weights (0..~10); values
        // above this look like pixels misused as flexible sizes and get scaled + reported.
        const float FlexiblePixelThreshold = 10f;

        const string UndoLabel = "Canvas Upgrade 800x450 -> 1920x1080";

        /// <summary>One scanned root canvas plus everything the window needs to display and act on it.</summary>
        public class CanvasEntry
        {
            public Canvas Canvas;
            public CanvasScaler Scaler;
            public int ChildRectCount;
            public int RaycastTargetsOn;
            public bool AlreadyUpgraded;      // already at 1920x1080 — eligible for re-anchor/anim only
            public bool IsPrefabInstance;
            public string PrefabAssetPath;
            public readonly List<string> Notes = new();
            public bool Selected = true;
        }

        /// <summary>Per-run tallies for the end-of-upgrade summary log.</summary>
        public class UpgradeCounters
        {
            public int Canvases, RectTransforms, TmpTexts, LegacyTexts, LayoutGroups, GridGroups,
                       LayoutElements, Shadows, RectMasks, RoundedInts, FlexibleLeftAsWeights,
                       FlexibleScaled, AdaptiveAdded;
        }

        /// <summary>A scene animation clip curve that animates anchoredPosition/sizeDelta under a scanned canvas.</summary>
        public class AnimatedBindingHit
        {
            public AnimationClip Clip;
            public string ClipAssetPath;
            public EditorCurveBinding Binding;
            public string HostPath;           // scene path of the Animator/Animation component
            public string CanvasName;

            // GUID + clip name, not instance ID: the key must be stable across editor sessions so
            // the scaled-curve log can prevent double-scaling; clip name disambiguates sub-asset
            // clips (e.g. inside an FBX) that share one path/GUID.
            public string CurveKey =>
                string.IsNullOrEmpty(ClipAssetPath)
                    ? $"{Clip.GetInstanceID()}|{Binding.path}|{Binding.propertyName}"
                    : $"{AssetDatabase.AssetPathToGUID(ClipAssetPath)}:{Clip.name}|{Binding.path}|{Binding.propertyName}";
        }

        // ------------------------------------------------------------------
        //  Scan
        // ------------------------------------------------------------------

        /// <summary>
        /// Finds every ROOT screen-space canvas in <paramref name="scene"/> whose CanvasScaler is
        /// Scale-With-Screen-Size at 800x450 (upgradeable) or 1920x1080 (re-anchor/anim eligible).
        /// Everything else lands in <paramref name="skipped"/> with a reason.
        /// </summary>
        public static List<CanvasEntry> Scan(Scene scene, List<string> skipped)
        {
            var entries = new List<CanvasEntry>();
            skipped.Clear();

            foreach (var rootGo in scene.GetRootGameObjects())
            {
                foreach (var canvas in rootGo.GetComponentsInChildren<Canvas>(true))
                {
                    // Nested canvases are handled as part of their root's traversal.
                    if (canvas.GetComponentsInParent<Canvas>(true).Length > 1) continue;

                    string path = PathOf(canvas.transform, null);
                    if (canvas.renderMode == RenderMode.WorldSpace)
                    {
                        skipped.Add($"{path} — world-space canvas");
                        continue;
                    }
                    if (!canvas.TryGetComponent(out CanvasScaler scaler))
                    {
                        skipped.Add($"{path} — no CanvasScaler");
                        continue;
                    }
                    if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    {
                        skipped.Add($"{path} — scale mode {scaler.uiScaleMode} (not ScaleWithScreenSize)");
                        continue;
                    }

                    bool isOld = Approximately(scaler.referenceResolution, OldResolution);
                    bool isNew = Approximately(scaler.referenceResolution, NewResolution);
                    if (!isOld && !isNew)
                    {
                        skipped.Add($"{path} — reference resolution {Fmt(scaler.referenceResolution)} (not 800x450)");
                        continue;
                    }

                    var entry = new CanvasEntry
                    {
                        Canvas = canvas,
                        Scaler = scaler,
                        AlreadyUpgraded = isNew,
                        ChildRectCount = canvas.GetComponentsInChildren<RectTransform>(true).Length - 1,
                    };

                    foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
                        if (graphic.raycastTarget)
                            entry.RaycastTargetsOn++;

                    if (PrefabUtility.IsPartOfPrefabInstance(canvas.gameObject))
                    {
                        entry.IsPrefabInstance = true;
                        entry.PrefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(canvas.gameObject);
                    }

                    foreach (var nested in canvas.GetComponentsInChildren<Canvas>(true))
                    {
                        if (nested == canvas) continue;
                        entry.Notes.Add(nested.TryGetComponent(out CanvasScaler _)
                            ? $"nested canvas '{PathOf(nested.transform, canvas.transform)}' carries an INERT CanvasScaler (only the root scaler applies); its children are value-scaled with the rest"
                            : $"nested canvas '{PathOf(nested.transform, canvas.transform)}' — children value-scaled with the rest");
                    }

                    entries.Add(entry);
                }
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.Canvas.name, b.Canvas.name));
            return entries;
        }

        // ------------------------------------------------------------------
        //  Upgrade (shared by Dry Run and Upgrade — apply flag decides)
        // ------------------------------------------------------------------

        /// <summary>
        /// Reports (and with <paramref name="apply"/> performs, Undo-recorded) the x2.4 conversion
        /// of every selected upgradeable canvas. The caller owns the Undo group and the single
        /// <c>Canvas.ForceUpdateCanvases()</c> afterwards.
        /// </summary>
        public static string Upgrade(List<CanvasEntry> entries, bool apply, bool addAdaptiveScaler, UpgradeCounters counters)
        {
            var report = new StringBuilder();
            report.AppendLine(apply
                ? $"UPGRADE REPORT — {Fmt(OldResolution)} -> {Fmt(NewResolution)}, all values x{UpgradeScale}"
                : $"DRY RUN — no changes applied. Every value below WOULD change ({Fmt(OldResolution)} -> {Fmt(NewResolution)}, x{UpgradeScale}).");

            foreach (var entry in entries)
            {
                if (entry == null || !entry.Canvas || !entry.Selected) continue;
                if (entry.AlreadyUpgraded)
                {
                    report.AppendLine($"\n=== Canvas '{entry.Canvas.name}' — already 1920x1080, skipped (re-anchor/animation actions still available) ===");
                    continue;
                }
                UpgradeCanvas(entry, apply, addAdaptiveScaler, report, counters);
                counters.Canvases++;
                if (apply) entry.AlreadyUpgraded = true;
            }
            return report.ToString();
        }

        static void UpgradeCanvas(CanvasEntry entry, bool apply, bool addAdaptiveScaler, StringBuilder report, UpgradeCounters c)
        {
            const float k = UpgradeScale;
            var canvas = entry.Canvas;
            var rootRt = (RectTransform)canvas.transform;

            report.AppendLine();
            report.AppendLine(entry.IsPrefabInstance
                ? $"=== Canvas '{canvas.name}' (PREFAB INSTANCE of {entry.PrefabAssetPath} — every change below becomes an instance override; prefer upgrading the prefab asset in its own Prefab Stage) ==="
                : $"=== Canvas '{canvas.name}' ===");
            foreach (var note in entry.Notes)
                report.AppendLine($"  note: {note}");

            // --- CanvasScaler ---
            var scaler = entry.Scaler;
            float newRefPpu = Mathf.Round(scaler.referencePixelsPerUnit * k * 1000f) / 1000f;
            report.AppendLine($"  <Canvas> :: CanvasScaler.referenceResolution: {Fmt(scaler.referenceResolution)} -> {Fmt(NewResolution)}");
            report.AppendLine($"  <Canvas> :: CanvasScaler.referencePixelsPerUnit: {Fmt(scaler.referencePixelsPerUnit)} -> {Fmt(newRefPpu)} (keeps 9-sliced/tiled sprite borders pixel-identical)");
            if (apply)
            {
                Record(scaler);
                scaler.referenceResolution = NewResolution;
                scaler.referencePixelsPerUnit = newRefPpu;
                MarkDirty(scaler);
            }

            UpgradeRectHierarchy(rootRt, includeRoot: false, apply, report, c);
            AddAdaptiveScalerStep(entry, apply, addAdaptiveScaler, report, c);
        }

        /// <summary>
        /// Scales every canvas-space value in a RectTransform hierarchy by x2.4. Shared by the
        /// canvas upgrade (root excluded — the root canvas rect is driven by the render mode) and
        /// the canvas-less UI prefab upgrade (root included — a spawned fragment's root is a plain
        /// RectTransform that gets parented under an upgraded canvas at runtime).
        /// </summary>
        static void UpgradeRectHierarchy(RectTransform pathRoot, bool includeRoot, bool apply, StringBuilder report, UpgradeCounters c)
        {
            const float k = UpgradeScale;

            foreach (var rt in pathRoot.GetComponentsInChildren<RectTransform>(true))
            {
                if (!includeRoot && rt == pathRoot) continue;
                Vector2 ap = rt.anchoredPosition;
                Vector2 sd = rt.sizeDelta;
                bool apChanges = ap.sqrMagnitude > 0f;
                bool sdChanges = sd.sqrMagnitude > 0f;
                if (!apChanges && !sdChanges) continue;

                report.AppendLine($"  {LabelPath(rt, pathRoot)} :: RectTransform {DescribeAxes(rt)}");
                if (apChanges) report.AppendLine($"      anchoredPosition: {Fmt(ap)} -> {Fmt(ap * k)}");
                if (sdChanges) report.AppendLine($"      sizeDelta: {Fmt(sd)} -> {Fmt(sd * k)}");
                if (apply)
                {
                    Record(rt);
                    rt.anchoredPosition = ap * k;
                    rt.sizeDelta = sd * k;
                    MarkDirty(rt);
                }
                c.RectTransforms++;
            }

            // --- TextMeshProUGUI ---
            foreach (var tmp in pathRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                report.AppendLine($"  {LabelPath(tmp.transform, pathRoot)} :: TextMeshProUGUI");
                report.AppendLine($"      fontSize: {Fmt(tmp.fontSize)} -> {Fmt(tmp.fontSize * k)}, fontSizeMin: {Fmt(tmp.fontSizeMin)} -> {Fmt(tmp.fontSizeMin * k)}, fontSizeMax: {Fmt(tmp.fontSizeMax)} -> {Fmt(tmp.fontSizeMax * k)}");
                bool marginChanges = tmp.margin.sqrMagnitude > 0f;
                if (marginChanges)
                    report.AppendLine($"      margin: {Fmt(tmp.margin)} -> {Fmt(tmp.margin * k)}");
                if (apply)
                {
                    Record(tmp);
                    tmp.fontSize *= k;
                    tmp.fontSizeMin *= k;
                    tmp.fontSizeMax *= k;
                    if (marginChanges) tmp.margin *= k;
                    MarkDirty(tmp);
                }
                c.TmpTexts++;
            }

            // --- Legacy UnityEngine.UI.Text ---
            foreach (var text in pathRoot.GetComponentsInChildren<Text>(true))
            {
                int newSize = ScaleInt(text.fontSize, c);
                int newMin = ScaleInt(text.resizeTextMinSize, c);
                int newMax = ScaleInt(text.resizeTextMaxSize, c);
                report.AppendLine($"  {LabelPath(text.transform, pathRoot)} :: Text (legacy)");
                report.AppendLine($"      fontSize: {text.fontSize} -> {newSize}, resizeTextMinSize: {text.resizeTextMinSize} -> {newMin}, resizeTextMaxSize: {text.resizeTextMaxSize} -> {newMax}");
                if (apply)
                {
                    Record(text);
                    text.fontSize = newSize;
                    text.resizeTextMinSize = newMin;
                    text.resizeTextMaxSize = newMax;
                    MarkDirty(text);
                }
                c.LegacyTexts++;
            }

            // --- Layout groups (padding on the shared base; spacing/cellSize per subtype) ---
            foreach (var group in pathRoot.GetComponentsInChildren<LayoutGroup>(true))
            {
                var p = group.padding;
                var newPadding = new RectOffset(ScaleInt(p.left, c), ScaleInt(p.right, c), ScaleInt(p.top, c), ScaleInt(p.bottom, c));
                report.AppendLine($"  {LabelPath(group.transform, pathRoot)} :: {group.GetType().Name}");
                report.AppendLine($"      padding LRTB: ({p.left}, {p.right}, {p.top}, {p.bottom}) -> ({newPadding.left}, {newPadding.right}, {newPadding.top}, {newPadding.bottom})");

                if (apply) Record(group);

                switch (group)
                {
                    case GridLayoutGroup grid:
                        report.AppendLine($"      cellSize: {Fmt(grid.cellSize)} -> {Fmt(grid.cellSize * k)}, spacing: {Fmt(grid.spacing)} -> {Fmt(grid.spacing * k)}");
                        if (apply)
                        {
                            grid.cellSize *= k;
                            grid.spacing *= k;
                        }
                        c.GridGroups++;
                        break;
                    case HorizontalOrVerticalLayoutGroup hv:
                        report.AppendLine($"      spacing: {Fmt(hv.spacing)} -> {Fmt(hv.spacing * k)}");
                        if (apply) hv.spacing *= k;
                        c.LayoutGroups++;
                        break;
                    default:
                        c.LayoutGroups++;
                        break;
                }

                if (apply)
                {
                    group.padding = newPadding;
                    MarkDirty(group);
                }
            }

            // --- LayoutElement ---
            var pendingWrites = new List<System.Action>();
            foreach (var le in pathRoot.GetComponentsInChildren<LayoutElement>(true))
            {
                var sub = new StringBuilder();
                pendingWrites.Clear();
                ScaleLayoutSize(le.minWidth, v => pendingWrites.Add(() => le.minWidth = v), "minWidth", sub);
                ScaleLayoutSize(le.minHeight, v => pendingWrites.Add(() => le.minHeight = v), "minHeight", sub);
                ScaleLayoutSize(le.preferredWidth, v => pendingWrites.Add(() => le.preferredWidth = v), "preferredWidth", sub);
                ScaleLayoutSize(le.preferredHeight, v => pendingWrites.Add(() => le.preferredHeight = v), "preferredHeight", sub);
                ScaleFlexible(le.flexibleWidth, v => pendingWrites.Add(() => le.flexibleWidth = v), "flexibleWidth", sub, c);
                ScaleFlexible(le.flexibleHeight, v => pendingWrites.Add(() => le.flexibleHeight = v), "flexibleHeight", sub, c);
                if (sub.Length == 0) continue;

                report.AppendLine($"  {LabelPath(le.transform, pathRoot)} :: LayoutElement");
                report.Append(sub);
                if (apply && pendingWrites.Count > 0)
                {
                    Record(le);
                    foreach (var write in pendingWrites) write();
                    MarkDirty(le);
                }
                c.LayoutElements++;
            }

            // --- Shadow / Outline (Outline derives from Shadow) ---
            foreach (var shadow in pathRoot.GetComponentsInChildren<Shadow>(true))
            {
                report.AppendLine($"  {LabelPath(shadow.transform, pathRoot)} :: {shadow.GetType().Name} effectDistance: {Fmt(shadow.effectDistance)} -> {Fmt(shadow.effectDistance * k)}");
                if (apply)
                {
                    Record(shadow);
                    shadow.effectDistance *= k;
                    MarkDirty(shadow);
                }
                c.Shadows++;
            }

            // --- RectMask2D (padding/softness are canvas-unit values) ---
            foreach (var mask in pathRoot.GetComponentsInChildren<RectMask2D>(true))
            {
                bool paddingChanges = mask.padding.sqrMagnitude > 0f;
                bool softnessChanges = mask.softness != Vector2Int.zero;
                if (!paddingChanges && !softnessChanges) continue;
                var newSoftness = new Vector2Int(ScaleInt(mask.softness.x, c), ScaleInt(mask.softness.y, c));
                report.AppendLine($"  {LabelPath(mask.transform, pathRoot)} :: RectMask2D");
                if (paddingChanges) report.AppendLine($"      padding: {Fmt(mask.padding)} -> {Fmt(mask.padding * k)}");
                if (softnessChanges) report.AppendLine($"      softness: {mask.softness} -> {newSoftness}");
                if (apply)
                {
                    Record(mask);
                    if (paddingChanges) mask.padding *= k;
                    if (softnessChanges) mask.softness = newSoftness;
                    MarkDirty(mask);
                }
                c.RectMasks++;
            }
        }

        static void AddAdaptiveScalerStep(CanvasEntry entry, bool apply, bool addAdaptiveScaler, StringBuilder report, UpgradeCounters c)
        {
            var canvas = entry.Canvas;
            if (addAdaptiveScaler)
            {
                if (canvas.TryGetComponent(out CosmicShore.UI.AdaptiveCanvasScaler _))
                {
                    report.AppendLine("  <Canvas> :: AdaptiveCanvasScaler already present — untouched");
                }
                else
                {
                    report.AppendLine("  <Canvas> :: add AdaptiveCanvasScaler (drives matchWidthOrHeight from the live aspect ratio)");
                    if (apply)
                    {
                        Undo.AddComponent<CosmicShore.UI.AdaptiveCanvasScaler>(canvas.gameObject);
                        c.AdaptiveAdded++;
                    }
                }
            }
        }

        static void ScaleLayoutSize(float value, System.Action<float> setter, string label, StringBuilder report)
        {
            // -1 means "not driven"; 0 scales to 0. Only positive values carry pixel meaning.
            if (value <= 0f) return;
            report.AppendLine($"      {label}: {Fmt(value)} -> {Fmt(value * UpgradeScale)}");
            setter(value * UpgradeScale);
        }

        static void ScaleFlexible(float value, System.Action<float> setter, string label, StringBuilder report, UpgradeCounters c)
        {
            if (value <= 0f) return;
            if (value > FlexiblePixelThreshold)
            {
                report.AppendLine($"      {label}: {Fmt(value)} -> {Fmt(value * UpgradeScale)} (pixel-like flexible value — review)");
                setter(value * UpgradeScale);
                c.FlexibleScaled++;
            }
            else
            {
                report.AppendLine($"      {label}: {Fmt(value)} left unchanged (dimensionless layout weight)");
                c.FlexibleLeftAsWeights++;
            }
        }

        // ------------------------------------------------------------------
        //  Smart re-anchor
        // ------------------------------------------------------------------

        /// <summary>
        /// Snaps center-anchored elements to the nearest of the 9 anchor presets based on where
        /// their rect center sits within the parent rect, recomputing anchoredPosition so nothing
        /// moves visually. Stretched axes, edge-anchored presets, custom anchors, and
        /// layout-group-driven children are left untouched (and reported). Direct children of the
        /// canvas by default; <paramref name="recursive"/> covers all descendants (each snapped
        /// relative to its own parent). The caller owns the Undo group.
        /// </summary>
        public static string Reanchor(List<CanvasEntry> entries, bool recursive, out int changed, out int skipped)
        {
            var report = new StringBuilder();
            report.AppendLine($"SMART RE-ANCHOR REPORT ({(recursive ? "recursive" : "direct children of each canvas")})");
            int changedCount = 0, skippedCount = 0;

            foreach (var entry in entries)
            {
                if (entry == null || !entry.Canvas || !entry.Selected) continue;
                var rootRt = (RectTransform)entry.Canvas.transform;
                report.AppendLine($"\n=== Canvas '{entry.Canvas.name}' ===");

                if (recursive)
                {
                    foreach (var rt in entry.Canvas.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (rt == rootRt) continue;
                        ReanchorElement(rt, rootRt, report, ref changedCount, ref skippedCount);
                    }
                }
                else
                {
                    foreach (Transform child in rootRt)
                    {
                        if (child is RectTransform rt)
                            ReanchorElement(rt, rootRt, report, ref changedCount, ref skippedCount);
                    }
                }
            }

            changed = changedCount;
            skipped = skippedCount;
            return report.ToString();
        }

        static void ReanchorElement(RectTransform rt, RectTransform canvasRoot, StringBuilder report, ref int changed, ref int skipped)
        {
            string path = PathOf(rt, canvasRoot);
            if (rt.parent is not RectTransform parent)
            {
                skipped++;
                return;
            }

            // Layout-group children get their position from the layout pass — re-anchoring them
            // would be overwritten and only churn the scene file.
            if (parent.TryGetComponent(out LayoutGroup layoutGroup) && layoutGroup.enabled
                && !(rt.TryGetComponent(out LayoutElement le) && le.ignoreLayout))
            {
                report.AppendLine($"  {path} — skipped (position driven by {layoutGroup.GetType().Name} on parent)");
                skipped++;
                return;
            }

            Vector2 aMin = rt.anchorMin, aMax = rt.anchorMax;
            bool stretchedX = !Mathf.Approximately(aMin.x, aMax.x);
            bool stretchedY = !Mathf.Approximately(aMin.y, aMax.y);
            if (stretchedX && stretchedY)
            {
                report.AppendLine($"  {path} — skipped (stretched on both axes)");
                skipped++;
                return;
            }

            Rect pr = parent.rect;
            if (pr.width <= 0f || pr.height <= 0f)
            {
                report.AppendLine($"  {path} — skipped (parent rect has zero size)");
                skipped++;
                return;
            }

            // Element rect center in parent local space (robust to element rotation/scale),
            // normalized 0..1 across the parent rect.
            Vector2 centerLocal = parent.InverseTransformPoint(rt.TransformPoint(rt.rect.center));
            float nx = (centerLocal.x - pr.xMin) / pr.width;
            float ny = (centerLocal.y - pr.yMin) / pr.height;

            bool moveX = ResolveAxis(stretchedX, aMin.x, nx, out float newAx, out string skipReasonX);
            bool moveY = ResolveAxis(stretchedY, aMin.y, ny, out float newAy, out string skipReasonY);

            if (!moveX && !moveY)
            {
                string reason = skipReasonX ?? skipReasonY ?? "already at the matching preset";
                report.AppendLine($"  {path} — skipped ({reason})");
                skipped++;
                return;
            }

            // Keep the pivot fixed in parent space: for a point anchor the pivot sits at
            // (parentMin + anchor*parentSize) + anchoredPosition, so shifting the anchor by
            // delta moves the reference by delta*parentSize and anchoredPosition compensates.
            Vector2 oldAp = rt.anchoredPosition;
            Vector2 ap = oldAp;
            var newMin = aMin;
            var newMax = aMax;
            if (moveX)
            {
                ap.x += (aMin.x - newAx) * pr.width;
                newMin.x = newMax.x = newAx;
            }
            if (moveY)
            {
                ap.y += (aMin.y - newAy) * pr.height;
                newMin.y = newMax.y = newAy;
            }

            Record(rt);
            rt.anchorMin = newMin;
            rt.anchorMax = newMax;
            rt.anchoredPosition = ap;
            MarkDirty(rt);

            string anchorsBefore = stretchedX || stretchedY ? $"{Fmt(aMin)}..{Fmt(aMax)}" : Fmt(aMin);
            string anchorsAfter = stretchedX || stretchedY ? $"{Fmt(newMin)}..{Fmt(newMax)}" : Fmt(newMin);
            report.AppendLine($"  {path} — anchors {anchorsBefore} -> {anchorsAfter}, center at ({nx:0.00}, {ny:0.00}) of parent, anchoredPosition {Fmt(oldAp)} -> {Fmt(ap)} (visual position preserved)");
            changed++;
        }

        /// <summary>
        /// Decides whether one axis should snap: stretched and edge/custom-anchored axes are left
        /// untouched; only default center (0.5) anchors snap to the nearest of 0 / 0.5 / 1.
        /// </summary>
        static bool ResolveAxis(bool stretched, float anchor, float normalizedCenter, out float newAnchor, out string skipReason)
        {
            newAnchor = anchor;
            skipReason = null;

            if (stretched)
            {
                skipReason = "stretched axis left untouched";
                return false;
            }
            if (Mathf.Approximately(anchor, 0f) || Mathf.Approximately(anchor, 1f))
            {
                skipReason = "already edge-anchored";
                return false;
            }
            if (!Mathf.Approximately(anchor, 0.5f))
            {
                skipReason = $"custom anchor {anchor:0.###} left as authored";
                return false;
            }

            newAnchor = normalizedCenter < 0.25f ? 0f : normalizedCenter > 0.75f ? 1f : 0.5f;
            return !Mathf.Approximately(newAnchor, anchor);
        }

        // ------------------------------------------------------------------
        //  Faulted anchors (anchor region nowhere near the element it drives)
        // ------------------------------------------------------------------

        /// <summary>
        /// Finds RectTransforms whose anchor region on the parent is DISJOINT from the rect it
        /// drives by more than <paramref name="tolerancePx"/> - the "anchors not wrapped around
        /// the element" state (huge offsets carrying the rect far from its anchors, so nothing
        /// holds its place across resolutions). With <paramref name="apply"/> the fix wraps the
        /// anchors around the element's current rect (anchorMin/Max = the rect's normalized
        /// corners in the parent, offsets zeroed) - pixel-identical visually, and the element now
        /// scales with its parent region. Legitimate layouts are untouched: an anchor point
        /// inside or near its own rect is not a fault, and layout-group-driven children are
        /// skipped. Works on the pre-rotation rect math, so rotated/scaled elements are safe.
        /// The caller owns the Undo group when applying.
        /// </summary>
        public static string FixFaultedAnchors(List<CanvasEntry> entries, RectTransform extraRoot,
            float tolerancePx, bool apply, out int faulted, out int fixedCount)
        {
            var report = new StringBuilder();
            report.AppendLine($"FAULTED ANCHOR {(apply ? "FIX" : "SCAN")} REPORT (tolerance {tolerancePx:0.#}px)");
            int faultCount = 0, fixCount = 0, skippedLayout = 0;

            void Walk(RectTransform root, string rootLabel)
            {
                report.AppendLine($"\n=== {rootLabel} ===");
                foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt == root) continue;
                    FixElement(rt, root, tolerancePx, apply, report,
                        ref faultCount, ref fixCount, ref skippedLayout);
                }
            }

            if (entries != null)
                foreach (var entry in entries)
                    if (entry != null && entry.Canvas && entry.Selected)
                        Walk((RectTransform)entry.Canvas.transform, $"Canvas '{entry.Canvas.name}'");

            if (extraRoot)
                Walk(extraRoot, $"Prefab root '{extraRoot.name}'");

            if (skippedLayout > 0)
                report.AppendLine($"\n({skippedLayout} faulted element(s) skipped: position driven by a parent LayoutGroup - fix the group or the element's ignoreLayout instead.)");

            faulted = faultCount;
            fixedCount = fixCount;
            return report.ToString();
        }

        static void FixElement(RectTransform rt, RectTransform scanRoot, float tolerancePx, bool apply,
            StringBuilder report, ref int faulted, ref int fixedCount, ref int skippedLayout)
        {
            if (rt.parent is not RectTransform parent) return;

            Rect pr = parent.rect;
            if (pr.width <= 0f || pr.height <= 0f) return;

            // Pre-rotation rect math: the anchor region and the driven rect, both in parent space.
            // (Rotation/scale apply around the pivot at render time and don't affect this.)
            Vector2 aMinPos = pr.min + rt.anchorMin * pr.size;
            Vector2 aMaxPos = pr.min + rt.anchorMax * pr.size;
            Vector2 rectMin = aMinPos + rt.offsetMin;
            Vector2 rectMax = aMaxPos + rt.offsetMax;

            // Fault = the anchor region and the element rect are disjoint beyond tolerance on
            // either axis. An anchor inside (or touching) its own rect is a legitimate layout.
            float gapX = Mathf.Max(0f, Mathf.Max(aMinPos.x - rectMax.x, rectMin.x - aMaxPos.x));
            float gapY = Mathf.Max(0f, Mathf.Max(aMinPos.y - rectMax.y, rectMin.y - aMaxPos.y));
            if (gapX <= tolerancePx && gapY <= tolerancePx) return;

            string path = PathOf(rt, scanRoot);
            faulted++;

            // A layout group will re-place this child anyway - rewriting anchors would only churn.
            if (parent.TryGetComponent(out LayoutGroup layoutGroup) && layoutGroup.enabled
                && !(rt.TryGetComponent(out LayoutElement le) && le.ignoreLayout))
            {
                report.AppendLine($"  {path} — FAULTED (anchor gap {Mathf.Max(gapX, gapY):0.#}px) but driven by {layoutGroup.GetType().Name} on parent — skipped");
                skippedLayout++;
                return;
            }

            if (!apply)
            {
                report.AppendLine($"  {path} — FAULTED: anchor region sits {Mathf.Max(gapX, gapY):0.#}px away from the element rect (anchors {Fmt(rt.anchorMin)}..{Fmt(rt.anchorMax)})");
                return;
            }

            var newMin = new Vector2((rectMin.x - pr.xMin) / pr.width, (rectMin.y - pr.yMin) / pr.height);
            var newMax = new Vector2((rectMax.x - pr.xMin) / pr.width, (rectMax.y - pr.yMin) / pr.height);

            Record(rt);
            rt.anchorMin = newMin;
            rt.anchorMax = newMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            MarkDirty(rt);

            report.AppendLine($"  {path} — fixed: anchors wrapped to {Fmt(newMin)}..{Fmt(newMax)}, offsets zeroed (was {Mathf.Max(gapX, gapY):0.#}px off; visual position preserved)");
            fixedCount++;
        }

        // ------------------------------------------------------------------
        //  Animation clips
        // ------------------------------------------------------------------

        /// <summary>
        /// Finds every AnimationClip reachable from Animator/Animation components on, under, or
        /// above the scanned canvases whose curves animate RectTransform anchoredPosition or
        /// sizeDelta on a transform inside those canvases. Deduplicated per (clip, curve).
        /// </summary>
        public static List<AnimatedBindingHit> FindAnimatedRectBindings(List<CanvasEntry> entries)
        {
            var hits = new List<AnimatedBindingHit>();
            var seenCurves = new HashSet<string>();
            var hosts = new List<GameObject>();

            foreach (var entry in entries)
            {
                if (entry == null || !entry.Canvas || !entry.Selected) continue;
                var canvasTr = entry.Canvas.transform;

                hosts.Clear();
                foreach (var animator in entry.Canvas.GetComponentsInChildren<Animator>(true))
                    if (!hosts.Contains(animator.gameObject)) hosts.Add(animator.gameObject);
                foreach (var animator in canvasTr.GetComponentsInParent<Animator>(true))
                    if (!hosts.Contains(animator.gameObject)) hosts.Add(animator.gameObject);
                foreach (var animation in entry.Canvas.GetComponentsInChildren<Animation>(true))
                    if (!hosts.Contains(animation.gameObject)) hosts.Add(animation.gameObject);
                foreach (var animation in canvasTr.GetComponentsInParent<Animation>(true))
                    if (!hosts.Contains(animation.gameObject)) hosts.Add(animation.gameObject);

                foreach (var host in hosts)
                {
                    foreach (var clip in CollectClips(host))
                    {
                        if (!clip) continue;
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        {
                            if (binding.type != typeof(RectTransform)) continue;
                            if (!binding.propertyName.StartsWith("m_AnchoredPosition")
                                && !binding.propertyName.StartsWith("m_SizeDelta")) continue;

                            // Only report curves that actually resolve to a transform inside this canvas.
                            var animated = AnimationUtility.GetAnimatedObject(host, binding) as RectTransform;
                            if (!animated) continue;
                            if (animated.transform != canvasTr && !animated.transform.IsChildOf(canvasTr)) continue;

                            var hit = new AnimatedBindingHit
                            {
                                Clip = clip,
                                ClipAssetPath = AssetDatabase.GetAssetPath(clip),
                                Binding = binding,
                                HostPath = PathOf(host.transform, null),
                                CanvasName = entry.Canvas.name,
                            };
                            if (seenCurves.Add(hit.CurveKey))
                                hits.Add(hit);
                        }
                    }
                }
            }
            return hits;
        }

        static IEnumerable<AnimationClip> CollectClips(GameObject host)
        {
            if (host.TryGetComponent(out Animator animator) && animator.runtimeAnimatorController)
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                    yield return clip;
            if (host.TryGetComponent(out Animation _))
                foreach (var clip in AnimationUtility.GetAnimationClips(host))
                    yield return clip;
        }

        // Cross-session logs in ProjectSettings (no .meta churn), committed so they protect the
        // whole team. Curve log: shared clips like the Settings-panel animations are surfaced by
        // EVERY scene's animation scan, and scaling twice would compound to x5.76. Prefab log:
        // canvas-less fragments carry no CanvasScaler marking their authored space, so the log
        // is the only double-run guard.
        const string ScaledCurveLogPath = "ProjectSettings/CanvasUpgraderScaledCurves.txt";
        internal const string UpgradedPrefabLogPath = "ProjectSettings/CanvasUpgraderUpgradedPrefabs.txt";

        /// <summary>Loads the persistent log of animation curves already scaled x2.4 (one CurveKey per line).</summary>
        public static HashSet<string> LoadScaledCurveLog() => LoadLog(ScaledCurveLogPath);

        /// <summary>True if a canvas-less UI prefab was already upgraded per the persistent log.</summary>
        public static bool IsPrefabLoggedUpgraded(string prefabGuid)
        {
            foreach (var line in LoadLog(UpgradedPrefabLogPath))
                if (line.StartsWith(prefabGuid))
                    return true;
            return false;
        }

        /// <summary>Records a canvas-less UI prefab as upgraded so the window refuses a second, compounding pass.</summary>
        public static void MarkPrefabUpgraded(string prefabGuid, string assetPath)
        {
            var keys = LoadLog(UpgradedPrefabLogPath);
            if (keys.Add($"{prefabGuid} {assetPath}"))
                SaveLog(UpgradedPrefabLogPath, keys);
        }

        static HashSet<string> LoadLog(string path)
        {
            var keys = new HashSet<string>();
            if (!File.Exists(path)) return keys;
            foreach (var line in File.ReadAllLines(path))
                if (!string.IsNullOrWhiteSpace(line))
                    keys.Add(line.Trim());
            return keys;
        }

        static void SaveLog(string path, HashSet<string> keys)
        {
            var sorted = new List<string>(keys);
            sorted.Sort();
            File.WriteAllLines(path, sorted);
        }

        /// <summary>
        /// Multiplies keyframe values (and tangents, which are value/time slopes) of the given
        /// curves by x2.4. Clips are ASSETS — scaling affects every scene and prefab that uses
        /// them. <paramref name="alreadyScaled"/> (seed it from <see cref="LoadScaledCurveLog"/>)
        /// guards against double-scaling; every curve scaled here is appended to the persistent
        /// log. The caller owns the Undo group; changes persist on the next asset save. If a
        /// scaling is Undone, remove the matching lines from the log file too.
        /// </summary>
        public static string ScaleAnimationKeys(List<AnimatedBindingHit> hits, HashSet<string> alreadyScaled, out int scaledCurves)
        {
            var report = new StringBuilder();
            report.AppendLine($"ANIMATION KEYFRAME SCALE x{UpgradeScale} — clips are shared assets; every user of these clips is affected.");
            var recordedClips = new HashSet<AnimationClip>();
            scaledCurves = 0;

            foreach (var hit in hits)
            {
                if (hit == null || !hit.Clip) continue;
                if (!alreadyScaled.Add(hit.CurveKey))
                {
                    report.AppendLine($"  {hit.ClipAssetPath} :: {hit.Binding.path}/{hit.Binding.propertyName} — skipped (already scaled per {ScaledCurveLogPath})");
                    continue;
                }

                var curve = AnimationUtility.GetEditorCurve(hit.Clip, hit.Binding);
                if (curve == null) continue;

                if (recordedClips.Add(hit.Clip))
                    Undo.RecordObject(hit.Clip, "Scale UI Animation Keys x2.4");

                var keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i].value *= UpgradeScale;
                    keys[i].inTangent *= UpgradeScale;
                    keys[i].outTangent *= UpgradeScale;
                }
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(hit.Clip, hit.Binding, curve);
                EditorUtility.SetDirty(hit.Clip);

                report.AppendLine($"  {hit.ClipAssetPath} :: {hit.Binding.path}/{hit.Binding.propertyName} — {keys.Length} key(s) scaled");
                scaledCurves++;
            }

            if (scaledCurves > 0)
            {
                SaveLog(ScaledCurveLogPath, alreadyScaled);
                report.AppendLine($"Logged to {ScaledCurveLogPath} (commit it) so future scans in other scenes skip these curves.");
            }
            report.AppendLine("Save the project (Ctrl+S / File > Save Project) to persist the clip assets.");
            return report.ToString();
        }

        // ------------------------------------------------------------------
        //  Canvas-less UI prefab upgrade (runtime-spawned fragments)
        // ------------------------------------------------------------------

        /// <summary>
        /// Upgrades a canvas-less UI prefab — a RectTransform hierarchy (PlayerScoreCard, toast,
        /// feed entry, vessel HUD variant, ...) that gets Instantiate'd under a canvas at runtime.
        /// The whole hierarchy INCLUDING the root is scaled x2.4, since the root's own
        /// sizeDelta/anchoredPosition live in the parent canvas's units. No CanvasScaler exists on
        /// these to mark the authored space, so the caller must gate re-runs via
        /// <see cref="IsPrefabLoggedUpgraded"/> / <see cref="MarkPrefabUpgraded"/>. The caller
        /// owns the Undo group.
        /// </summary>
        public static string UpgradePrefabRoot(RectTransform root, bool apply, UpgradeCounters counters)
        {
            var report = new StringBuilder();
            report.AppendLine(apply
                ? $"CANVAS-LESS PREFAB UPGRADE — '{root.name}' hierarchy x{UpgradeScale} (root included)"
                : $"DRY RUN — no changes applied. '{root.name}' hierarchy WOULD scale x{UpgradeScale} (root included).");
            UpgradeRectHierarchy(root, includeRoot: true, apply, report, counters);
            return report.ToString();
        }

        // ------------------------------------------------------------------
        //  Shared helpers
        // ------------------------------------------------------------------

        static void Record(Object obj) => Undo.RecordObject(obj, UndoLabel);

        static void MarkDirty(Object obj)
        {
            EditorUtility.SetDirty(obj);
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }

        static int ScaleInt(int value, UpgradeCounters c)
        {
            float exact = value * UpgradeScale;
            int rounded = Mathf.RoundToInt(exact);
            if (!Mathf.Approximately(exact, rounded)) c.RoundedInts++;
            return rounded;
        }

        static bool Approximately(Vector2 a, Vector2 b) =>
            Mathf.Abs(a.x - b.x) < 0.5f && Mathf.Abs(a.y - b.y) < 0.5f;

        static string DescribeAxes(RectTransform rt)
        {
            string x = Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x) ? "anchored" : "stretched";
            string y = Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y) ? "anchored" : "stretched";
            return $"(x: {x}, y: {y})";
        }

        static string Fmt(float f) => f.ToString("0.##");
        static string Fmt(Vector2 v) => $"({v.x:0.##}, {v.y:0.##})";
        static string Fmt(Vector4 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##}, {v.w:0.##})";

        // Report label: the traversal root itself has an empty PathOf, so name it explicitly
        // (happens only in the root-inclusive canvas-less prefab upgrade).
        static string LabelPath(Transform t, Transform root) =>
            t == root ? $"<root {t.name}>" : PathOf(t, root);

        /// <summary>Hierarchy path of <paramref name="t"/> up to (exclusive) <paramref name="stopAt"/>.</summary>
        public static string PathOf(Transform t, Transform stopAt)
        {
            var parts = new List<string>();
            while (t != null && t != stopAt)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
