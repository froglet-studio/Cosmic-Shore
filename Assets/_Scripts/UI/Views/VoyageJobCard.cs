using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One job on the Voyage window (<see cref="VoyageJobsView"/> stands one per job on offer, in a grid like the arcade
    /// games' tiles). Closed, it is the course and the pilot who brought it, the job's title and its course line; OPEN
    /// expands it over the other cards (<see cref="Overlay"/>) to the brief, where the player confirms the squad, the
    /// mission and its intensity and sets sail (Garrett, 2026-11-01: "clicking on a voyage expands it over the others";
    /// "on the voyage card players confirm their squad mission difficulty etc."). The one galaxy map stands above the
    /// cards (<see cref="VoyageMapGraphic"/>), so the card carries no art. Every word is the voyage content's
    /// (<c>VoyageContentSO</c>), never this class's.
    /// </summary>
    public class VoyageJobCard : MonoBehaviour
    {
        [Header("The card")]
        [Tooltip("The card's art. Hidden: the window's one galaxy map stands beside the cards.")]
        [SerializeField] Image art;
        [SerializeField] TMP_Text eyebrowText;
        [SerializeField] TMP_Text titleText;
        [Tooltip("Set sail on this card's job. Shown while the card is open.")]
        [SerializeField] Button setSailButton;

        // The card's parts by their prefab names (MissionView.prefab's Voyage3, a copy of the open voyage card).
        const string OpenChipName = "Open";
        const string OpenFillName = "Open/Fill";
        const string OpenLabelName = "Open/Label";
        const string ClipName = "Clip";
        const string FadeName = "Clip/Fade";
        const string DetailsName = "Details";
        const string CourseName = "Course";

        // Heights and rows, from the card's foot (its children anchor to the bottom edge).
        /// <summary>A closed card's height: the grid's cell.</summary>
        public const float ClosedHeight = 176f;
        /// <summary>An open card's height, standing over the grid.</summary>
        public const float OpenHeight = 560f;
        const float Inset = 24f;
        const float SailFoot = 22f;
        const float TitleHalf = 31f;
        const float LineHeight = 24f;

        Button _toggle, _clipToggle;
        TMP_Text _openLabel, _details, _course;
        LayoutElement _layout;
        Action _onSetSail, _onToggle;
        bool _built;

        void Awake()
        {
            Build();
            if (setSailButton) setSailButton.onClick.AddListener(OnSetSail);
        }

        void OnDestroy()
        {
            if (setSailButton) setSailButton.onClick.RemoveListener(OnSetSail);
            if (_toggle) _toggle.onClick.RemoveListener(OnToggle);
            if (_clipToggle) _clipToggle.onClick.RemoveListener(OnToggle);
        }

        void OnSetSail() => _onSetSail?.Invoke();
        void OnToggle() => _onToggle?.Invoke();

        /// <summary>The text the window's map copies for its place names.</summary>
        public TMP_Text LabelTemplate => eyebrowText ? eyebrowText : titleText;

        /// <summary>
        /// Draw the card. <paramref name="onToggle"/> opens or closes it (null: no OPEN chip, the card stays as
        /// <paramref name="open"/> says). A null <paramref name="onSetSail"/> leaves Set sail dark. Empty texts show
        /// nothing.
        /// </summary>
        public void Show(string eyebrow, string title, string course, string details, bool open, Action onToggle,
            string openLabel, string closeLabel, Action onSetSail)
        {
            Build();
            if (eyebrowText) eyebrowText.text = eyebrow ?? string.Empty;
            if (titleText) titleText.text = title ?? string.Empty;
            _onSetSail = onSetSail;
            _onToggle = onToggle;
            if (setSailButton) setSailButton.interactable = onSetSail != null;

            var chip = transform.Find(OpenChipName);
            if (chip) chip.gameObject.SetActive(onToggle != null);
            if (_clipToggle) _clipToggle.enabled = onToggle != null;
            if (_openLabel) _openLabel.text = (open ? closeLabel : openLabel) ?? string.Empty;

            Text(_course, course, true);
            Text(_details, details, open);
            if (setSailButton) setSailButton.gameObject.SetActive(open);
            Lay(open);
        }

        static void Text(TMP_Text t, string value, bool show)
        {
            if (!t) return;
            bool any = show && !string.IsNullOrEmpty(value);
            t.gameObject.SetActive(any);
            if (any) t.text = value;
        }

        // ── Building the card's parts (once; a copy of the card brings them along) ───

        void Build()
        {
            if (_built) return;
            _built = true;
            TryGetComponent(out _layout);
            if (art) art.enabled = false;
            var fade = transform.Find(FadeName);
            if (fade) fade.gameObject.SetActive(false);

            // OPEN was a sign on the open voyage cards; here it is the control that drills in.
            var chip = transform.Find(OpenChipName);
            if (chip)
            {
                var fill = transform.Find(OpenFillName);
                Graphic target = fill && fill.TryGetComponent(out Graphic g) ? g : null;
                if (target) target.raycastTarget = true;
                if (!chip.TryGetComponent(out _toggle)) _toggle = chip.gameObject.AddComponent<Button>();
                _toggle.targetGraphic = target;
                _toggle.onClick.AddListener(OnToggle);
                var label = transform.Find(OpenLabelName);
                if (label) label.TryGetComponent(out _openLabel);
            }
            // The whole card opens it too: its dark ground is the press target (Set sail stands above it).
            var clip = transform.Find(ClipName);
            if (clip && clip.TryGetComponent(out Graphic ground))
            {
                ground.raycastTarget = true;
                if (!clip.TryGetComponent(out _clipToggle)) _clipToggle = clip.gameObject.AddComponent<Button>();
                _clipToggle.transition = Selectable.Transition.None;
                _clipToggle.targetGraphic = ground;
                _clipToggle.onClick.AddListener(OnToggle);
            }

            // Nothing else on the card catches a press meant for it (the title, the eyebrow, the border).
            foreach (var gfx in GetComponentsInChildren<Graphic>(true))
            {
                var at = gfx.transform;
                if (at == clip || (chip && at.IsChildOf(chip))) continue;
                if (setSailButton && at.IsChildOf(setSailButton.transform)) continue;
                gfx.raycastTarget = false;
            }

            _course = Line(CourseName, TextAlignmentOptions.BottomLeft, false);
            _details = Line(DetailsName, TextAlignmentOptions.TopLeft, true);
        }

        TMP_Text Line(string name, TextAlignmentOptions align, bool wrap)
        {
            var existing = transform.Find(name);
            if (existing && existing.TryGetComponent(out TMP_Text found)) return found;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            // Above the card's ground, under Set sail.
            if (setSailButton) go.transform.SetSiblingIndex(setSailButton.transform.GetSiblingIndex());
            var t = go.AddComponent<TextMeshProUGUI>();
            var from = eyebrowText ? eyebrowText : titleText;
            if (from)
            {
                t.font = from.font;
                t.fontSharedMaterial = from.fontSharedMaterial;
                t.fontSize = from.fontSize;
                t.color = from.color;
            }
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            return t;
        }

        /// <summary>
        /// Stand the card over the grid it sits in (the grid's whole width from its top, <see cref="OpenHeight"/> tall,
        /// drawn last so it covers the others), or back in its cell.
        /// </summary>
        public void Overlay(bool over)
        {
            Build();
            if (_layout) _layout.ignoreLayout = over;
            if (!over) return;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, OpenHeight);
            transform.SetAsLastSibling();
        }

        /// <summary>Stand the parts for the card closed (title and course line) or open (the brief and Set sail).</summary>
        void Lay(bool open)
        {
            float height = open ? OpenHeight : ClosedHeight;
            if (_layout) _layout.preferredHeight = height;
            else ((RectTransform)transform).sizeDelta = new Vector2(((RectTransform)transform).sizeDelta.x, height);

            float sailTop = SailFoot;
            if (open && setSailButton) sailTop += ((RectTransform)setSailButton.transform).rect.height + 16f;
            if (titleText)
            {
                var rt = titleText.rectTransform;
                float y = open ? height - 56f - TitleHalf : SailFoot + LineHeight + 12f + TitleHalf;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
            }
            float courseY = open ? height - 56f - 2f * TitleHalf - 8f - LineHeight : SailFoot;
            Place(_course, courseY, LineHeight);
            float detailsTop = courseY - 12f;
            Place(_details, sailTop, Mathf.Max(0f, detailsTop - sailTop));
        }

        static void Place(TMP_Text t, float y, float h)
        {
            if (!t) return;
            var rt = t.rectTransform;
            rt.sizeDelta = new Vector2(-2f * Inset, h);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        // ── The Ark escort's difficulty stepper ──────────────────────────────

        Button _lower, _raise;
        Vector2 _sailMin, _sailMax;
        bool _sailRead;

        const float StepWidth = 64f, StepGap = 8f;

        /// <summary>
        /// Two small buttons either side of Set sail that step a number down and up (the Ark escort's difficulty). They
        /// are copies of Set sail, so they look like the card; their labels are the caller's authored words. Set sail
        /// narrows between them while they show. Null actions hide them and give Set sail back its width.
        /// </summary>
        public void ShowStepper(string lowerLabel, string raiseLabel, Action lower, Action raise)
        {
            bool show = lower != null && raise != null && setSailButton;
            if (setSailButton && !_sailRead)
            {
                var sail = (RectTransform)setSailButton.transform;
                _sailMin = sail.offsetMin;
                _sailMax = sail.offsetMax;
                _sailRead = true;
            }
            if (show)
            {
                _lower = _lower ? _lower : Step(-1);
                _raise = _raise ? _raise : Step(1);
                Arm(_lower, lowerLabel, lower);
                Arm(_raise, raiseLabel, raise);
            }
            if (_lower) _lower.gameObject.SetActive(show);
            if (_raise) _raise.gameObject.SetActive(show);
            if (setSailButton)
            {
                // Set sail stretches across the card's foot; with the stepper up it keeps the middle and the steps
                // stand at its old ends, so none of the three covers another.
                float inset = show ? StepWidth + StepGap : 0f;
                var sail = (RectTransform)setSailButton.transform;
                sail.offsetMin = new Vector2(_sailMin.x + inset, _sailMin.y);
                sail.offsetMax = new Vector2(_sailMax.x - inset, _sailMax.y);
            }
        }

        /// <summary>A copy of Set sail, <see cref="StepWidth"/> wide, pinned to the left (down) or right (up) end of
        /// where Set sail stands at full width. Sized in units, not from Set sail's rect, which is not laid out yet when
        /// the window first draws.</summary>
        Button Step(int side)
        {
            var copy = Instantiate(setSailButton, setSailButton.transform.parent);
            copy.name = side < 0 ? "StepDown" : "StepUp";
            copy.onClick.RemoveAllListeners();
            var rt = (RectTransform)copy.transform;
            float x = side < 0 ? 0f : 1f;
            rt.anchorMin = new Vector2(x, rt.anchorMin.y);
            rt.anchorMax = new Vector2(x, rt.anchorMax.y);
            rt.pivot = new Vector2(x, rt.pivot.y);
            rt.sizeDelta = new Vector2(StepWidth, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(side < 0 ? _sailMin.x : _sailMax.x, rt.anchoredPosition.y);
            return copy;
        }

        static void Arm(Button button, string label, Action onClick)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
            button.interactable = true;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text) text.text = label ?? string.Empty;
        }
    }
}
