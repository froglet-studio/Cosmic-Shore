using System;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>The type roles of <c>Docs/STYLE_FOUNDATION.md</c> §4, plus the caps LABEL the style-B
    /// panels use. Explicit values: this enum is serialized on every <see cref="KitText"/>.</summary>
    public enum UITypeRole
    {
        Display = 0,
        H1 = 1,
        H2 = 2,
        H3 = 3,
        Body = 4,
        BodySmall = 5,
        Button = 6,
        ButtonSmall = 7,
        /// <summary>Section and field labels: Button-small face and size, wider tracking (style B).</summary>
        Label = 8,
        DataLarge = 9,
        Data = 10,
        DataSmall = 11,
    }

    /// <summary>Which of the two §4 faces a role sets in.</summary>
    public enum UITypeFace
    {
        /// <summary>Aldrich - headings, body and every Data role.</summary>
        Aldrich = 0,
        /// <summary>Chakra Petch SemiBold - buttons and caps labels, always upper case.</summary>
        ChakraPetchSemiBold = 1,
    }

    /// <summary>
    /// The §4 type scale as data: one row per <see cref="UITypeRole"/> with its face, size at the
    /// 1920 reference, tracking, case and whether its numerals are tabular. Read by
    /// <see cref="KitText"/>, which applies a row to a TMP text - so a size or tracking change is
    /// one edit here, not a sweep of every label in the menu.
    ///
    /// This is task T6's type-role deliverable in component form rather than as a TMP style sheet:
    /// a style sheet can only reach a font by NAME through a Resources path, and its styles are
    /// opening tags invisible in the inspector, while a role row binds the font asset by reference
    /// and shows on every label it drives (Docs/UI_REDESIGN_TASKS.md T6, deviation recorded there).
    /// </summary>
    [CreateAssetMenu(fileName = "UITypeRoles", menuName = "ScriptableObjects/UI/UI Type Roles")]
    public class UITypeRolesSO : ScriptableObject
    {
        [Serializable]
        public struct RoleSpec
        {
            [Tooltip("Which role this row defines. Rows are looked up by this value, not by position.")]
            public UITypeRole role;
            [Tooltip("Aldrich (headings, body, data) or Chakra Petch SemiBold (buttons, labels).")]
            public UITypeFace face;
            [Tooltip("Size in canvas units at the 1920x1080 reference (§4 PC column).")]
            public float size;
            [Tooltip("TMP character spacing (1/100 em). Buttons 8 = 0.08em, labels 14 = 0.14em.")]
            public float characterSpacing;
            [Tooltip("Render upper case. §4: caps on buttons is unconditional.")]
            public bool upperCase;
            [Tooltip("Live numerals render column-aligned via <mspace> in this face (§4 tabular numerics).")]
            public bool tabular;
        }

        [Header("Faces")]
        [Tooltip("Aldrich SDF - the heading and body face (§4).")]
        [SerializeField] private TMP_FontAsset aldrich;
        [Tooltip("Chakra Petch SemiBold SDF - the button and label face (§4).")]
        [SerializeField] private TMP_FontAsset chakraPetchSemiBold;

        [Header("Roles (§4 PC @1920)")]
        [SerializeField] private RoleSpec[] roles = Defaults();

        public TMP_FontAsset FontFor(UITypeFace face) =>
            face == UITypeFace.ChakraPetchSemiBold ? chakraPetchSemiBold : aldrich;

        /// <summary>The row for <paramref name="role"/>; the §4 default when the asset lacks one.</summary>
        public RoleSpec Spec(UITypeRole role)
        {
            if (roles != null)
                foreach (var r in roles)
                    if (r.role == role) return r;
            return DefaultSpec(role);
        }

        /// <summary>The §4 row for <paramref name="role"/>, used when no asset is wired.</summary>
        public static RoleSpec DefaultSpec(UITypeRole role)
        {
            foreach (var r in Defaults())
                if (r.role == role) return r;
            return Defaults()[(int)UITypeRole.Body];
        }

        /// <summary>Docs/STYLE_FOUNDATION.md §4, PC column, verbatim; Label is the style-B addition.</summary>
        public static RoleSpec[] Defaults() => new[]
        {
            Row(UITypeRole.Display, UITypeFace.Aldrich, 48f, 0f, false, false),
            Row(UITypeRole.H1, UITypeFace.Aldrich, 36f, 0f, false, false),
            Row(UITypeRole.H2, UITypeFace.Aldrich, 28f, 0f, false, false),
            Row(UITypeRole.H3, UITypeFace.Aldrich, 22f, 0f, false, false),
            Row(UITypeRole.Body, UITypeFace.Aldrich, 18f, 0f, false, false),
            Row(UITypeRole.BodySmall, UITypeFace.Aldrich, 15f, 0f, false, false),
            Row(UITypeRole.Button, UITypeFace.ChakraPetchSemiBold, 18f, 8f, true, true),
            Row(UITypeRole.ButtonSmall, UITypeFace.ChakraPetchSemiBold, 14f, 8f, true, true),
            Row(UITypeRole.Label, UITypeFace.ChakraPetchSemiBold, 14f, 14f, true, true),
            Row(UITypeRole.DataLarge, UITypeFace.Aldrich, 44f, 0f, false, true),
            Row(UITypeRole.Data, UITypeFace.Aldrich, 20f, 0f, false, true),
            Row(UITypeRole.DataSmall, UITypeFace.Aldrich, 15f, 0f, false, true),
        };

        private static RoleSpec Row(UITypeRole role, UITypeFace face, float size, float spacing,
            bool upper, bool tabular) =>
            new() { role = role, face = face, size = size, characterSpacing = spacing, upperCase = upper, tabular = tabular };
    }
}
