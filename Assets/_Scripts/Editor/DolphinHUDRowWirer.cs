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
    ///   Charge → ProfileIcon   (generated profile + a living tally) "Pilot Echo"
    ///   Mass   → CrystalIcon   (the seeding recharge fill)       "Claimed Seed"
    ///   Space  → JawIcon       (jaw halves + a prism tally)      "Clean Blast"
    ///   Time   → Boost Display (the authored 11-step ring)       "Live Current"
    ///
    /// The Time slot is the Dolphin's PRE-EXISTING boost gauge, adopted into the row rather than
    /// replaced. This tool never creates it — if 'Boost Display' is missing from the Time band the
    /// slot is left unbound and reported, because re-authoring that widget from scratch would throw
    /// away art the vessel already ships.
    ///
    /// The Charge slot's ProfileIcon is a TRANSPARENT container, the same arrangement JawIcon uses:
    /// the visible gauge is its generated child, so the row's upgrade signal and the live readout
    /// can never contest one object.
    ///
    /// Idempotent: it finds objects by name and only re-binds, so running it on the authored prefab
    /// changes nothing. Sprites are never touched — art is an authoring decision.
    ///
    /// Open the prefab in Prefab Mode, select the root, then
    /// FrogletTools > Vessels > Wire Dolphin Ability Row.
    /// </summary>
    public static class DolphinHUDRowWirer
    {
        const float IconSize = 80f;

        // THE fleet standard, from SquirrelHUDVariant.prefab. One Y band, four equal X bands.
        const float BandYMin = 0.027730448f;
        const float BandYMax = 0.1665395f;
        static readonly (string slot, float xMin, float xMax)[] Bands =
        {
            ("Profile", 0.68481258f, 0.76293758f),
            ("Crystal", 0.75652886f, 0.83465386f),
            ("Jaw",     0.82824515f, 0.90637015f),
            ("Drift",   0.89996143f, 0.97808643f),
        };

        [MenuItem("FrogletTools/Vessels/Wire Dolphin Ability Row")]
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
                "Verify with FrogletTools > Vessels > Audit Vessel Ability Rows.", "OK");
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

                // Time is the adopted boost ring, not a generated icon.
                icons[i] = slot == "Drift"
                    ? FindBoostRing(button.transform)
                    : EnsureIcon(button.transform, $"{slot}Icon");
            }

            var profile = icons[0];
            var crystal = icons[1];
            var jaw = icons[2];
            var drift = icons[3];

            if (!drift)
                Debug.LogWarning("[DolphinHUDRowWirer] The Time band has no 'Boost Display' — the " +
                                 "boost ring is missing from this HUD. Restore it rather than " +
                                 "letting this tool draw a replacement.", view);

            // The Charge slot's icon is a transparent CONTAINER; the generated profile is the gauge.
            MakeTransparentContainer(profile);
            var profileGraphic = EnsureProfile(profile.transform);
            var pilotText = EnsureTallyText(profile.transform, "PilotCount", -18f);
            var faunaText = EnsureTallyText(profile.transform, "FaunaCount", -44f);

            // The recharge wipe. (The boost ring steps through authored sprites instead.)
            crystal.type = Image.Type.Filled;
            crystal.fillMethod = Image.FillMethod.Radial360;

            var jawUpper = EnsureJawHalf(jaw.transform, "JawUpper");
            var jawLower = EnsureJawHalf(jaw.transform, "JawLower");
            var blastText = EnsureBlastText(jaw.transform);

            var so = new SerializedObject(view);
            Bind(so, "blastProfile", profileGraphic);
            Bind(so, "pilotCountText", pilotText);
            Bind(so, "faunaCountText", faunaText);
            Bind(so, "crystalIcon", crystal);
            Bind(so, "driftBoostIcon", drift);
            Bind(so, "blastCountText", blastText);
            Bind(so, "jawUpper", jawUpper);
            Bind(so, "jawLower", jawLower);

            // Every Dolphin icon is a live gauge, so colour is already spoken for, and these four
            // are busy enough (generated profile, recharge wipe, jaw pair + tally, stepped ring)
            // that a corner badge just clutters them - the upgrade signal rides the persistent scale
            // bump alone here.
            var tint = so.FindProperty("tintIconOnUpgrade");
            if (tint != null) tint.boolValue = false;
            var badge = so.FindProperty("showUpgradeBadge");
            if (badge != null) badge.boolValue = false;

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

        /// <summary>
        /// The adopted boost gauge's own Image — the stepped ring. Never created here: it is
        /// authored art with a nested glyph, so a missing one is a fault to report, not to paper
        /// over with a generated square.
        /// </summary>
        static Image FindBoostRing(Transform band)
        {
            var existing = band.Find("Boost Display");
            return existing ? existing.GetComponent<Image>() : null;
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

        /// <summary>
        /// Strips a slot's icon back to an invisible container. Sprites are otherwise never touched
        /// by this tool (art is an authoring decision) — the exception is a slot whose gauge is a
        /// generated child, where a leftover sprite would draw UNDER the readout.
        /// </summary>
        static void MakeTransparentContainer(Image icon)
        {
            if (!icon) return;
            icon.sprite = null;
            icon.type = Image.Type.Simple;
            icon.color = new Color(1f, 1f, 1f, 0f);
        }

        static BlastProfileGraphic EnsureProfile(Transform parent)
        {
            var go = EnsureChild(parent, "Profile");
            var graphic = go.GetComponent<BlastProfileGraphic>();
            if (!graphic) graphic = go.AddComponent<BlastProfileGraphic>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(IconSize * 0.9f, IconSize * 0.9f);
            graphic.raycastTarget = false;
            return graphic;
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

        /// <summary>
        /// One of the Charge slot's two stacked tallies (pilots debuffed / creatures killed). Same
        /// grammar as the Space slot's prism tally — a bare centred number — because the row should
        /// read as one language; the two are told apart by COLOUR, which the view resolves from the
        /// shared palette rather than from anything authored here.
        /// </summary>
        static TMP_Text EnsureTallyText(Transform parent, string name, float y)
        {
            var go = EnsureChild(parent, name);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (!text) text = go.AddComponent<TextMeshProUGUI>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(120f, 26f);
            rect.anchoredPosition = new Vector2(0f, y);

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 22f;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        static TMP_Text EnsureBlastText(Transform parent)
        {
            var go = EnsureChild(parent, "BlastCount");
            var text = go.GetComponent<TextMeshProUGUI>();
            if (!text) text = go.AddComponent<TextMeshProUGUI>();

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            // Its own row beneath the jaws, and wider than the icon, so a five-figure claim renders
            // at full size instead of auto-shrinking into the gape.
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(120f, 30f);
            rect.anchoredPosition = new Vector2(0f, -20f);

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 22f;
            text.raycastTarget = false;
            return text;
        }
    }
}
