using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyage window's four tabs (Shombith, 2026-11-02, Docs/MENU_REDESIGN_B.md sections 116 and 119): Voyages (the
    /// HyperSea map and the voyages the crew found; it opens first), Saga (Tandava and its Severing), Quests (the quest
    /// tracker) and Episodes (locked: coming soon). "Episode, saga are separate things and so is quest tracker." LB / RB
    /// step through them while the window is open (the window claims the bumpers from the hub slide,
    /// <see cref="HubShoulders"/>); a press on a tab shows it. Laid out by Tools/Build/author_voyage_window.py.
    /// </summary>
    public class VoyageWindowTabs : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            [Tooltip("The tab in the top row.")]
            public Button button;
            [Tooltip("The tab's label; it takes the selected or idle colour.")]
            public TMP_Text label;
            [Tooltip("The line under the open tab.")]
            public GameObject mark;
            [Tooltip("What the tab shows.")]
            public GameObject page;
        }

        [Header("Tabs")]
        [Tooltip("Voyages, Saga, Quests, Episodes, left to right. The first opens with the window.")]
        [SerializeField] Tab[] tabs = Array.Empty<Tab>();

        [Header("Look")]
        [SerializeField] Color selectedColor = new(0.902f, 0.914f, 1f, 1f);
        [SerializeField] Color idleColor = new(0.455f, 0.482f, 0.678f, 1f);

        [Header("Input")]
        [Tooltip("LB / RB step through the tabs while the window is open.")]
        [SerializeField] bool shoulderInput = true;

        ModalWindowManager _window;
        int _shown = -1;

        /// <summary>The tab on show.</summary>
        public int Shown => _shown;

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i].button) tabs[i].button.onClick.AddListener(() => Show(index));
            }
        }

        void OnEnable()
        {
            _window = GetComponentInParent<ModalWindowManager>(true);
            if (_window)
            {
                _window.OnModalOpened += Opened;
                _window.OnModalClosed += Closed;
            }
            Show(0);
            if (!_window || _window.IsOpen) HubShoulders.Claim(this);
        }

        void OnDisable()
        {
            if (_window)
            {
                _window.OnModalOpened -= Opened;
                _window.OnModalClosed -= Closed;
            }
            _window = null;
            HubShoulders.Release(this);
        }

        void OnDestroy()
        {
            foreach (var t in tabs)
                if (t.button) t.button.onClick.RemoveAllListeners();
        }

        // Every visit opens on Voyages.
        void Opened()
        {
            Show(0);
            HubShoulders.Claim(this);
        }

        void Closed() => HubShoulders.Release(this);

        void Update()
        {
            if (!shoulderInput || tabs.Length == 0) return;
            if (_window && !_window.IsOpen)
            {
                HubShoulders.Release(this);
                return;
            }
            if (ModePreviewWindow.AnyHasFocus) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            if (pad.rightShoulder.wasPressedThisFrame) Show(Step(_shown, +1, tabs.Length));
            else if (pad.leftShoulder.wasPressedThisFrame) Show(Step(_shown, -1, tabs.Length));
        }

        /// <summary>The tab LB (-1) or RB (+1) moves to from <paramref name="from"/>: it stops at either end.</summary>
        public static int Step(int from, int direction, int count) =>
            count <= 0 ? -1 : Mathf.Clamp(from + Math.Sign(direction), 0, count - 1);

        /// <summary>Show tab <paramref name="index"/> and hide the others.</summary>
        public void Show(int index)
        {
            if (index < 0 || index >= tabs.Length) return;
            _shown = index;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = i == index;
                var t = tabs[i];
                if (t.page && t.page.activeSelf != on) t.page.SetActive(on);
                if (t.mark) t.mark.SetActive(on);
                if (t.label) t.label.color = on ? selectedColor : idleColor;
            }
        }
    }
}
