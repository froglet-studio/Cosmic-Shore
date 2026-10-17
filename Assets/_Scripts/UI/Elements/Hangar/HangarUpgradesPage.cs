using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's UPGRADES tab (rev 3): the vessel's four elements, each with its level bar, the ability
    /// it scales and its level-5 upgrade - and the 3D hull on the stage morphing as you try a level.
    ///
    /// <para>It opens in the DEFAULT form: nothing selected, every element at 0, the hull un-morphed
    /// ("when you first switch there is no elemental value or upgrades selected and it shows default
    /// vessel form"). Pressing an element's row steps that element's PREVIEW level through
    /// <see cref="previewSteps"/> (0 -> 3 -> 5 -> 10 -> 0 by default) and morphs the hull through
    /// <see cref="HangarVesselStage.SetElementLevel"/>; the other elements drop back to 0 so the change on
    /// the model is always one element's. A preview is a look, not a purchase: there is no saved
    /// per-vessel element level in the game today.</para>
    /// </summary>
    public class HangarUpgradesPage : MonoBehaviour, IHangarTabPage
    {
        [System.Serializable]
        public class Row
        {
            public Element element = Element.Charge;
            public KitButton button;
            public TMP_Text titleText;
            public TMP_Text lineText;
            public TMP_Text upgradeText;
            public TMP_Text levelText;
            [Tooltip("The ten level pips, lowest first.")]
            public Image[] pips = new Image[10];
            public Image elementGlyph;
            public Image glow;
            [Tooltip("The breathing element glow behind the glyph.")]
            public KitElementHalo halo;
            [HideInInspector] public int level;
        }

        [Header("Rows (Charge, Mass, Space, Time)")]
        [SerializeField] private Row[] rows = new Row[4];

        [Header("Header")]
        [SerializeField] private TMP_Text headerLineText;

        [SerializeField] private AbilityLockupStyleSO lockupStyle;

        [Header("Preview")]
        [Tooltip("Levels a press steps through, in order; the last wraps back to the first.")]
        [SerializeField] private int[] previewSteps = { 0, 3, 5, 10 };
        [Tooltip("Pip colour while off.")]
        [SerializeField] private Color pipOff = new(0.13f, 0.15f, 0.27f, 1f);

        [Header("Words")]
        [SerializeField] private string[] elementNames = { "Charge", "Mass", "Space", "Time" };
        [Tooltip("Header line in the default form.")]
        [SerializeField] private string defaultLine = "Default form - press an element to see the hull change.";
        [Tooltip("Header line while previewing; {element} and {level} are filled in.")]
        [SerializeField] private string previewTemplate = "{element} {level} - the hull as it would fly.";
        [Tooltip("Header line for a vessel with no element shapes to preview.")]
        [SerializeField] private string noMorphLine = "This hull keeps its shape at every level.";
        [Tooltip("Row title; {element} and {ability} are filled in.")]
        [SerializeField] private string titleTemplate = "{element} - {ability}";
        [Tooltip("Row line under the title; {level} is filled in.")]
        [SerializeField] private string levelTemplate = "Level {level} of 10";
        [Tooltip("The level-5 upgrade; {upgrade} is filled in.")]
        [SerializeField] private string upgradeTemplate = "L5 {upgrade}";
        [Tooltip("Title of a slot with no ability yet.")]
        [SerializeField] private string openSlotTitle = "Open slot";

        HangarVesselStage _stage;

        void Awake()
        {
            if (!lockupStyle) lockupStyle = Resources.Load<AbilityLockupStyleSO>("AbilityLockupStyle");
            foreach (var row in rows)
            {
                if (row?.button == null) continue;
                var r = row;
                r.button.onClick.AddListener(() => Step(r));
            }
        }

        public void SetVessel(SO_Vessel vessel, HangarVesselStage stage)
        {
            _stage = stage;
            var map = vessel ? ElementalAbilityMapSO.LoadFor(vessel.Class) : null;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                var entry = map ? map.GetEntry(row.element) : null;
                string el = i < elementNames.Length ? elementNames[i] : row.element.ToString();
                bool open = entry == null || string.IsNullOrEmpty(entry.AbilityLabel);
                if (row.titleText)
                    row.titleText.text = Fill(titleTemplate, ("element", el), ("ability", open ? openSlotTitle : entry.AbilityLabel));
                if (row.upgradeText)
                    row.upgradeText.text = open || string.IsNullOrEmpty(entry.UpgradeLabel) ? string.Empty
                        : Fill(upgradeTemplate, ("upgrade", entry.UpgradeLabel));
                if (row.elementGlyph) row.elementGlyph.color = Colour(row.element);
                if (row.halo) row.halo.SetColour(Colour(row.element));
            }
            ResetPreview();
        }

        /// <summary>Every visit opens in the default form.</summary>
        public void SetActivePage(bool active)
        {
            if (active) ResetPreview();
        }

        void ResetPreview()
        {
            foreach (var row in rows)
                if (row != null) row.level = 0;
            if (_stage) _stage.ResetMorphs();
            Redraw(null);
        }

        void Step(Row picked)
        {
            int next = previewSteps.Length > 0 ? previewSteps[0] : 0;
            for (int i = 0; i < previewSteps.Length; i++)
                if (previewSteps[i] == picked.level) { next = previewSteps[(i + 1) % previewSteps.Length]; break; }
            foreach (var row in rows)
                if (row != null) row.level = row == picked ? next : 0;
            if (_stage)
            {
                _stage.ResetMorphs();
                if (next > 0) _stage.SetElementLevel(picked.element, next);
            }
            Redraw(next > 0 ? picked : null);
        }

        void Redraw(Row previewing)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null) continue;
                Color c = Colour(row.element);
                for (int p = 0; p < row.pips.Length; p++)
                    if (row.pips[p]) row.pips[p].color = p < row.level ? c : pipOff;
                if (row.levelText) row.levelText.text = row.level.ToString();
                if (row.lineText) row.lineText.text = Fill(levelTemplate, ("level", row.level.ToString()));
                if (row.glow) row.glow.color = new Color(c.r, c.g, c.b, row == previewing ? 0.9f : 0f);
            }
            if (!headerLineText) return;
            if (_stage && !_stage.HasMorphs) headerLineText.text = noMorphLine;
            else if (previewing == null) headerLineText.text = defaultLine;
            else
            {
                int i = System.Array.IndexOf(rows, previewing);
                string el = i >= 0 && i < elementNames.Length ? elementNames[i] : previewing.element.ToString();
                headerLineText.text = Fill(previewTemplate, ("element", el), ("level", previewing.level.ToString()));
            }
        }

        Color Colour(Element e) => lockupStyle ? lockupStyle.ColourFor(e) : Color.white;

        static string Fill(string template, params (string key, string value)[] tokens)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            foreach (var (k, v) in tokens) template = template.Replace("{" + k + "}", v);
            return template;
        }
    }
}
