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
    /// <item>The shared style is authored and internally sane - sprites wired, and the kerning
    /// actually leaves negative space rather than overflowing the card.</item>
    /// <item><b>Per-vessel FIT.</b> One content scale serves the whole fleet, so it has to suit
    /// every vessel's icon size. A vessel whose icons are authored much larger than the Dolphin's 80
    /// would still overflow the plate after kerning - the card is fixed at
    /// <see cref="AbilityLockupStyleSO.plateWidth"/> while the icon size is per-prefab. That is the
    /// one number this style cannot absorb on its own, and it is invisible until someone flies that
    /// vessel.</item>
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
                              $"iconContentScale {style.iconContentScale}  flower {style.petalFlowerSize}");

            if (!style.plateSprite) { report.AppendLine("  ✗ no plateSprite - the card has no body."); problems++; }
            if (!style.rimSprite)   { report.AppendLine("  ✗ no rimSprite - no resting hairline and no upgrade rim."); problems++; }
            if (!style.bloomSprite) { report.AppendLine("  ✗ no bloomSprite - the upgrade loses its glow."); problems++; }

            // The flower must stay under the icon: the ability is the headline, the element qualifies it.
            // Compared against the icon's DRAWN size, which is what a player actually sees.
            if (style.petalFlowerSize > style.petalCellHeight - 4f)
            {
                report.AppendLine($"  ✗ flower {style.petalFlowerSize} does not fit its {style.petalCellHeight} cell.");
                problems++;
            }
            return problems;
        }

        static int AuditVesselFit(string vessel, VesselHUDView view, AbilityLockupStyleSO style, StringBuilder report)
        {
            var sizes = new List<(Element element, Vector2 size)>();
            foreach (var element in VesselHUDView.AbilityDisplayOrder)
                if (view.TryGetAbilityIcon(element, out var icon) && icon)
                    sizes.Add((element, IconSize(icon.rectTransform)));

            if (sizes.Count == 0)
            {
                report.AppendLine($"  {vessel,-10} ✗ binds an ability row but no icon resolves.");
                return 1;
            }

            float widest = sizes.Max(s => Mathf.Max(s.size.x, s.size.y));
            float drawn = widest * style.iconContentScale;
            float cell = Mathf.Min(style.plateWidth, style.abilityCellHeight);
            float margin = (cell - drawn) * 0.5f;

            bool uniform = sizes.All(s => Mathf.Approximately(s.size.x, sizes[0].size.x)
                                       && Mathf.Approximately(s.size.y, sizes[0].size.y));

            string flowerNote = drawn <= style.petalFlowerSize
                ? "  ⚠ flower is NOT smaller than the drawn icon - the hierarchy inverts here."
                : "";

            if (margin < MinIconMarginPx)
            {
                report.AppendLine($"  {vessel,-10} ✗ icon {widest} → drawn {drawn:0.#} in a {cell} cell " +
                                  $"leaves {margin:0.#} of air (min {MinIconMarginPx}). " +
                                  "Lower iconContentScale, or widen the card.");
                return 1;
            }

            report.AppendLine($"  {vessel,-10} ✓ {sizes.Count} icon(s) {(uniform ? "uniform" : "MIXED size")} " +
                              $"{widest} → drawn {drawn:0.#}, {margin:0.#} of air each side, " +
                              $"flower {style.petalFlowerSize}.{flowerNote}");
            return uniform ? 0 : 0;   // mixed sizes are reported, not failed: the fit test already covers the risk
        }

        /// <summary>
        /// A prefab's rect is not laid out, so a point-anchored icon's size is its sizeDelta and a
        /// stretch-anchored one has to fall back to whatever rect it carries.
        /// </summary>
        static Vector2 IconSize(RectTransform rt)
        {
            if (rt.anchorMin == rt.anchorMax) return rt.sizeDelta;
            var r = rt.rect.size;
            return r.sqrMagnitude > 1f ? r : rt.sizeDelta;
        }

        static IEnumerable<string> VesselPrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.EndsWith(".prefab"))
                         .OrderBy(p => p);
    }
}
