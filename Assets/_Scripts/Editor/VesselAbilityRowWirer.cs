using System;
using CosmicShore.Data;
using CosmicShore.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Builds or repairs a vessel's <b>four-icon ability row</b> — the fleet-wide contract that every
    /// vessel HUD shows exactly four ability icons in the lower right, in the order
    /// charge → mass → space → time, each under the element that upgrades it (CLAUDE.md ▸ "The
    /// Four-Icon Ability Row (LOCKED structure)").
    ///
    /// <para>Because that contract is fleet-wide, so is this tool. It was the Dolphin's private
    /// wirer until 2026-08-17; nothing about the row's geometry was ever Dolphin-specific, and three
    /// vessels (Manta, Rhino, Serpent) still report <b>0/4 icons</b> against the audit. Pointing this
    /// at one of them creates the whole row from nothing, correctly placed and correctly bound —
    /// which is the entire mechanical half of bringing a vessel into compliance. The remaining half
    /// is design (authoring that vessel's `ElementalAbilityMapSO`), which no tool can do.</para>
    ///
    /// <para><b>The layout is not a judgement call.</b> Four buttons parented to the HUD root sharing
    /// one Y band, at four equal X bands across the lower right, each holding an 80×80 icon. The
    /// numbers are lifted verbatim from <c>SquirrelHUDVariant.prefab</c>, which established them —
    /// change them here and you change the fleet, so don't.</para>
    ///
    /// <para><b>Generated vs ADOPTED slots.</b> A slot whose gauge is authored art (the Dolphin's
    /// 11-step boost ring) is adopted by name, never re-created: re-authoring a widget from scratch
    /// would throw away art the vessel already ships, so a missing one is reported as a fault rather
    /// than papered over with a generated square. A vessel with nothing authored gets a plain
    /// <c>{Element}Icon</c> per slot, ready for art.</para>
    ///
    /// <para><b>Idempotent</b> — it finds objects by name and only re-binds, so running it on an
    /// already-correct prefab changes nothing. Verified against the shipped Dolphin row: every value
    /// it would write already matches the prefab. Sprites are never touched; art is an authoring
    /// decision, and the one exception (a slot whose visible gauge is a generated CHILD) is called
    /// out at its call site.</para>
    ///
    /// Open the HUD prefab in Prefab Mode, select the root, then
    /// <b>FrogletTools ▸ Vessels ▸ Wire Vessel Ability Row</b>. Verify with
    /// <b>FrogletTools ▸ Vessels ▸ Audit Vessel Ability Rows</b>.
    /// </summary>
    public static class VesselAbilityRowWirer
    {
        const float IconSize = 80f;

        // THE fleet standard, from SquirrelHUDVariant.prefab. One Y band, four equal X bands, in
        // AbilityDisplayOrder (charge → mass → space → time), left to right.
        const float BandYMin = 0.027730448f;
        const float BandYMax = 0.1665395f;
        static readonly (float xMin, float xMax)[] BandsX =
        {
            (0.68481258f, 0.76293758f),
            (0.75652886f, 0.83465386f),
            (0.82824515f, 0.90637015f),
            (0.89996143f, 0.97808643f),
        };

        /// <summary>
        /// What one slot is called on a given vessel, and whether its icon is generated or adopted.
        ///
        /// The names matter because they are what makes the tool idempotent on a vessel whose row was
        /// authored before this tool existed: the Dolphin's slots are called Profile/Crystal/Jaw/Drift
        /// rather than Charge/Mass/Space/Time, and renaming them would churn a shipped prefab to no
        /// benefit.
        /// </summary>
        readonly struct SlotSpec
        {
            public readonly string ButtonName;
            public readonly string IconName;
            /// <summary>Non-null = adopt this existing child by name instead of generating an icon.</summary>
            public readonly string AdoptChildNamed;

            public SlotSpec(string buttonName, string iconName, string adoptChildNamed = null)
            {
                ButtonName = buttonName;
                IconName = iconName;
                AdoptChildNamed = adoptChildNamed;
            }
        }

        /// <summary>
        /// A vessel with no authored row gets element-named slots. A vessel whose row predates this
        /// tool keeps the names it already ships with — see <see cref="SlotSpec"/>.
        /// </summary>
        static SlotSpec[] ProfileFor(VesselHUDView view)
        {
            if (view is DolphinVesselHUDView)
                return new[]
                {
                    new SlotSpec("ProfileButton", "ProfileIcon"),                 // Charge — Echo Sight
                    new SlotSpec("CrystalButton", "CrystalIcon"),                 // Mass   — crystal seeding
                    new SlotSpec("JawButton",     "JawIcon"),                     // Space  — cone blast
                    new SlotSpec("DriftButton",   null, "Boost Display"),         // Time   — the authored ring
                };

            var order = VesselHUDView.AbilityDisplayOrder;
            var slots = new SlotSpec[order.Length];
            for (int i = 0; i < order.Length; i++)
                slots[i] = new SlotSpec($"{order[i]}Button", $"{order[i]}Icon");
            return slots;
        }

        [MenuItem("FrogletTools/Vessels/Wire Vessel Ability Row")]
        static void WireSelected()
        {
            var go = Selection.activeGameObject;
            var view = go ? go.GetComponentInChildren<VesselHUDView>(true) : null;
            if (!view)
            {
                EditorUtility.DisplayDialog("Wire Vessel Ability Row",
                    "Select a GameObject with a VesselHUDView in its hierarchy.\n\n" +
                    "Tip: open the vessel's HUD variant prefab in Prefab Mode and select its root.", "OK");
                return;
            }

            var icons = WireRow(view);
            if (view is DolphinVesselHUDView dolphin) WireDolphinGauges(dolphin, icons);

            EditorUtility.SetDirty(view);

            int bound = 0;
            foreach (var i in icons) if (i) bound++;
            EditorUtility.DisplayDialog("Wire Vessel Ability Row",
                $"Placed and bound {bound}/4 ability icons on '{view.name}' at the fleet-standard bands.\n\n" +
                (bound < 4 ? "A slot is unbound — see the Console for which and why.\n\n" : "") +
                "Verify with FrogletTools > Vessels > Audit Vessel Ability Rows.", "OK");
        }

        /// <summary>
        /// The generic half: place the four bands, resolve an icon per slot, bind the row. Returns the
        /// four icons in <see cref="VesselHUDView.AbilityDisplayOrder"/>, with nulls for slots whose
        /// adopted widget is missing.
        /// </summary>
        static Image[] WireRow(VesselHUDView view)
        {
            var root = view.transform;
            var slots = ProfileFor(view);
            var icons = new Image[BandsX.Length];

            for (int i = 0; i < BandsX.Length; i++)
            {
                var (xMin, xMax) = BandsX[i];
                var spec = slots[i];

                // The BUTTON carries the position; sizeDelta 0 means "exactly the anchor band".
                var button = EnsureChild(root, spec.ButtonName);
                var brt = button.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(xMin, BandYMin);
                brt.anchorMax = new Vector2(xMax, BandYMax);
                brt.pivot = new Vector2(0.5f, 0.5f);
                brt.anchoredPosition = Vector2.zero;
                brt.sizeDelta = Vector2.zero;

                if (spec.AdoptChildNamed != null)
                {
                    icons[i] = Adopt(button.transform, spec.AdoptChildNamed, view);
                    continue;
                }

                icons[i] = EnsureIcon(button.transform, spec.IconName);
            }

            var so = new SerializedObject(view);
            BindAbilityRow(so, icons);
            so.ApplyModifiedPropertiesWithoutUndo();
            return icons;
        }

        /// <summary>
        /// An adopted slot's own Image. Never created: it is authored art, often with nested glyphs,
        /// so a missing one is a fault to report rather than to paper over with a generated square.
        /// </summary>
        // UnityEngine.Object is spelled out because this file imports System for Array/Enum
        // (OrdinalOf below), which makes a bare `Object` ambiguous with System.Object (CS0104).
        static Image Adopt(Transform band, string childName, UnityEngine.Object context)
        {
            var existing = band.Find(childName);
            var image = existing ? existing.GetComponent<Image>() : null;
            if (!image)
                Debug.LogWarning($"[VesselAbilityRowWirer] The '{band.name}' band has no '{childName}' " +
                                 "— that slot's authored gauge is missing from this HUD. Restore it " +
                                 "rather than letting this tool draw a replacement; the slot is left " +
                                 "unbound.", context);
            return image;
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
            => Array.IndexOf((Element[])Enum.GetValues(typeof(Element)), element);

        static void Bind(SerializedObject so, string field, UnityEngine.Object value)
        {
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
        }

        // ---------------------------------------------------------------
        // Per-vessel gauges. Everything above is fleet-generic; everything below belongs to one
        // vessel's readouts and is invoked only for that vessel's view type. A second vessel with
        // its own gauges gets its own method here, NOT a new abstraction — there is exactly one
        // today, and three similar lines beat a premature interface.
        // ---------------------------------------------------------------

        /// <summary>
        /// The Dolphin's four gauges: a generated blast-profile capsule + two stacked living tallies
        /// (Charge), the seeding recharge wipe (Mass), the jaw pair + prism tally (Space), and the
        /// adopted boost ring (Time, already bound by the generic pass).
        /// </summary>
        static void WireDolphinGauges(DolphinVesselHUDView view, Image[] icons)
        {
            var profile = icons[0];
            var crystal = icons[1];
            var jaw = icons[2];
            var drift = icons[3];
            if (!profile || !crystal || !jaw) return;

            // The Charge slot's icon is a transparent CONTAINER; the generated profile is the gauge.
            // This is the one sanctioned exception to "sprites are never touched": a leftover sprite
            // would draw UNDER the readout.
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
            // bump alone here. Deliberately NOT set by the generic pass: a vessel whose icons are
            // static art SHOULD tint and badge, so this is a per-vessel decision.
            var tint = so.FindProperty("tintIconOnUpgrade");
            if (tint != null) tint.boolValue = false;
            var badge = so.FindProperty("showUpgradeBadge");
            if (badge != null) badge.boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------
        // Shared builders
        // ---------------------------------------------------------------

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
        /// Strips a slot's icon back to an invisible container, for a slot whose visible gauge is a
        /// generated CHILD. Sprites are otherwise never touched by this tool.
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
