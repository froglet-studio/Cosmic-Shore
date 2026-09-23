using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The in-game credits screen. One modal, driven entirely by
    /// <see cref="CreditsManifestSO"/> at <c>Resources/CreditsManifest</c>.
    ///
    /// <para><b>Why this exists at all:</b> FMOD's EULA (<c>Assets/Plugins/FMOD/LICENSE.txt</c>,
    /// clause 3) requires an in-game credit containing the words <c>FMOD</c> and
    /// <c>Firelight Technologies Pty Ltd.</c> on every licence tier. The same screen discharges
    /// every other attribution the tree owes — the MIT notices, the SIL OFL and Apache font
    /// notices, EmojiOne, the Modified BSD notice for OpenImageIO. See
    /// <c>Docs/THIRD_PARTY_REGISTER.md</c> §7.</para>
    ///
    /// <para><b>The rows are BUILT, not authored.</b> The manifest is the single source of the
    /// text, so authoring one label per notice in a prefab would be a second copy that drifts —
    /// and adding the next notice is supposed to be an asset edit, not a prefab edit. A human
    /// wires a ScrollRect and (optionally) two style templates; everything inside the content is
    /// generated here.</para>
    ///
    /// <para><b>The ScrollRect trap this is written against</b> (CLAUDE.md, the arcade grid): a
    /// ScrollRect's Content is a SCROLL EXTENT, not a layout frame. A section below the reachable
    /// range is clipped by the viewport Mask <i>and</i> eats the press. So the content height is
    /// never authored — a <see cref="ContentSizeFitter"/> derives it from a
    /// <see cref="VerticalLayoutGroup"/> after the rows exist, and
    /// <see cref="LayoutRebuilder.ForceRebuildLayoutImmediate"/> settles it in the same frame the
    /// rows are created rather than one frame later.</para>
    /// </summary>
    public class CreditsModal : ModalWindowManager
    {
        [Header("Credits content")]
        [SerializeField, Tooltip("The ScrollRect's Content RectTransform. Rows are generated as " +
                                 "its children; its height is MEASURED after they exist, never authored. " +
                                 "Leave empty to find the ScrollRect in this modal's own children.")]
        RectTransform content;

        [SerializeField, Tooltip("Optional. A TMP_Text whose font/size/colour a HEADING row copies. " +
                                 "Leave empty to use the first text found under this modal as the style.")]
        TMP_Text headingTemplate;

        [SerializeField, Tooltip("Optional. A TMP_Text whose font/size/colour a BODY row copies.")]
        TMP_Text bodyTemplate;

        [SerializeField, Tooltip("Vertical gap between generated rows, in canvas units.")]
        float rowSpacing = 12f;

        [SerializeField, Tooltip("Padding inside the content column, in canvas units.")]
        RectOffset contentPadding;

        bool _built;

        protected override void Start()
        {
            base.Start();
            Build();
        }

        /// <summary>
        /// Populates the scroll content from the manifest. Idempotent — the manifest is static
        /// data, so this runs once on first open rather than on every open.
        /// </summary>
        public void Build()
        {
            if (_built) return;
            _built = true;

            var manifest = CreditsManifestSO.Load();
            if (manifest == null)
            {
                // Loud, because an empty credits screen is a licence breach wearing the costume of
                // a cosmetic bug. The release-build guard refuses to ship this state at all.
                CSDebug.LogErrorFormat(
                    "{0} - no credits manifest at Resources/{1}. The screen will be empty, which " +
                    "breaches FMOD's EULA clause 3 (an in-game credit naming FMOD and Firelight " +
                    "Technologies Pty Ltd. is required on every tier). Restore " +
                    "Assets/Resources/CreditsManifest.asset.",
                    nameof(CreditsModal), CreditsManifestSO.ResourcePath);
                return;
            }

            var column = ResolveContent();
            if (column == null)
            {
                CSDebug.LogErrorFormat(
                    "{0} - no ScrollRect content found on '{1}'. Wire the ScrollRect's Content " +
                    "RectTransform to this component's 'content' field; nothing can be shown " +
                    "without it.", nameof(CreditsModal), name);
                return;
            }

            var heading = headingTemplate ? headingTemplate : FindStyleSource();
            var body = bodyTemplate ? bodyTemplate : heading;

            PrepareColumn(column);

            AddRow(column, manifest.Title, heading, "CreditsTitle");

            foreach (var section in manifest.Sections)
            {
                if (section == null) continue;

                if (!string.IsNullOrWhiteSpace(section.Heading))
                    AddRow(column, section.Heading, heading, "Heading");

                if (!string.IsNullOrWhiteSpace(section.Blurb))
                    AddRow(column, section.Blurb, body, "Blurb");

                if (section.Entries == null) continue;

                foreach (var entry in section.Entries)
                {
                    if (entry == null) continue;

                    // Name and role read as one credit, so they are one row: a person's name and
                    // what they did should not be separable by a scroll position.
                    string headline = string.IsNullOrWhiteSpace(entry.Role)
                        ? entry.Name
                        : $"{entry.Name}\n{entry.Role}";
                    if (!string.IsNullOrWhiteSpace(headline))
                        AddRow(column, headline, body, "Entry");

                    if (!string.IsNullOrWhiteSpace(entry.Notice))
                        AddRow(column, entry.Notice, body, "Notice");
                }
            }

            // MEASURE, never author. The fitter needs the rows to exist first, and forcing the
            // rebuild now means the scroll extent is correct on the frame the modal opens rather
            // than on the one after it — which is what stops the last notice being unreachable.
            LayoutRebuilder.ForceRebuildLayoutImmediate(column);
        }

        /// <summary>The ScrollRect content, wired or found.</summary>
        RectTransform ResolveContent()
        {
            if (content) return content;
            var scroll = GetComponentInChildren<ScrollRect>(true);
            if (scroll && scroll.content) content = scroll.content;
            return content;
        }

        /// <summary>
        /// Any TMP_Text already on this modal, used as the style to clone. Falls back to nothing,
        /// in which case generated rows use TMP's defaults — legible, unstyled, and obvious enough
        /// that it reads as unfinished rather than as missing.
        /// </summary>
        TMP_Text FindStyleSource() => GetComponentInChildren<TMP_Text>(true);

        /// <summary>
        /// Ensures the content column lays its children out vertically and sizes itself to them.
        /// Ensured rather than required so a modal authored before this component, or built by a
        /// tool, still scrolls correctly.
        /// </summary>
        void PrepareColumn(RectTransform column)
        {
            // Clear anything previously generated. Rows are ours; a wired template that happens to
            // live under the content is not, so only generated rows are removed.
            for (int i = column.childCount - 1; i >= 0; i--)
            {
                var child = column.GetChild(i);
                if (child.GetComponent<GeneratedCreditsRow>())
                    Destroy(child.gameObject);
            }

            var layout = column.GetComponent<VerticalLayoutGroup>();
            if (!layout) layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = rowSpacing;
            layout.padding = contentPadding ?? new RectOffset(24, 24, 24, 24);
            layout.childControlWidth = true;
            layout.childControlHeight = true;   // rows are multi-line; their height is their text
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = column.GetComponent<ContentSizeFitter>();
            if (!fitter) fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // A top-anchored, top-pivoted column is what makes a growing height extend DOWNWARD.
            // Anchored any other way, adding rows moves the first one off the top of the viewport.
            column.anchorMin = new Vector2(0f, 1f);
            column.anchorMax = new Vector2(1f, 1f);
            column.pivot = new Vector2(0.5f, 1f);
        }

        void AddRow(RectTransform column, string text, TMP_Text style, string rowName)
        {
            var go = new GameObject($"Credits_{rowName}", typeof(RectTransform));
            go.transform.SetParent(column, false);
            go.AddComponent<GeneratedCreditsRow>();

            var label = go.AddComponent<TextMeshProUGUI>();
            if (style)
            {
                label.font = style.font;
                label.fontSize = style.fontSize;
                label.fontStyle = style.fontStyle;
                label.color = style.color;
                label.alignment = style.alignment;
            }
            label.richText = false;   // notices are verbatim legal text - never parse markup in it
            label.text = text;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }

        #region Structural construction

        /// <summary>
        /// Builds a complete, working credits window under <paramref name="canvasRoot"/>.
        ///
        /// <para><b>Why this is BUILT and not a prefab.</b> Every other law in this project that
        /// must not be authorable-away is ensured in code rather than wired per-scene — the ability
        /// lockup (<c>VesselHUDController.Initialize</c>), the connecting panel's pilot roster, the
        /// prism occlusion corridor. A licence obligation is squarely in that class: FMOD's EULA
        /// clause 3 is not satisfied by a modal that exists, it is satisfied by one a player can
        /// open, and a scene-authored window can be deleted from <c>Menu_Main</c> with nothing in
        /// the project noticing. The build guard checks the DATA; this is what keeps the DOOR.</para>
        ///
        /// <para>It stands down the moment a human authors a real one:
        /// <see cref="ScreenSwitcher"/> only calls this when no <c>CREDITS</c> modal is registered,
        /// so replacing this with proper chrome is an ordinary scene edit and costs no code
        /// change.</para>
        ///
        /// <para>The chrome is deliberately plain — no art is invented. It is a dimmed plate, a
        /// scroll view and a close button, in TMP's default font.</para>
        /// </summary>
        public static CreditsModal Build(RectTransform canvasRoot)
        {
            if (canvasRoot == null) return null;

            var modalGo = new GameObject("CreditsModal (built)", typeof(RectTransform), typeof(CanvasGroup));
            var modalRect = (RectTransform)modalGo.transform;
            modalRect.SetParent(canvasRoot, false);
            Stretch(modalRect);
            modalRect.SetAsLastSibling();   // modals draw over the screens

            // Hidden from the frame it exists. A CanvasGroup defaults to alpha 1, and the base
            // class only hides itself in Start - which is a frame away - so without this the menu
            // opens with the credits window flashed over it once on every scene load.
            var group = modalGo.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var modal = modalGo.AddComponent<CreditsModal>();
            modal.ModalType = ScreenSwitcher.ModalWindows.CREDITS;

            // The window plate. Anchored as FRACTIONS of the canvas rather than in pixels, so it
            // is correct at any reference resolution and on any aspect ratio - this modal has no
            // authored rect to inherit one from.
            var window = NewRect("Window", modalRect);
            window.anchorMin = new Vector2(0.12f, 0.08f);
            window.anchorMax = new Vector2(0.88f, 0.92f);
            window.offsetMin = Vector2.zero;
            window.offsetMax = Vector2.zero;
            var plate = window.gameObject.AddComponent<Image>();
            plate.color = new Color(0.04f, 0.05f, 0.09f, 0.96f);
            plate.raycastTarget = true;   // the plate eats clicks meant for the screens behind it

            // Standard Unity scroll view: ScrollRect on the outer object, a masked viewport, and a
            // content column whose HEIGHT IS MEASURED (see Build()) rather than authored.
            var scrollRoot = NewRect("Scroll View", window);
            Stretch(scrollRoot, left: 24f, right: 24f, top: 88f, bottom: 24f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();

            var viewport = NewRect("Viewport", scrollRoot);
            Stretch(viewport);
            viewport.pivot = new Vector2(0f, 1f);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            // Invisible, but raycastable: a ScrollRect drags only where a Graphic accepts the
            // pointer, so an alpha-0 image here is what makes the list scrollable by touch/mouse.
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var column = NewRect("Content", viewport);
            column.anchorMin = new Vector2(0f, 1f);
            column.anchorMax = new Vector2(1f, 1f);
            column.pivot = new Vector2(0.5f, 1f);
            column.offsetMin = new Vector2(0f, 0f);
            column.offsetMax = new Vector2(0f, 0f);

            scroll.viewport = viewport;
            scroll.content = column;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.scrollSensitivity = 30f;

            modal.content = column;

            BuildCloseButton(modal, window);

            return modal;
        }

        static void BuildCloseButton(CreditsModal modal, RectTransform window)
        {
            var closeRect = NewRect("Close", window);
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-24f, -20f);
            closeRect.sizeDelta = new Vector2(120f, 52f);

            var image = closeRect.gameObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.18f, 0.26f, 1f);

            var button = closeRect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            // ModalWindowOut, never a direct SetActive: the window has to leave the switcher's
            // modal stack, or the next gamepad B unwinds a stack entry that is no longer on screen.
            button.onClick.AddListener(modal.ModalWindowOut);

            var labelRect = NewRect("Label", closeRect);
            Stretch(labelRect);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "CLOSE";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 24f;
            label.raycastTarget = false;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect, float left = 0f, float right = 0f,
                            float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        #endregion

        /// <summary>Marks a row this component generated, so a rebuild removes only its own.</summary>
        sealed class GeneratedCreditsRow : MonoBehaviour { }
    }
}
