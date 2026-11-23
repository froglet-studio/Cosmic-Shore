using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The way between the home hub's windows (Rev 7, Docs/MENU_REDESIGN_B.md §47.2; the owner, 2026-10-17: "pressing
    /// lt rt or pressing a new left right button should slide between the menus"): a strip under the top nav naming the
    /// four windows with the shown one underlined, and an arrow on each screen edge naming the neighbour it slides to.
    /// LB / RB do the same from the pad (<see cref="ScreenSwitcher"/> reads them; LT / RT page the top bar). The slide stops at the ends, so the
    /// first window has no left arrow and the last no right arrow.
    ///
    /// <para>One instance over every window: it shows while a hub window is the TOP modal and hides under a configure,
    /// Settings or anything else, so it never offers a slide the switcher would refuse. A guided path (MenuGuide) hides
    /// it too: the path leaves one way forward (Docs/claude/FTUE_DIALOGUE_AI.md, the LOCKED guided-path rule). It
    /// repaints only when the modal stack or the guide changes; nothing here runs per frame.</para>
    /// </summary>
    public class HubWindowNav : MonoBehaviour
    {
        [Serializable]
        public struct Item
        {
            [Tooltip("The hub window this strip entry names (one of ScreenSwitcher's hub windows).")]
            public ScreenSwitcher.ModalWindows window;
            [Tooltip("The entry's tab: its word is the window's name, which the edge arrows also show.")]
            public KitTextTab tab;
            [Tooltip("The tab's word. The edge arrow that slides to this window copies it.")]
            public TMP_Text label;
        }

        [Header("Wiring")]
        [Tooltip("The scene's switcher. Found in the scene when left empty (there is one per menu scene).")]
        [SerializeField] ScreenSwitcher screenSwitcher;
        [Tooltip("Shown (alpha 1, raycasts on) while a hub window is the top modal; hidden otherwise.")]
        [SerializeField] CanvasGroup group;

        [Header("Strip")]
        [Tooltip("One entry per hub window, in the slide order. The words are authored on each tab.")]
        [SerializeField] Item[] items = Array.Empty<Item>();

        [Header("Edge arrows")]
        [Tooltip("Slides to the previous window. Hidden on the first window (the slide stops there).")]
        [SerializeField] Button leftArrow;
        [Tooltip("The previous window's name, under the left chevron.")]
        [SerializeField] TMP_Text leftLabel;
        [Tooltip("Slides to the next window. Hidden on the last window.")]
        [SerializeField] Button rightArrow;
        [Tooltip("The next window's name, under the right chevron.")]
        [SerializeField] TMP_Text rightLabel;

        bool _shown = true;

        void Awake()
        {
            if (!screenSwitcher) screenSwitcher = FindAnyObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (leftArrow) leftArrow.onClick.AddListener(() => Slide(-1));
            if (rightArrow) rightArrow.onClick.AddListener(() => Slide(+1));
            for (int i = 0; i < items.Length; i++)
            {
                var window = items[i].window;
                if (items[i].tab) items[i].tab.onClick.AddListener(() => Jump(window));
            }
        }

        void OnEnable()
        {
            if (screenSwitcher) screenSwitcher.ModalStackChanged += Repaint;
            MenuGuide.Changed += Repaint;
            Repaint();
        }

        void OnDisable()
        {
            if (screenSwitcher) screenSwitcher.ModalStackChanged -= Repaint;
            MenuGuide.Changed -= Repaint;
        }

        void Slide(int direction)
        {
            if (screenSwitcher) screenSwitcher.SlideHubWindow(direction);
        }

        void Jump(ScreenSwitcher.ModalWindows window)
        {
            if (screenSwitcher) screenSwitcher.SlideHubWindowTo(window);
        }

        void Repaint()
        {
            var active = screenSwitcher ? screenSwitcher.ActiveHubWindow : ScreenSwitcher.ModalWindows.NONE;
            SetShown(active != ScreenSwitcher.ModalWindows.NONE && !MenuGuide.IsActive);
            if (!_shown) return;

            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i].tab) continue;
                items[i].tab.IsSelected = items[i].window == active;
                // A window that cannot open right now (locked, or not in this build) stays named but does not answer.
                items[i].tab.interactable = items[i].window == active || screenSwitcher.CanOpenHubWindow(items[i].window);
            }

            PaintArrow(leftArrow, leftLabel, screenSwitcher.HubNeighbour(active, -1));
            PaintArrow(rightArrow, rightLabel, screenSwitcher.HubNeighbour(active, +1));
        }

        void PaintArrow(Button arrow, TMP_Text label, ScreenSwitcher.ModalWindows neighbour)
        {
            if (!arrow) return;
            bool show = neighbour != ScreenSwitcher.ModalWindows.NONE;
            if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
            if (show && label) label.text = NameOf(neighbour);
        }

        string NameOf(ScreenSwitcher.ModalWindows window)
        {
            foreach (var item in items)
                if (item.window == window && item.label) return item.label.text;
            return string.Empty;
        }

        void SetShown(bool shown)
        {
            if (_shown == shown || !group) { _shown = shown; return; }
            _shown = shown;
            group.alpha = shown ? 1f : 0f;
            group.interactable = shown;
            group.blocksRaycasts = shown;
        }
    }
}
