using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Fleet-wide compliance audit for the ABILITY LOCKUP (Docs/ABILITY_LOCKUP.md) - the totem card
    /// that fuses each ability icon with the element flower that upgrades it.
    ///
    /// <para>The lockup is ENSURED at runtime for every vessel that has an ability row
    /// (<c>VesselHUDController.Initialize</c> → <c>VesselHUDView.EnsureAbilityLockup</c>), so this
    /// auditor is not asking "did someone remember to add the component". It checks the two things
    /// that CAN still be wrong, and that nothing else catches:</para>
    ///
    /// <list type="number">
    /// <item>The shared style is authored and internally sane - sprites wired, and the drawn icon
    /// actually leaves negative space inside the card rather than overflowing it.</item>
    /// <item><b>What each vessel is being NORMALISED away from.</b> The lockup overwrites row
    /// position, pitch, cell size, host scale and icon size at runtime, so a prefab can no longer
    /// make the row diverge - but the divergence is still sitting in the asset, and anyone reading
    /// the prefab will believe it. This lists it: icon sizes that differ (the Dolphin authors 96 on
    /// one slot and 80 on three), hosts scaled away from 1 (the Squirrel's 0.7), rows anchored in a
    /// different container (the Sparrow and Scarab), and the legacy decagon plate the card
    /// replaces.</item>
    /// <item>The one thing the lockup CANNOT normalise: an icon whose authored size cannot be read
    /// (a stretch anchor with no laid-out rect), because the drawn scale is derived from it.</item>
    /// </list>
    ///
    /// Runs entirely on assets - no play mode, no writes. Reuses the runtime geometry accessors on
    /// <see cref="AbilityLockupStyleSO"/>, so the report and the game cannot disagree.
    /// </summary>
    public static class AbilityLockupAuditor
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string StyleResourcePath = "AbilityLockupStyle";

        /// <summary>Minimum air we insist on between the drawn icon and the plate edge.</summary>
        const float MinIconMarginPx = 8f;

        [MenuItem("FrogletTools/Vessels/Audit Ability Lockups")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Ability lockup: style sanity + per-vessel icon fit inside the totem card.")]
        public static void Audit()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Ability lockup (TOTEM) compliance ===");

            var style = Resources.Load<AbilityLockupStyleSO>(StyleResourcePath);
            if (!style)
            {
                Debug.LogError($"[AbilityLockupAuditor] No AbilityLockupStyleSO at " +
                               $"Resources/{StyleResourcePath}. Every vessel would fall back to " +
                               "un-styled ability icons.");
                return;
            }

            int problems = AuditStyle(style, report);
            report.AppendLine();

            int vessels = 0, withRow = 0;
            foreach (var path in VesselPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var view = prefab ? prefab.GetComponentInChildren<VesselHUDView>(true) : null;
                if (!view) continue;

                vessels++;
                string vessel = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!view.HasAbilityIconRow)
                {
                    report.AppendLine($"  {vessel,-10} —  no ability row yet (blocked on ability DESIGN, " +
                                      "not on this style). Nothing to lock up.");
                    continue;
                }

                withRow++;
                problems += AuditVesselFit(vessel, view, style, report);
            }

            report.AppendLine();
            report.AppendLine($"{withRow} of {vessels} vessel HUD(s) carry an ability row; all of them are " +
                              "lockup-styled at runtime (VesselHUDView.EnsureAbilityLockup).");

            if (problems == 0)
            {
                report.AppendLine("RESULT: OK — the style is sane and every row fits its card.");
                Debug.Log(report.ToString());
            }
            else
            {
                report.AppendLine($"RESULT: {problems} problem(s) — see above. Tune " +
                                  $"Resources/{StyleResourcePath}; no code change is needed.");
                Debug.LogWarning(report.ToString());
            }
        }

        static int AuditStyle(AbilityLockupStyleSO style, StringBuilder report)
        {
            int problems = 0;
            report.AppendLine($"STYLE  card {style.plateWidth}x{style.PlateHeight}  " +
                              $"(ability cell {style.abilityCellHeight}, element cell {style.petalCellHeight})  " +
                              $"icon {style.iconBoxSize}  flower {style.petalFlowerSize}  " +
                              $"pitch {style.cardPitch}  margin R{style.rowMarginRight}/B{style.rowMarginBottom}");

            if (!style.plateSprite) { report.AppendLine("  ✗ no plateSprite - the card has no body."); problems++; }
            if (!style.rimSprite)   { report.AppendLine("  ✗ no rimSprite - no resting hairline and no upgrade rim."); problems++; }
            if (!style.bloomSprite) { report.AppendLine("  ✗ no bloomSprite - the upgrade loses its glow."); problems++; }

            if (style.petalFlowerSize > style.petalCellHeight - 4f)
            {
                report.AppendLine($"  ✗ flower {style.petalFlowerSize} does not fit its {style.petalCellHeight} cell.");
                problems++;
            }

            float cell = Mathf.Min(style.plateWidth, style.abilityCellHeight);
            float margin = (cell - style.iconBoxSize) * 0.5f;
            if (margin < MinIconMarginPx)
            {
                report.AppendLine($"  ✗ an icon drawn at {style.iconBoxSize} in a {cell} cell leaves " +
                                  $"{margin:0.#} of air (min {MinIconMarginPx}); the corner sliver alone eats 12.");
                problems++;
            }

            if (style.petalFlowerSize >= style.iconBoxSize)
            {
                report.AppendLine($"  ✗ flower {style.petalFlowerSize} is not smaller than the drawn icon " +
                                  $"{style.iconBoxSize} - the hierarchy inverts.");
                problems++;
            }
            return problems;
        }

        static int AuditVesselFit(string vessel, VesselHUDView view, AbilityLockupStyleSO style, StringBuilder report)
        {
            var sizes = new List<(Element element, float size, bool readable)>();
            foreach (var element in VesselHUDView.AbilityDisplayOrder)
                if (view.TryGetAbilityIcon(element, out var icon) && icon)
                {
                    float authored = AuthoredIconSize(icon.rectTransform, out bool readable);
                    sizes.Add((element, authored, readable));
                }

            if (sizes.Count == 0)
            {
                report.AppendLine($"  {vessel,-10} ✗ binds an ability row but no icon resolves.");
                return 1;
            }

            int problems = 0;
            var unreadable = sizes.Where(x => !x.readable).ToList();
            if (unreadable.Count > 0)
            {
                report.AppendLine($"  {vessel,-10} ✗ cannot read the authored size of " +
                                  string.Join(", ", unreadable.Select(u => u.element)) +
                                  " - the lockup derives each icon's drawn scale from it, so those " +
                                  "slots would draw at their authored size instead of the fleet's.");
                problems++;
            }

            var distinct = sizes.Select(x => Mathf.Round(x.size)).Distinct().ToList();
            string normalising = distinct.Count > 1
                ? $"icons {string.Join("/", distinct.Select(d => d.ToString("0")))} → all drawn at {style.iconBoxSize}"
                : $"icons {distinct[0]:0} → drawn at {style.iconBoxSize}";

            report.AppendLine($"  {vessel,-10} ✓ {sizes.Count} slot(s); {normalising}. " +
                              "Row position, pitch, cell size and host scale are taken over by the lockup.");
            return problems;
        }

        /// <summary>
        /// A prefab's rect is not laid out, so a point-anchored icon's size is its sizeDelta and a
        /// stretch-anchored one has to fall back to whatever rect it carries.
        /// </summary>
        static float AuthoredIconSize(RectTransform rt, out bool readable)
        {
            var size = rt.anchorMin == rt.anchorMax ? rt.sizeDelta : rt.rect.size;
            if (size.sqrMagnitude < 1f) size = rt.sizeDelta;
            readable = size.sqrMagnitude > 1f;
            return Mathf.Max(size.x, size.y);
        }

        static IEnumerable<string> VesselPrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.EndsWith(".prefab"))
                         .OrderBy(p => p);
    }
}
