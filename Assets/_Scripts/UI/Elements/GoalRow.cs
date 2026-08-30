using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Where a goal sits in the stack. Rank is expressed as POSITION plus a quieter style, never
    /// as a badge or a colour code - the top row is the one that ends the turn, and everything
    /// below it is context. That is why adding a fourth goal needs no re-layout: it appends.
    /// </summary>
    public enum GoalRank
    {
        Primary = 0,
        Secondary = 1,
    }

    /// <summary>
    /// One line of the top-left goal stack: the mode's objective glyph, what it is called, and
    /// how far along it is - "COLLECT CRYSTALS 18/30".
    ///
    /// It replaces the ring cluster the number used to sit inside. That cluster was chrome around
    /// a number that had nothing to do with time: every turn monitor raises
    /// <c>onUpdateTurnMonitorDisplay</c> with the metric REMAINING, so a timer face was drawn over
    /// an objective count, unlabelled and with no target. This row shows the same number with the
    /// two things the ring could not - what you are counting, and how many it takes.
    ///
    /// The row owns only its own presentation. It is told what to show; resolving the metric,
    /// the target and the count is <see cref="GoalStack"/>'s job.
    /// </summary>
    public class GoalRow : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The plate. A Graphic rather than an Image because it is GENERATED - a " +
                 "TrapezoidGraphic, the same house shape the ability lockup draws its cards with. " +
                 "A sprited plate freezes the slant into the art and is only crisp at the one size " +
                 "it was exported at; generated, it is exact at any resolution. Sized by the " +
                 "layout, never by this component.")]
        [SerializeField] Graphic plate;

        [Tooltip("Optional. The soft bloom behind the plate, drawn as an earlier sibling so it " +
                 "sits underneath. Bloom is bought with lit AREA, not intensity (Docs/PALETTE.md " +
                 "section 3: gameplay bloom clamps at 0.5), which is why this is a wide low-alpha " +
                 "falloff rather than a bright edge.")]
        [SerializeField] Image glow;

        [Tooltip("The objective glyph, from ObjectiveIconSetSO. Tinted here, so the art stays " +
                 "pure white.")]
        [SerializeField] Image icon;

        [Tooltip("What the objective is called - 'COLLECT CRYSTALS'. Uppercased here so the " +
                 "catalogue can author sentence case.")]
        [SerializeField] TMP_Text label;

        [Tooltip("The count. One text rather than two, so the pair reads as a single object and " +
                 "right-alignment holds the column as digits are added.")]
        [SerializeField] TMP_Text value;

        [Tooltip("Optional. The unfilled slider bed under the progress bar. Without it a run at " +
                 "0/30 draws nothing at all, so the bar reads as missing rather than as empty - " +
                 "and the first crystal makes a bar appear out of nowhere instead of moving one.")]
        [SerializeField] Image progressTrack;

        [Tooltip("Optional. The progress bar along the plate's bottom edge, filled left to right " +
                 "over the track. A row with no target (a clock) hides both.")]
        [SerializeField] Image progressFill;

        [Tooltip("Drives the whole row's alpha so a secondary goal reads as quieter without " +
                 "restating any colour.")]
        [SerializeField] CanvasGroup canvasGroup;

        [Tooltip("Sets the row's height per rank.")]
        [SerializeField] LayoutElement layoutElement;

        [Header("Style - primary")]
        [SerializeField] float primaryHeight = 48f;
        [SerializeField] float primaryLabelSize = 16f;
        [SerializeField] float primaryValueSize = 22f;
        [SerializeField] float primaryIconSize = 19f;
        [Tooltip("How lit the plate is. The win condition is the one row worth lighting.")]
        [SerializeField, Range(0f, 1f)] float primaryGlowAlpha = 0.3f;
        [Tooltip("The win condition wears the reward green.")]
        [SerializeField] Color primaryFillColor = new Color(0.224f, 0.843f, 0.627f, 1f);

        [Header("Style - secondary")]
        [SerializeField] float secondaryHeight = 37f;
        [SerializeField] float secondaryLabelSize = 13f;
        [SerializeField] float secondaryValueSize = 16f;
        [SerializeField] float secondaryIconSize = 14f;
        [SerializeField, Range(0f, 1f)] float secondaryGlowAlpha = 0.12f;
        [Tooltip("Cooler and thinner: information, not the finish line.")]
        [SerializeField] Color secondaryFillColor = new Color(0.247f, 0.498f, 0.847f, 1f);
        [SerializeField, Range(0f, 1f)] float secondaryAlpha = 0.6f;

        [Header("Style - shared")]
        [Tooltip("Glyph and label tint. Style Foundation section 2: Light E6E9FF for HUD chrome.")]
        [SerializeField] Color chromeTint = new Color(0.902f, 0.914f, 1f, 1f);

        [Tooltip("The '/target' half, as a hex string TMP rich text can take. Dim enough that " +
                 "the current count reads first, close enough that the pair reads as one number.")]
        [SerializeField] string targetHexColor = "FFFFFF5C";

        [Tooltip("The bloom's colour. Its ALPHA is overwritten per rank - author the hue here.")]
        [SerializeField] Color glowColor = new Color(0.96f, 0.96f, 1f, 1f);

        [Tooltip("The slider bed. Dim enough to read as unfilled, present enough to read as a " +
                 "bar that has somewhere to go.")]
        [SerializeField] Color trackColor = new Color(0.902f, 0.914f, 1f, 0.16f);

        /// <summary>
        /// Show a counted objective - a current value against a target, with the progress
        /// hairline filled to match.
        /// </summary>
        public void ShowCount(Sprite glyph, string title, int current, int target, GoalRank rank)
        {
            Apply(glyph, title, rank);

            int shown = Mathf.Clamp(current, 0, Mathf.Max(current, target));
            if (value)
                value.text = target > 0
                    ? $"{shown}<color=#{targetHexColor}>/{target}</color>"
                    : shown.ToString();

            SetFill(target > 0 ? Mathf.Clamp01((float)shown / target) : 0f, rank, target > 0);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Show an objective whose value is not a count - a clock, most of all. No target and no
        /// hairline, because there is no proportion to draw.
        /// </summary>
        public void ShowText(Sprite glyph, string title, string text, GoalRank rank)
        {
            Apply(glyph, title, rank);
            if (value) value.text = text;
            SetFill(0f, rank, false);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Apply(Sprite glyph, string title, GoalRank rank)
        {
            bool primary = rank == GoalRank.Primary;

            if (icon)
            {
                icon.sprite = glyph;
                icon.color = chromeTint;
                // An Image with no sprite still draws a white box, so absence has to switch it off.
                icon.enabled = glyph != null;
                var r = icon.rectTransform;
                float s = primary ? primaryIconSize : secondaryIconSize;
                r.sizeDelta = new Vector2(s, s);
            }

            if (label)
            {
                label.text = string.IsNullOrEmpty(title) ? string.Empty : title.ToUpperInvariant();
                label.color = chromeTint;
                label.fontSize = primary ? primaryLabelSize : secondaryLabelSize;
            }

            if (value) value.fontSize = primary ? primaryValueSize : secondaryValueSize;
            if (plate) plate.enabled = true;
            if (glow)
            {
                glow.enabled = true;
                var g = glowColor;
                g.a = primary ? primaryGlowAlpha : secondaryGlowAlpha;
                glow.color = g;
            }
            if (canvasGroup) canvasGroup.alpha = primary ? 1f : secondaryAlpha;
            if (layoutElement)
            {
                float h = primary ? primaryHeight : secondaryHeight;
                layoutElement.preferredHeight = h;
                layoutElement.minHeight = h;
            }
        }

        void SetFill(float amount01, GoalRank rank, bool visible)
        {
            if (progressTrack)
            {
                progressTrack.enabled = visible;
                progressTrack.color = trackColor;
            }

            if (!progressFill) return;
            progressFill.enabled = visible;
            progressFill.color = rank == GoalRank.Primary ? primaryFillColor : secondaryFillColor;
            progressFill.fillAmount = amount01;
        }
    }
}
