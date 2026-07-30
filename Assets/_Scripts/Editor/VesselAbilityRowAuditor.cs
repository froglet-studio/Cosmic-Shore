using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Fleet-wide compliance audit for the four-icon ability row (CLAUDE.md "The Four-Icon Ability Row",
    /// Docs/ElementalAbilitySystem/ARCHITECTURE.md §7.1-7.3).
    ///
    /// The contract every vessel must satisfy:
    ///   1. ElementalAbilityMapSO with all four elements, each naming an ability and its level-5 upgrade.
    ///   2. Four ability icons in the HUD, bound one per element.
    ///   3. Those icons laid out left to right in charge → mass → space → time order.
    ///   4. Uniform slot size and uniform pitch.
    ///   5. A control hint on every ability the player actually presses.
    ///
    /// Runs entirely on assets - no play mode. Geometry is measured against a deterministic
    /// 1920x1080 world-space canvas so the numbers do not depend on the editor window size.
    /// </summary>
    public static class VesselAbilityRowAuditor
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const float RefWidth = 1920f, RefHeight = 1080f;
        const float PitchTolerancePx = 1.5f;
        const float SizeTolerancePx = 1.5f;

        [MenuItem("Tools/Cosmic Shore/Audit Vessel Ability Rows")]
        public static void Audit()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Vessel ability-row compliance ===");
            report.AppendLine("contract: 4 icons, one per element, charge → mass → space → time, "
                              + "uniform size + pitch, a hint per pressable ability");
            report.AppendLine();

            int compliant = 0, total = 0;
            foreach (var path in VesselPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;
                if (!prefab.GetComponentInChildren<VesselHUDView>(true)) continue;   // not a flyable HUD vessel

                total++;
                var issues = AuditVessel(prefab, out var vesselClass, out var detail);
                if (issues.Count == 0) compliant++;

                report.AppendLine($"--- {prefab.name}  ({vesselClass})  {(issues.Count == 0 ? "COMPLIANT" : issues.Count + " issue(s)")}");
                if (!string.IsNullOrEmpty(detail)) report.AppendLine(detail.TrimEnd());
                foreach (var issue in issues) report.AppendLine($"      ! {issue}");
                report.AppendLine();
            }

            report.AppendLine($"{compliant}/{total} vessels compliant.");
            Debug.Log(report.ToString());
        }

        static IEnumerable<string> VesselPrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .OrderBy(p => p);

        static List<string> AuditVessel(GameObject prefab, out VesselClassType vesselClass, out string detail)
        {
            var issues = new List<string>();
            var lines = new StringBuilder();

            if (!Enum.TryParse(prefab.name, out vesselClass))
            {
                vesselClass = VesselClassType.Any;
                issues.Add($"prefab name '{prefab.name}' does not match a VesselClassType - cannot resolve its ability map");
            }

            // ---- 1. the map ----
            var map = vesselClass != VesselClassType.Any ? ElementalAbilityMapSO.LoadFor(vesselClass) : null;
            var handler = prefab.GetComponentInChildren<R_VesselActionHandler>(true);
            var boundInputs = handler ? SerializedBoundInputs(handler) : new HashSet<InputEvents>();

            if (map == null)
            {
                issues.Add($"no ElementalAbilityMapSO at Resources/{ElementalAbilityMapSO.ResourceFolder}/{vesselClass}");
            }
            else
            {
                foreach (var element in VesselHUDView.AbilityDisplayOrder)
                {
                    var entry = map.GetEntry(element);
                    if (entry == null) { issues.Add($"map has no {element} entry"); continue; }

                    bool pressable = boundInputs.Contains(entry.Input);
                    string label = string.IsNullOrWhiteSpace(entry.AbilityLabel) ? "(unnamed)" : entry.AbilityLabel;
                    lines.AppendLine($"      {element,-6} {(pressable ? entry.Input.ToString() : "passive"),-22} \"{label}\"");

                    if (label.Contains("open design slot"))
                        issues.Add($"{element}: ability is still an open design slot - the element→ability mapping is unauthored");
                    if (string.IsNullOrWhiteSpace(entry.UpgradeLabel))
                        issues.Add($"{element}: no level-5 UpgradeLabel authored");
                }
            }

            // ---- 2/3/4. the icon row ----
            var view = prefab.GetComponentInChildren<VesselHUDView>(true);
            var order = VesselHUDView.AbilityDisplayOrder;

            if (view.abilityIcons == null || view.abilityIcons.Count == 0)
            {
                issues.Add($"HUD '{view.GetType().Name}' binds NO abilityIcons - the four-icon row does not exist on this vessel");
            }
            else
            {
                if (view.abilityIcons.Count != order.Length)
                    issues.Add($"binds {view.abilityIcons.Count} ability icon(s); the contract is {order.Length}");

                for (int i = 0; i < Mathf.Min(view.abilityIcons.Count, order.Length); i++)
                {
                    if (view.abilityIcons[i].element != order[i])
                        issues.Add($"slot {i} is bound to {view.abilityIcons[i].element}, expected {order[i]}");
                    if (!view.abilityIcons[i].icon)
                        issues.Add($"slot {i} ({order[i]}) has no icon Image assigned");
                }

                MeasureRow(prefab, issues, lines);
            }

            // ---- 5. control hints ----
            var switcher = prefab.GetComponentInChildren<InputDeviceIconSetSwitcher>(true);
            if (map != null)
            {
                var pressable = order
                    .Select(map.GetEntry)
                    .Where(e => e != null && boundInputs.Contains(e.Input))
                    .ToList();

                if (pressable.Count > 0 && !switcher)
                    issues.Add($"{pressable.Count} pressable ability(ies) but no InputDeviceIconSetSwitcher - "
                               + "control hints cannot bind to abilities, and any glyph sets are untoggled");
                else if (switcher)
                {
                    var labelled = HintedElements(switcher, map);
                    foreach (var entry in pressable.Where(e => !labelled.Contains(e.Element)))
                        issues.Add($"{entry.Element} is pressable ({entry.Input}) but no control hint labels it");
                }
            }

            detail = lines.ToString();
            return issues;
        }

        /// <summary>Inputs the prefab binds, read off the serialized maps (no Initialize required).</summary>
        static HashSet<InputEvents> SerializedBoundInputs(R_VesselActionHandler handler)
        {
            var result = new HashSet<InputEvents>();
            var so = new SerializedObject(handler);
            foreach (var field in new[] { "_inputEventShipActions", "_touchActionOverrides", "_gamepadActionOverrides" })
            {
                var list = so.FindProperty(field);
                if (list == null || !list.isArray) continue;
                for (int i = 0; i < list.arraySize; i++)
                {
                    var entry = list.GetArrayElementAtIndex(i);
                    var input = entry.FindPropertyRelative("InputEvent");
                    var actions = entry.FindPropertyRelative("ShipActions");
                    if (input != null && actions != null && actions.arraySize > 0)
                        result.Add((InputEvents)input.intValue);
                }
            }
            return result;
        }

        static HashSet<Element> HintedElements(InputDeviceIconSetSwitcher switcher, ElementalAbilityMapSO map)
        {
            var labelled = new HashSet<Element>();
            var so = new SerializedObject(switcher);
            var sets = so.FindProperty("setVisuals");
            if (sets == null || !sets.isArray) return labelled;

            for (int s = 0; s < sets.arraySize; s++)
            {
                var hints = sets.GetArrayElementAtIndex(s).FindPropertyRelative("hints");
                if (hints == null || !hints.isArray) continue;
                for (int h = 0; h < hints.arraySize; h++)
                {
                    var binding = hints.GetArrayElementAtIndex(h).FindPropertyRelative("binding");
                    if (binding == null) continue;
                    var inputs = InputHintBindingMap.InputEventsFor(
                        (InputDeviceIconSetSwitcher.HintBinding)binding.intValue);
                    foreach (var entry in map.Entries)
                        if (entry != null && inputs.Contains(entry.Input))
                            labelled.Add(entry.Element);
                }
            }
            return labelled;
        }

        /// <summary>
        /// Measures the bound icons against a deterministic 1920x1080 world-space canvas, so order,
        /// pitch and size are real numbers rather than whatever the editor window happens to be.
        /// </summary>
        static void MeasureRow(GameObject prefab, List<string> issues, StringBuilder lines)
        {
            GameObject canvasGo = null, instance = null;
            try
            {
                canvasGo = new GameObject("~AbilityRowAuditCanvas", typeof(Canvas))
                    { hideFlags = HideFlags.HideAndDontSave };
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRT = (RectTransform)canvasGo.transform;
                canvasRT.sizeDelta = new Vector2(RefWidth, RefHeight);
                canvasRT.localScale = Vector3.one;

                instance = (GameObject)UnityEngine.Object.Instantiate(prefab, canvasGo.transform);
                instance.hideFlags = HideFlags.HideAndDontSave;

                var view = instance.GetComponentInChildren<VesselHUDView>(true);
                if (!view) return;

                // The HUD root may sit under a nested Canvas inside the vessel; re-parent it so it
                // stretches to the audit canvas and lays out at the reference resolution.
                var hudRT = view.transform as RectTransform;
                if (hudRT)
                {
                    hudRT.SetParent(canvasRT, false);
                    hudRT.anchorMin = Vector2.zero;
                    hudRT.anchorMax = Vector2.one;
                    hudRT.sizeDelta = Vector2.zero;
                    hudRT.anchoredPosition = Vector2.zero;
                }
                Canvas.ForceUpdateCanvases();

                var order = VesselHUDView.AbilityDisplayOrder;
                var centres = new List<(Element element, float x, float y, Vector2 size)>();
                foreach (var element in order)
                {
                    if (!view.TryGetAbilityIcon(element, out var icon)) continue;
                    var rt = icon.rectTransform;
                    Vector3 world = rt.TransformPoint(rt.rect.center);
                    Vector2 localCentre = canvasRT.InverseTransformPoint(world);
                    // slot geometry is the icon's row-level box (its parent), not the sprite itself
                    var slot = rt.parent as RectTransform ?? rt;
                    centres.Add((element, localCentre.x, localCentre.y, slot.rect.size * slot.lossyScale.x));
                }

                if (centres.Count < 2) return;

                for (int i = 1; i < centres.Count; i++)
                    if (centres[i].x < centres[i - 1].x)
                        issues.Add($"{centres[i].element} sits LEFT of {centres[i - 1].element} - the row order "
                                   + "contradicts charge → mass → space → time");

                var pitches = new List<float>();
                for (int i = 1; i < centres.Count; i++) pitches.Add(centres[i].x - centres[i - 1].x);
                if (pitches.Count > 1 && pitches.Max() - pitches.Min() > PitchTolerancePx)
                    issues.Add($"pitch is not uniform: {string.Join(" / ", pitches.Select(p => p.ToString("0.0")))} px");

                var widths = centres.Select(c => c.size.x).ToList();
                var heights = centres.Select(c => c.size.y).ToList();
                if (widths.Max() - widths.Min() > SizeTolerancePx || heights.Max() - heights.Min() > SizeTolerancePx)
                    issues.Add($"slot size is not uniform: "
                               + string.Join(" / ", centres.Select(c => $"{c.size.x:0}x{c.size.y:0}")));

                var ys = centres.Select(c => c.y).ToList();
                if (ys.Max() - ys.Min() > SizeTolerancePx)
                    issues.Add($"icons do not share one y: {string.Join(" / ", ys.Select(y => y.ToString("0.0")))}");

                lines.AppendLine("      row: " + string.Join("  ", centres.Select(c => $"{c.element}@{c.x:0}")) +
                                 $"   pitch {(pitches.Count > 0 ? pitches.Average().ToString("0.0") : "-")} px" +
                                 $"   slot {widths.Average():0}x{heights.Average():0}");
            }
            finally
            {
                if (instance) UnityEngine.Object.DestroyImmediate(instance);
                if (canvasGo) UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }
    }
}
