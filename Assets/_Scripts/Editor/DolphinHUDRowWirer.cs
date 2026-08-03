using CosmicShore.Data;
using CosmicShore.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Repair/re-bind tool for the Dolphin's four-icon ability row. The row is already AUTHORED in
    /// DolphinHUDVariant.prefab — this exists so a broken or hand-edited HUD can be put back on the
    /// standard without re-deriving it, and so the layout has exactly one definition in code.
    ///
    /// The layout is the fleet standard, not a judgement call: four buttons parented to the HUD
    /// root, sharing one Y band, at four equal X bands across the lower right, each holding an
    /// 80×80 <c>Icon</c>. The numbers below are lifted verbatim from SquirrelHUDVariant.prefab,
    /// which established them — change them here and you are changing the fleet, so don't.
    ///
    ///   Charge → CrystalIcon (+2 carry pips)     "Twin Seed"
    ///   Mass   → DriftIcon   (radial boost fill) "Hard Wake"
    ///   Space  → BlastIcon   (+ a prism tally)   "Clean Blast"
    ///   Time   → JawIcon     (two jaw halves)    "Live Current"
    ///
    /// Idempotent: it finds objects by name and only re-binds, so running it on the authored prefab
    /// changes nothing. Sprites are never touched — art is an authoring decision.
    ///
    /// Open the prefab in Prefab Mode, select the root, then
    /// Tools > Cosmic Shore > Wire Dolphin Ability Row.
    /// </summary>
    public static class DolphinHUDRowWirer
    {
        const float IconSize = 80f;

        // THE fleet standard, from SquirrelHUDVariant.prefab. One Y band, four equal X bands.
        const float BandYMin = 0.027730448f;
        const float BandYMax = 0.1665395f;
        static readonly (string slot, float xMin, float xMax)[] Bands =
        {
            ("Crystal", 0.68481258f, 0.76293758f),
            ("Drift",   0.75652886f, 0.83465386f),
            ("Blast",   0.82824515f, 0.90637015f),
            ("Jaw",     0.89996143f, 0.97808643f),
        };

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
                $"Re-bound the four-icon ability row on '{view.name}' to the fleet-standard bands.\n\n" +
                "Verify with Tools > Cosmic Shore > Audit Vessel Ability Rows.", "OK");
        }

        static void Wire(DolphinVesselHUDView view)
        {
            var root = view.transform;

            var icons = new Image[Bands.Length];
            for (int i = 0; i < Bands.Length; i++)
            {
                var (slot, xMin, xMax) = Bands[i];

                // The BUTTON carries the position; sizeDelta 0 means "exactly the anchor band".
                var button = EnsureChild(root, $"{slot}Button");
                var brt = button.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(xMin, BandYMin);
                brt.anchorMax = new Vector2(xMax, BandYMax);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = Vector2.zero;
                brt.sizeDelta = Vector2.zero;

                icons[i] = EnsureIcon(button.transform, $"{slot}Icon");
            }

            var crystal = icons[0];
            var drift = icons[1];
            var blast = icons[2];
            var jaw = icons[3];

            // Radial fills: the recharge wipe and the drift-boost meter.
            crystal.type = Image.Type.Filled;
            crystal.fillMethod = Image.FillMethod.Radial360;
            drift.type = Image.Type.Filled;
            drift.fillMethod = Image.FillMethod.Radial360;

            var pip0 = EnsurePip(crystal.transform, "CrystalPip0", -18f);
            var pip1 = EnsurePip(crystal.transform, "CrystalPip1", 18f);
            var jawUpper = EnsureJawHalf(jaw.transform, "JawUpper");
            var jawLower = EnsureJawHalf(jaw.transform, "JawLower");
            var blastText = EnsureBlastText(blast.transform);

            var so = new SerializedObject(view);
            Bind(so, "crystalIcon", crystal);
            BindList(so, "crystalPips", pip0, pip1);
            Bind(so, "driftBoostIcon", drift);
            Bind(so, "blastIcon", blast);
            Bind(so, "blastCountText", blastText);
            Bind(so, "jawUpper", jawUpper);
            Bind(so, "jawLower", jawLower);

            // Every Dolphin icon is a live gauge, so colour is already spoken for - the upgrade
            // signal rides the element badge and the scale bump instead.
            var tint = so.FindProperty("tintIconOnUpgrade");
            if (tint != null) tint.boolValue = false;

            BindAbilityRow(so, icons);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Binds the icons into abilityIcons in the canonical charge/mass/space/time order.</summary>
        static void BindAbilityRow(SerializedObject so, Image[] icons)
        {
            var prop = so.FindProperty("abilityIcons");
            if (prop == null) return;

            var elements = VesselHUDView.AbilityDisplayOrder;
            prop.arraySize = elements.Length;
            for (int i = 0; i < elements.Length; i++)
            {
                var entry = prop.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("element").enumValueIndex = OrdinalOf(elements[i]);
                entry.FindPropertyRelative("icon").objectReferenceValue = icons[i];
                // upgradedSprite stays empty: authored art only, never generated.
            }
        }

        // SerializedProperty.enumValueIndex wants the DECLARATION ORDINAL, not the enum's value.
        // Element happens to declare None=0 … Omni=5 in order so the two coincide today; computing
        // it means a future renumbering cannot silently bind the wrong element.
        static int OrdinalOf(Element element)
            => System.Array.IndexOf((Element[])System.Enum.GetValues(typeof(Element)), element);

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
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return go;
        }

        static Image EnsureIcon(Transform parent, string name)
        {
            var go = EnsureChild(parent, name);
            var image = go.GetComponent<Image>() ? go.GetComponent<Image>() : go.AddComponent<Image>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        static Image EnsurePip(Transform parent, string name, float x)
        {
            var go = EnsureChild(parent, name);
            var image = go.GetComponent<Image>() ? go.GetComponent<Image>() : go.AddComponent<Image>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(22f, 22f);
            rect.anchoredPosition = new Vector2(x, -14f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        static RectTransform EnsureJawHalf(Transform parent, string name)
        {
            var go = EnsureChild(parent, name);
            var image = go.GetComponent<Image>() ? go.GetComponent<Image>() : go.AddComponent<Image>();

            var rect = go.GetComponent<RectTransform>();
            // The vessel's own jaw art (DolphinTopJaw / DolphinBottomJaw) runs blunt-end-RIGHT and
            // tapers to the tip on the LEFT, so the hinge is the RIGHT edge and the rect takes the
            // sprite's 272:50 aspect (preserveAspect would letterbox anything else). Both halves
            // share the hinge exactly: at zero energy they close with no seam, and rotation alone
            // opens the maw.
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(78f, 14f);
            rect.anchoredPosition = new Vector2(39f, 0f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            return rect;
        }

        static TMP_Text EnsureBlastText(Transform parent)
        {
            var go = EnsureChild(parent, "BlastCount");
            var text = go.GetComponent<TextMeshProUGUI>();
            if (!text) text = go.AddComponent<TextMeshProUGUI>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(IconSize, 28f);
            rect.anchoredPosition = new Vector2(0f, -18f);

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 22f;
            text.raycastTarget = false;
            return text;
        }
    }
}
