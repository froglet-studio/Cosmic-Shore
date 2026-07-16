using System.Collections.Generic;
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

            public string CurveKey => $"{ClipAssetPath}|{Binding.path}|{Binding.propertyName}";
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

            // --- RectTransforms (root excluded: it is driven by the render mode) ---
            foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt == rootRt) continue;
                Vector2 ap = rt.anchoredPosition;
                Vector2 sd = rt.sizeDelta;
                bool apChanges = ap.sqrMagnitude > 0f;
                bool sdChanges = sd.sqrMagnitude > 0f;
                if (!apChanges && !sdChanges) continue;

                report.AppendLine($"  {PathOf(rt, rootRt)} :: RectTransform {DescribeAxes(rt)}");
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
            foreach (var tmp in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                report.AppendLine($"  {PathOf(tmp.transform, rootRt)} :: TextMeshProUGUI");
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
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            {
                int newSize = ScaleInt(text.fontSize, c);
                int newMin = ScaleInt(text.resizeTextMinSize, c);
                int newMax = ScaleInt(text.resizeTextMaxSize, c);
                report.AppendLine($"  {PathOf(text.transform, rootRt)} :: Text (legacy)");
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
            foreach (var group in canvas.GetComponentsInChildren<LayoutGroup>(true))
            {
                var p = group.padding;
                var newPadding = new RectOffset(ScaleInt(p.left, c), ScaleInt(p.right, c), ScaleInt(p.top, c), ScaleInt(p.bottom, c));
                report.AppendLine($"  {PathOf(group.transform, rootRt)} :: {group.GetType().Name}");
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
            foreach (var le in canvas.GetComponentsInChildren<LayoutElement>(true))
            {
                report.AppendLine($"  {PathOf(le.transform, rootRt)} :: LayoutElement");
                if (apply) Record(le);
                ScaleLayoutSize(le.minWidth, v => { if (apply) le.minWidth = v; }, "minWidth", report);
                ScaleLayoutSize(le.minHeight, v => { if (apply) le.minHeight = v; }, "minHeight", report);
                ScaleLayoutSize(le.preferredWidth, v => { if (apply) le.preferredWidth = v; }, "preferredWidth", report);
                ScaleLayoutSize(le.preferredHeight, v => { if (apply) le.preferredHeight = v; }, "preferredHeight", report);
                ScaleFlexible(le.flexibleWidth, v => { if (apply) le.flexibleWidth = v; }, "flexibleWidth", report, c);
                ScaleFlexible(le.flexibleHeight, v => { if (apply) le.flexibleHeight = v; }, "flexibleHeight", report, c);
                if (apply) MarkDirty(le);
                c.LayoutElements++;
            }

            // --- Shadow / Outline (Outline derives from Shadow) ---
            foreach (var shadow in canvas.GetComponentsInChildren<Shadow>(true))
            {
                report.AppendLine($"  {PathOf(shadow.transform, rootRt)} :: {shadow.GetType().Name} effectDistance: {Fmt(shadow.effectDistance)} -> {Fmt(shadow.effectDistance * k)}");
                if (apply)
                {
                    Record(shadow);
                    shadow.effectDistance *= k;
                    MarkDirty(shadow);
                }
                c.Shadows++;
            }

            // --- RectMask2D (padding/softness are canvas-unit values) ---
            foreach (var mask in canvas.GetComponentsInChildren<RectMask2D>(true))
            {
                bool paddingChanges = mask.padding.sqrMagnitude > 0f;
                bool softnessChanges = mask.softness != Vector2Int.zero;
                if (!paddingChanges && !softnessChanges) continue;
                var newSoftness = new Vector2Int(ScaleInt(mask.softness.x, c), ScaleInt(mask.softness.y, c));
                report.AppendLine($"  {PathOf(mask.transform, rootRt)} :: RectMask2D");
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

            // --- AdaptiveCanvasScaler (runtime aspect matching for PC monitors) ---
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
            Vector2 ap = rt.anchoredPosition;
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

            report.AppendLine($"  {path} — anchors ({Fmt(aMin)}) -> ({Fmt(newMin)}), center at ({nx:0.00}, {ny:0.00}) of parent, anchoredPosition {Fmt(rt.anchoredPosition)} (visual position preserved)");
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

        /// <summary>
        /// Multiplies keyframe values (and tangents, which are value/time slopes) of the given
        /// curves by x2.4. Clips are ASSETS — scaling affects every scene and prefab that uses
        /// them, and <paramref name="alreadyScaled"/> guards against double-scaling within this
        /// window session. The caller owns the Undo group; changes persist on the next asset save.
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
                    report.AppendLine($"  {hit.ClipAssetPath} :: {hit.Binding.path}/{hit.Binding.propertyName} — skipped (already scaled this session)");
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

            report.AppendLine("Save the project (Ctrl+S / File > Save Project) to persist the clip assets.");
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
