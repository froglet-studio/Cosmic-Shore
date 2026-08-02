using CosmicShore.Data;
using CosmicShore.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click setup for the Dolphin's four-icon ability row — the fleet-wide contract in
    /// <see cref="VesselHUDView"/>: exactly four icons in the lower right, running charge → mass →
    /// space → time, left to right, each under the element flower that upgrades it.
    ///
    /// It authors the row as real prefab content (nothing spawned at runtime), binds every serialized
    /// slot on <see cref="DolphinVesselHUDView"/>, and turns <c>tintIconOnUpgrade</c> OFF because all
    /// four Dolphin icons are live gameplay gauges — the upgrade signal rides the element badge and
    /// the scale bump instead of icon colour.
    ///
    ///   Charge → CrystalIcon (+2 carry pips)      "Twin Seed"
    ///   Mass   → DriftIcon   (radial boost fill)  "Hard Wake"
    ///   Space  → BlastIcon   (+ a blast tally)    "Clean Blast"
    ///   Time   → JawIcon     (two jaw halves)     "Live Current"
    ///
    /// Idempotent: re-running finds the objects it made last time and only re-binds. Sprites are
    /// deliberately left empty — art is an authoring decision, and the row reads as placeholder
    /// white boxes until someone assigns them. Reposition the AbilityRow container to taste.
    ///
    /// Open Assets/_Prefabs/UI Elements/VesselHUD/DolphinHUDVariant.prefab in Prefab Mode, select
    /// the root, then Tools > Cosmic Shore > Wire Dolphin Ability Row.
    /// </summary>
    public static class DolphinHUDRowWirer
    {
        const string RowName = "AbilityRow";
        const float IconSize = 96f;
        const float IconSpacing = 112f;

        [MenuItem("Tools/Cosmic Shore/Wire Dolphin Ability Row")]
        static void WireSelected()
        {
            var go = Selection.activeGameObject;
            var view = go ? go.GetComponentInChildren<DolphinVesselHUDView>(true) : null;
            if (!view)
            {
                EditorUtility.DisplayDialog("Wire Dolphin Ability Row",
                    "Select a GameObject with a DolphinVesselHUDView in its hierarchy.\n\n" +
                    "Tip: open DolphinHUDVariant.prefab in Prefab Mode and select its root.", "OK");
                return;
            }

            Wire(view);
            EditorUtility.SetDirty(view);
            EditorUtility.DisplayDialog("Wire Dolphin Ability Row",
                $"Authored the four-icon ability row on '{view.name}'.\n\n" +
                "Assign sprites to the four icons, then reposition the AbilityRow container. " +
                "Verify with Tools > Cosmic Shore > Audit Vessel Ability Rows.", "OK");
        }

        static void Wire(DolphinVesselHUDView view)
        {
            var row = EnsureChild(view.transform, RowName);
            var rowRect = row.GetComponent<RectTransform>();
            // Anchor the row to the lower right — where the fleet contract puts it.
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(1f, 0f);
            rowRect.anchoredPosition = new Vector2(-40f, 40f);
            rowRect.sizeDelta = new Vector2(IconSpacing * 4f, IconSize);

            // Left to right in the canonical element order. X is what the row validator checks.
            var crystal = EnsureIcon(row.transform, "CrystalIcon", 0);
            var drift = EnsureIcon(row.transform, "DriftIcon", 1);
            var blast = EnsureIcon(row.transform, "BlastIcon", 2);
            var jaw = EnsureIcon(row.transform, "JawIcon", 3);

            drift.type = Image.Type.Filled;
            drift.fillMethod = Image.FillMethod.Radial360;
            crystal.type = Image.Type.Filled;
            crystal.fillMethod = Image.FillMethod.Radial360;

            // Charge: two carry pips under the crystal icon. The second only shows once Twin Seed
            // raises the limit, so the row communicates capacity as well as stock.
            var pip0 = EnsurePip(crystal.transform, "CrystalPip0", -18f);
            var pip1 = EnsurePip(crystal.transform, "CrystalPip1", 18f);

            // Time: the jaw pair. Two halves that rotate apart by the same angle the hull's jaws do.
            var jawUpper = EnsureJawHalf(jaw.transform, "JawUpper", 1f);
            var jawLower = EnsureJawHalf(jaw.transform, "JawLower", -1f);

            // Space: the blast tally.
            var blastText = EnsureBlastText(blast.transform);

            var so = new SerializedObject(view);
            Bind(so, "crystalIcon", crystal);
            BindList(so, "crystalPips", pip0, pip1);
            Bind(so, "driftBoostIcon", drift);
            Bind(so, "blastIcon", blast);
            Bind(so, "blastCountText", blastText);
            Bind(so, "jawUpper", jawUpper.GetComponent<RectTransform>());
            Bind(so, "jawLower", jawLower.GetComponent<RectTransform>());

            // Every Dolphin icon is a live gauge, so colour is already spoken for.
            var tint = so.FindProperty("tintIconOnUpgrade");
            if (tint != null) tint.boolValue = false;

            BindAbilityRow(so, crystal, drift, blast, jaw);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Binds the four icons into abilityIcons in the canonical charge/mass/space/time order.</summary>
        static void BindAbilityRow(SerializedObject so, Image charge, Image mass, Image space, Image time)
        {
            var icons = so.FindProperty("abilityIcons");
            if (icons == null) return;

            var elements = VesselHUDView.AbilityDisplayOrder;
            var bound = new[] { charge, mass, space, time };

            icons.arraySize = elements.Length;
            for (int i = 0; i < elements.Length; i++)
            {
                var entry = icons.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("element").enumValueIndex = ElementEnumIndex(elements[i]);
                entry.FindPropertyRelative("icon").objectReferenceValue = bound[i];
                // upgradedSprite stays empty: authored art only, never generated.
            }
        }

        // Element is a plain enum with explicit values; SerializedProperty wants the ORDINAL.
        static int ElementEnumIndex(Element element) => element switch
        {
            Element.None => 0,
            Element.Charge => 1,
            Element.Mass => 2,
            Element.Space => 3,
            Element.Time => 4,
            Element.Omni => 5,
            _ => 0,
        };

        static void Bind(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        static void BindList(SerializedObject so, string field, params Object[] values)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static GameObject EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing) return existing.gameObject;

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static Image EnsureIcon(Transform parent, string name, int slot)
        {
            var go = EnsureChild(parent, name);
            var image = go.GetComponent<Image>();
            if (!image) image = go.AddComponent<Image>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            rect.anchoredPosition = new Vector2(slot * IconSpacing, 0f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        static Image EnsurePip(Transform parent, string name, float x)
        {
            var go = EnsureChild(parent, name);
            var image = go.GetComponent<Image>();
            if (!image) image = go.AddComponent<Image>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(22f, 22f);
            rect.anchoredPosition = new Vector2(x, -14f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        static GameObject EnsureJawHalf(Transform parent, string name, float dir)
        {
            var go = EnsureChild(parent, name);
            var image = go.GetComponent<Image>();
            if (!image) image = go.AddComponent<Image>();

            var rect = go.GetComponent<RectTransform>();
            // Pivot at the hinge (the jaw's inner edge) so a Z rotation opens the gape rather than
            // spinning the half about its own middle.
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(IconSize * 0.6f, IconSize * 0.3f);
            rect.anchoredPosition = new Vector2(-IconSize * 0.3f, dir * IconSize * 0.08f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return go;
        }

        static TMP_Text EnsureBlastText(Transform parent)
        {
            var existing = parent.Find("BlastCount");
            if (existing)
            {
                var found = existing.GetComponent<TMP_Text>();
                if (found) return found;
            }

            var go = existing ? existing.gameObject : new GameObject("BlastCount", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            if (!text) text = go.AddComponent<TextMeshProUGUI>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(IconSize, 28f);
            rect.anchoredPosition = new Vector2(0f, -18f);

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 22f;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }
    }
}
