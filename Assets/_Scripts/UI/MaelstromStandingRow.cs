using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One domain's line in the Maelstrom race (Docs/MENU_REDESIGN_B.md section 27): a domain pill
    /// (tinted plate, hex mark, name), an optional bar that fills toward the win target, and the
    /// points. The hub's "Race to N" strip uses the bar; the summary's domain ranking uses the same
    /// part without one. Shown and hidden through its CanvasGroup, so a two-domain tournament simply
    /// closes the third row.
    /// </summary>
    public class MaelstromStandingRow : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;

        [Header("Pill")]
        [SerializeField] Image pillBackground;
        [SerializeField] Image pillMark;
        [SerializeField] TMP_Text domainText;

        [Header("Bar (optional)")]
        [Tooltip("The lit part of the bar, anchored along its track by the points / target ratio.")]
        [SerializeField] RectTransform fill;
        [SerializeField] Image fillImage;

        [Header("Points")]
        [SerializeField] TMP_Text valueText;
        [Tooltip("Alpha of the pill's plate behind the domain colour.")]
        [SerializeField, Range(0f, 1f)] float pillAlpha = 0.14f;
        [Tooltip("The points against a target. Tokens: {points} {target}.")]
        [SerializeField] string targetTemplate = "{points} / {target}";

        /// <summary>Draws a domain's standing. <paramref name="target"/> 0 shows the points alone.</summary>
        public void Show(Domains domain, Color color, int points, int target)
        {
            KitVisibility.SetShown(group, true);
            if (domainText)
            {
                domainText.text = domain.ToString().ToUpperInvariant();
                domainText.color = color;
            }
            if (pillMark) pillMark.color = color;
            if (pillBackground) pillBackground.color = new Color(color.r, color.g, color.b, pillAlpha);
            if (fillImage) fillImage.color = color;
            if (fill)
            {
                float ratio = target > 0 ? Mathf.Clamp01((float)points / target) : 0f;
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(ratio, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }
            if (valueText)
                valueText.text = target > 0
                    ? targetTemplate.Replace("{points}", points.ToString()).Replace("{target}", target.ToString())
                    : points.ToString();
        }

        public void Hide() => KitVisibility.SetShown(group, false);
    }
}
