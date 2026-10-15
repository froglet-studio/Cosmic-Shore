using DG.Tweening;
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
        [Tooltip("The win condition's bar, until the HUD names the local pilot's domain " +
                 "(SetAccent) - then it wears the team colour, as every other 'this is yours' " +
                 "mark on the HUD does (Docs/MENU_REDESIGN_B.md section 22).")]
        [SerializeField] UIColorToken primaryFillToken = UIColorToken.ControlActive;

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

        [Tooltip("The '/target' half. Dim enough that the current count reads first, close " +
                 "enough that the pair reads as one number (style B: Neutral Lightest).")]
        [SerializeField] UIColorToken targetToken = UIColorToken.NeutralLightest;

        [Tooltip("The bloom's colour. Its ALPHA is overwritten per rank - author the hue here.")]
        [SerializeField] Color glowColor = new Color(0.96f, 0.96f, 1f, 1f);

        [Tooltip("Alpha the bloom snaps to when this objective advances, before easing back to " +
                 "its rank's rest alpha.")]
        [SerializeField, Range(0f, 1f)] float glowPunchAlpha = 1f;

        [Tooltip("Seconds for the punch to ease back to rest.")]
        [SerializeField, Min(0.05f)] float glowPunchSeconds = 0.45f;

        [Header("Motion (style B, Docs/MENU_REDESIGN_B.md section 22.7)")]
        [Tooltip("Seconds the progress bar takes to travel to a new value. It used to jump; a bar " +
                 "that jumps reads as a redraw, a bar that travels reads as progress.")]
        [SerializeField, Min(0f)] float fillSeconds = 0.32f;
        [Tooltip("How far the count swells when the objective advances, then settles (1 = no punch).")]
        [SerializeField, Min(1f)] float countPunchScale = 1.14f;

        [Tooltip("The slider bed. Dim enough to read as unfilled, present enough to read as a " +
                 "bar that has somewhere to go.")]
        [SerializeField] Color trackColor = new Color(0.902f, 0.914f, 1f, 0.16f);

        // The last count this row DREW, and whether it has drawn one at all. The stack rebuilds
        // this row on every monitor tick with the same value, so an increase - not a write - is
        // what a score is. -1 means "nothing drawn yet": arriving at a value (a scene re-entry, or
        // the target resolving late over the network) is not something the player just did.
        int _lastCount = -1;
        Tween _glowPunch;
        Tween _fillTween;
        Tween _countPunch;
        float _glowRest;
        Color? _accent;

        /// <summary>
        /// The local pilot's domain colour: the primary row's progress bar wears it. Null goes
        /// back to <see cref="primaryFillToken"/> (no domain known - the menu's freestyle HUD).
        /// </summary>
        public void SetAccent(Color? accent)
        {
            _accent = accent;
            if (progressFill && progressFill.enabled && _lastRank == GoalRank.Primary)
                progressFill.color = PrimaryFill;
        }

        GoalRank _lastRank = GoalRank.Primary;

        Color PrimaryFill => _accent ?? UITheme.Resolve(null, primaryFillToken);

        /// <summary>
        /// Show a counted objective - a current value against a target, with the progress
        /// hairline filled to match.
        /// </summary>
        public void ShowCount(Sprite glyph, string title, int current, int target, GoalRank rank)
        {
            Apply(glyph, title, rank);

            int shown = Mathf.Clamp(current, 0, Mathf.Max(current, target));
            if (_lastCount >= 0 && shown > _lastCount)
            {
                PunchGlow();
                PunchCount();
            }
            _lastCount = shown;
            if (value)
            {
                // Tabular digits (STYLE_FOUNDATION section 4) so the count never reflows as a 1
                // becomes an 8. Each number is wrapped on its own: the colour tag's hex must not be.
                var face = value.font;
                value.text = target > 0
                    ? $"{TabularText.Wrap(shown.ToString(), face)}<color=#{ColorUtility.ToHtmlStringRGB(UITheme.Resolve(null, targetToken))}>/{TabularText.Wrap(target.ToString(), face)}</color>"
                    : TabularText.Wrap(shown.ToString(), face);
            }

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
            if (value) value.text = TabularText.Wrap(text, value.font);
            SetFill(0f, rank, false);
            _lastCount = -1;          // a clock has no count; don't punch off a stale one later
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _lastCount = -1;          // a new turn starts from nothing, never from the old total
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Apply(Sprite glyph, string title, GoalRank rank)
        {
            bool primary = rank == GoalRank.Primary;
            _lastRank = rank;

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
                _glowRest = primary ? primaryGlowAlpha : secondaryGlowAlpha;
                // Rank sets the HUE and the rest alpha - but the row is re-applied on every
                // monitor tick, so writing the alpha unconditionally would stomp a punch a tick
                // after it started and the pulse would never be seen.
                var g = glowColor;
                g.a = _glowPunch != null && _glowPunch.IsActive() && _glowPunch.IsPlaying()
                    ? glow.color.a
                    : _glowRest;
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

        /// <summary>
        /// Flare the bloom when the objective advances, then ease it back to its rank's rest
        /// alpha. Killed and re-fired rather than stacked, so a burst of scores reads as a burst
        /// of pulses instead of leaving the plate stuck lit - the same failure
        /// <see cref="DomainScorePanel"/> records for the score columns' glow.
        ///
        /// Unscaled time deliberately: a score can land on the frame a mode freezes the clock.
        /// </summary>
        void PunchGlow()
        {
            if (!glow) return;

            _glowPunch?.Kill();

            var c = glowColor;
            c.a = Mathf.Clamp01(Mathf.Max(glowPunchAlpha, _glowRest));
            glow.color = c;

            _glowPunch = glow
                .DOFade(Mathf.Clamp01(_glowRest), glowPunchSeconds)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .SetUpdate(true);
        }

        void PunchCount()
        {
            if (!value || countPunchScale <= 1f) return;
            _countPunch?.Kill(true);
            var t = value.rectTransform;
            t.localScale = Vector3.one;
            _countPunch = t.DOPunchScale(Vector3.one * (countPunchScale - 1f), glowPunchSeconds, 1, 0f)
                .SetLink(gameObject).SetUpdate(true);
        }

        void OnDisable()
        {
            _fillTween?.Kill();
            _countPunch?.Kill();
            if (value) value.rectTransform.localScale = Vector3.one;
            // Pooled/toggled HUD: settle at rest rather than leaving the plate mid-pulse.
            _glowPunch?.Kill();
            _glowPunch = null;
            if (glow)
            {
                var c = glowColor;
                c.a = _glowRest;
                glow.color = c;
            }
        }

        void OnDestroy()
        {
            _glowPunch?.Kill();
            _fillTween?.Kill();
            _countPunch?.Kill();
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
            progressFill.color = rank == GoalRank.Primary ? PrimaryFill : secondaryFillColor;
            // The stack re-applies every row on each monitor tick with the same value, so only a
            // CHANGE travels; a fresh row (or a disabled one) lands where it is told.
            if (Mathf.Approximately(progressFill.fillAmount, amount01)) return;
            _fillTween?.Kill();
            if (fillSeconds <= 0f || !isActiveAndEnabled || _lastCount < 0)
            {
                progressFill.fillAmount = amount01;
                return;
            }
            _fillTween = progressFill.DOFillAmount(amount01, fillSeconds).SetEase(Ease.OutCubic)
                .SetLink(gameObject).SetUpdate(true);
        }
    }
}
