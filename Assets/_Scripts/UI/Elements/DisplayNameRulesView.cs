using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The live rule ticks under a pilot-name field (Docs/MENU_REDESIGN_B.md section 26): the four
    /// format rules <see cref="DisplayNameValidator.EvaluateRules"/> judges, each a mark that fills once
    /// met, captioned from <see cref="DisplayNameValidator.RuleCaptions"/> so the copy follows the
    /// validator's config, plus "n / max" and a field border that turns Danger only while the typed name
    /// would be refused. The profile window draws the same rules (ProfileIconSelectView); this is the
    /// sign-in scene's copy of that drawing, with no save path of its own - the scene's controller still
    /// submits and owns every message about the result.
    /// </summary>
    public class DisplayNameRulesView : MonoBehaviour
    {
        [Tooltip("The pilot-name field the rules judge.")]
        [SerializeField] private TMP_InputField input;
        [SerializeField] private UIThemeSO theme;

        [Header("Rules")]
        [Tooltip("The four rule captions, in DisplayNameValidator.RuleCaptions order.")]
        [SerializeField] private TMP_Text[] ruleLabels = { };
        [Tooltip("The four rule marks: a filled hex once met, a hollow one until then.")]
        [SerializeField] private Image[] ruleMarks = { };
        [SerializeField] private Sprite ruleMetSprite;
        [SerializeField] private Sprite ruleOpenSprite;

        [Header("Field")]
        [Tooltip("\"9 / 25\" in the field's corner. Optional.")]
        [SerializeField] private TMP_Text countText;
        [Tooltip("The field's border: Control Active, Danger while the typed name would be refused. Optional.")]
        [SerializeField] private Image fieldBorder;

        private void OnEnable()
        {
            var captions = DisplayNameValidator.RuleCaptions();
            for (int i = 0; i < ruleLabels.Length && i < captions.Length; i++)
                if (ruleLabels[i]) ruleLabels[i].text = captions[i];
            if (input)
            {
                input.characterLimit = DisplayNameValidator.Config.MaxLength;
                input.onValueChanged.AddListener(Draw);
            }
            Draw(input ? input.text : string.Empty);
        }

        private void OnDisable()
        {
            if (input) input.onValueChanged.RemoveListener(Draw);
        }

        private void Draw(string raw)
        {
            raw ??= string.Empty;
            var rules = DisplayNameValidator.EvaluateRules(raw);
            string sanitized = DisplayNameValidator.Sanitize(raw);
            var config = DisplayNameValidator.Config;

            for (int i = 0; i < DisplayNameRuleState.Count; i++)
            {
                bool met = rules[i];
                if (i < ruleMarks.Length && ruleMarks[i])
                {
                    var sprite = met ? ruleMetSprite : ruleOpenSprite;
                    if (sprite) ruleMarks[i].sprite = sprite;
                    ruleMarks[i].color = UITheme.Resolve(theme, met ? UIColorToken.Cta : UIColorToken.NeutralLightest);
                }
                if (i < ruleLabels.Length && ruleLabels[i])
                    ruleLabels[i].color = UITheme.Resolve(theme, met ? UIColorToken.TextLight : UIColorToken.NeutralLightest);
            }

            if (countText) countText.text = $"{sanitized.Length} / {config.MaxLength}";

            // Judged once the name is long enough to judge (or empty), as the profile window does: a
            // two-letter name in progress is shown by its length rule, not scolded by the border.
            bool judge = sanitized.Length >= config.MinLength;
            bool refused = judge && !DisplayNameValidator.Validate(raw).IsValid;
            if (fieldBorder)
                fieldBorder.color = UITheme.Resolve(theme, refused ? UIColorToken.Danger : UIColorToken.ControlActive);
        }
    }
}
