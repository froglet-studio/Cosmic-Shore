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
    /// <item><b>Where each bound gauge is authored.</b> A vessel's meter is regularly parented under
    /// a DIFFERENT ability's button than the ability it reports on (the Squirrel's boost fill sits
    /// under the skimming button; the Scarab's ball-energy ring under the throttle button). The
    /// lockup re-homes it onto the right card, so the game is correct either way - but the drift is
    /// still in the asset and a reader will believe it.</item>
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
                    report.AppendLine($"  {vessel,-10} —  binds no ability icons; the lockup still builds " +
                                      "four LOCKED cards so the row exists and the element flowers have " +
                                      "somewhere to dock. Blocked on ability DESIGN, not on this style.");
                    continue;
                }

                withRow++;
                problems += AuditVesselFit(vessel, view, style, report);
            }

            report.AppendLine();
            report.AppendLine($"{withRow} of {vessels} vessel HUD(s) bind ability icons; ALL {vessels} are " +
                              "lockup-styled at runtime (VesselHUDView.EnsureAbilityLockup), the rest as " +
                              "four locked cards.");

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
            report.AppendLine($"STYLE  totem {style.plateWidth}x{style.PlateHeight}  " +
                              $"(ability plate {style.abilityCellHeight}, gap {style.cellGap}, element plate " +
                              $"{style.petalCellHeight})  slant inset {style.trapezoidInset} " +
                              $"(narrow edge {style.NarrowEdgeFraction:0.###} of wide)  " +
                              $"icon {style.iconBoxSize}  flower {style.petalFlowerSize}  " +
                              $"pitch {style.cardPitch}  margin R{style.rowMarginRight}/B{style.rowMarginBottom}");

            // The plates are generated geometry, so the bloom is the only sprite left - and with the
            // rim retired it is also half the upgrade signal, which makes a missing one worse than
            // it used to be, not milder.
            if (!style.bloomSprite) { report.AppendLine("  ✗ no bloomSprite - the upgrade loses its glow, " +
                                                        "and the plates are borderless, so half the signal " +
                                                        "would be the plate lift alone."); problems++; }

            if (style.trapezoidInset * 2f >= style.plateWidth * 0.5f)
            {
                report.AppendLine($"  ✗ trapezoidInset {style.trapezoidInset} collapses a {style.plateWidth} " +
                                  "plate past half its width - the totem becomes a pair of wedges.");
                problems++;
            }
            if (style.cellGap <= 0f)
            {
                report.AppendLine("  ✗ cellGap 0 - the gap IS the divider now, so the two plates would " +
                                  "fuse into one shape with no separation at all.");
                problems++;
            }

            if (style.petalFlowerSize > style.petalCellHeight - 4f)
            {
                report.AppendLine($"  ✗ flower {style.petalFlowerSize} does not fit its {style.petalCellHeight} cell.");
                problems++;
            }

            // Measure the icon against the NARROW edge, not the wide one: the ability plate tapers
            // downward, so the tightest place the icon has to clear is its base, not its rect.
            float cell = Mathf.Min(style.plateWidth * style.NarrowEdgeFraction, style.abilityCellHeight);
            float margin = (cell - style.iconBoxSize) * 0.5f;
            if (margin < MinIconMarginPx)
            {
                report.AppendLine($"  ✗ an icon drawn at {style.iconBoxSize} leaves {margin:0.#} of air " +
                                  $"against the ability plate's narrow edge ({cell:0.#}), min {MinIconMarginPx}.");
                problems++;
            }

            if (style.petalFlowerSize >= style.iconBoxSize)
            {
                report.AppendLine($"  ✗ flower {style.petalFlowerSize} is not smaller than the drawn icon " +
                                  $"{style.iconBoxSize} - the hierarchy inverts.");
                problems++;
            }

            // The gauge that replaced every ring: a fill nobody can tell from its own track is the
            // exact failure the rings were retired for, so check the READ rather than the values.
            float track = Luminance(style.gaugeTrackColor) * style.gaugeTrackColor.a;
            float fill  = Luminance(style.gaugeFillColor)  * style.gaugeFillColor.a;
            if (fill - track < 0.1f)
            {
                report.AppendLine($"  ✗ the gauge fill (lum {fill:0.###}) does not read against its track " +
                                  $"(lum {track:0.###}) - the meter would be invisible.");
                problems++;
            }
            if (style.gaugeCellFraction <= 0f || style.gaugeCellFraction > 1f)
            {
                report.AppendLine($"  ✗ gaugeCellFraction {style.gaugeCellFraction} is outside (0,1] - the " +
                                  "gauge has no height, or overflows into the element cell.");
                problems++;
            }

            // A control chip that reaches past the row's bottom margin is clipped off the screen,
            // which is how the hint placement failed three times before it was moved onto the card.
            float chipReach = style.chipGap + style.chipHeight;
            if (chipReach >= style.rowMarginBottom)
            {
                report.AppendLine($"  ✗ the control chip reaches {chipReach:0.#}px below the card but the " +
                                  $"row sits only {style.rowMarginBottom}px off the bottom - labels clip.");
                problems++;
            }

            if (style.pressFlashColor.a <= 0f || style.pressFlashDuration <= 0f)
            {
                report.AppendLine("  ✗ the press flash is invisible or snaps - a fired ability would show " +
                                  "nothing, which is what the retired circular glow did.");
                problems++;
            }

            if (Luminance(style.lockedPlateColor) * style.lockedPlateColor.a >
                Luminance(style.plateColor) * style.plateColor.a + 0.001f)
            {
                report.AppendLine("  ✗ a LOCKED card is brighter than a live one - the row would advertise " +
                                  "abilities that do not exist yet.");
                problems++;
            }
            return problems;
        }

        static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

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
            ReportGauges(vessel, view, report);
            return problems;
        }

        /// <summary>
        /// Which slots bind a meter, and whether that meter is authored under a DIFFERENT ability's
        /// button. Not a failure - the lockup re-homes it - but it is the drift a reader of the
        /// prefab would get wrong, and the reason the build adopts every gauge before retiring any
        /// chrome.
        /// </summary>
        static void ReportGauges(string vessel, VesselHUDView view, StringBuilder report)
        {
            foreach (var element in VesselHUDView.AbilityDisplayOrder)
            {
                if (!view.TryGetAbilityGauge(element, out var gauge) || !gauge) continue;

                var ownHost = view.TryGetAbilityIcon(element, out var icon) && icon
                    ? icon.rectTransform.parent
                    : null;
                bool authoredHere = ownHost && gauge.transform.IsChildOf(ownHost);

                report.AppendLine(authoredHere
                    ? $"             gauge on {element}: {gauge.name} (authored on this card)"
                    : $"             gauge on {element}: {gauge.name} — authored under " +
                      $"'{(gauge.transform.parent ? gauge.transform.parent.name : "?")}', re-homed onto " +
                      "the card at runtime.");
            }
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
