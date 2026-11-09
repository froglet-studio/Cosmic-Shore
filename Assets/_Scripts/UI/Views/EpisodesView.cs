using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A locked episode in the picked look (Rev 77 "Episodes", Docs/MENU_REDESIGN_B.md sections 150 and 157): one panel over
    /// the whole galaxy, muted (<see cref="VoyagePlaceArt.Galaxy"/>), with the lock, the episode's name, "Coming soon"
    /// and a line at its foot, and one button under it that goes back to another tab of the same window
    /// (<see cref="VoyageWindowTabs.Show"/>). The Voyage window's Episodes tab goes back to the Saga; the Quests window's
    /// Episode 2 back to Episode 1. Every word is a serialized line, authored by Tools/Build/author_voyage_window.py.
    /// </summary>
    public class EpisodesView : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The window's tabs; the button shows one of them.")]
        [SerializeField] VoyageWindowTabs tabs;
        [Tooltip("The tab the button goes back to (0 is the first).")]
        [SerializeField] int backTab = 1;
        [Tooltip("The lock drawn before the episode's name.")]
        [SerializeField] Sprite lockIcon;
        [Tooltip("The text whose font the words take (the window's title).")]
        [SerializeField] TMP_Text fontFrom;

        [Header("Words")]
        [Tooltip("The episode's name. Empty shows nothing.")]
        [SerializeField] string episodeTitle = "Episode 2";
        [Tooltip("Under the name.")]
        [SerializeField] string soonLine = "Coming soon";
        [Tooltip("The line at the panel's foot.")]
        [SerializeField] string bodyLine = "New episodes arrive here. Voyages and the Saga are open now.";
        [Tooltip("The button under the panel.")]
        [SerializeField] string backLabel = "Back to the Saga";

        [Header("Look")]
        [Tooltip("The panel's size, px (the board's 1000 x 600).")]
        [SerializeField] Vector2 panelSize = new(1000f, 600f);

        static readonly Color LockColor = new(0.902f, 0.914f, 1f, 1f);

        RawImage _galaxy;
        bool _built;

        void Awake() => Build();

        void OnEnable()
        {
            Build();
            // Painted on the first show, not at load: nobody may open this tab.
            if (!_galaxy.texture) _galaxy.texture = VoyagePlaceArt.Galaxy();
        }

        void Back()
        {
            if (tabs) tabs.Show(backTab);
        }

        void Build()
        {
            if (_built) return;
            _built = true;
            var root = (RectTransform)transform;

            // The panel, a little above the middle so the button sits under it.
            var panel = FlowUi.Node("Episode", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = panelSize;
            panel.anchoredPosition = new Vector2(0f, 44f);
            var plate = FlowUi.Fill(panel, FlowUi.Deep);
            panel.gameObject.AddComponent<RectMask2D>();
            FlowUi.Edge(plate, FlowUi.Line);
            var art = FlowUi.Node("Galaxy", panel);
            FlowUi.Anchor(art, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _galaxy = art.gameObject.AddComponent<RawImage>();
            _galaxy.raycastTarget = false;

            // The words at the foot: the lock and the name, "Coming soon", the line.
            var line = FlowUi.Text(panel, fontFrom, 18, FlowUi.Light, TextAlignmentOptions.BottomLeft);
            FlowUi.Anchor(line.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(40f, 36f), new Vector2(-40f, 62f));
            line.text = bodyLine ?? string.Empty;
            var soon = FlowUi.Text(panel, fontFrom, 22, FlowUi.Light, TextAlignmentOptions.BottomLeft);
            FlowUi.Anchor(soon.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(40f, 74f), new Vector2(-40f, 102f));
            soon.text = soonLine ?? string.Empty;
            float nameX = 40f;
            if (lockIcon)
            {
                var lk = FlowUi.Node("Lock", panel);
                lk.anchorMin = lk.anchorMax = Vector2.zero;
                lk.pivot = Vector2.zero;
                lk.sizeDelta = new Vector2(30f, 34f);
                lk.anchoredPosition = new Vector2(40f, 118f);
                var img = lk.gameObject.AddComponent<Image>();
                img.sprite = lockIcon;
                img.preserveAspect = true;
                img.color = LockColor;
                img.raycastTarget = false;
                nameX = 84f;
            }
            var title = FlowUi.Text(panel, fontFrom, 48, FlowUi.Light, TextAlignmentOptions.BottomLeft);
            FlowUi.Anchor(title.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(nameX, 112f), new Vector2(-40f, 168f));
            title.text = episodeTitle ?? string.Empty;

            // The way back: a quiet button (not the call to action), the board's 400 px.
            var back = FlowUi.Node("Back", root);
            back.anchorMin = back.anchorMax = new Vector2(0.5f, 0.5f);
            back.sizeDelta = new Vector2(400f, 56f);
            back.anchoredPosition = new Vector2(0f, 44f - panelSize.y * 0.5f - 52f);
            var fill = FlowUi.Fill(back, FlowUi.Plate);
            FlowUi.Edge(fill, FlowUi.Line);
            var b = back.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            b.onClick.AddListener(Back);
            var label = FlowUi.Text(back, fontFrom, 18, FlowUi.Light, TextAlignmentOptions.Center);
            FlowUi.Anchor(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.text = backLabel ?? string.Empty;
        }
    }
}
