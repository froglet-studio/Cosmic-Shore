using System;
using CosmicShore.ScriptableObjects;
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
    /// <see cref="HubShoulders"/>) unless <see cref="Plain"/> took the bumpers off; a press on a tab shows it. Laid out by Tools/Build/author_voyage_window.py.
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
        // The frame the window opened on: the RB that slid the hub onto this window is not also a tab step.
        int _openedFrame = -1;

        /// <summary>The tab on show.</summary>
        public int Shown => _shown;

        /// <summary>How many tabs the window has.</summary>
        public int Count => tabs.Length;

        /// <summary>What tab <paramref name="index"/> shows (its page), or null.</summary>
        public GameObject PageOf(int index) => index >= 0 && index < tabs.Length ? tabs[index].page : null;

        /// <summary>The row the tab buttons stand in (the first tab's parent), or null.</summary>
        public RectTransform TabStrip => tabs.Length > 0 && tabs[0].button ? tabs[0].button.transform.parent as RectTransform : null;

        /// <summary>How many tabs are still in the window (not dropped).</summary>
        public int Kept
        {
            get
            {
                int n = 0;
                for (int i = 0; i < tabs.Length; i++)
                    if (tabs[i].button && !IsDropped(i)) n++;
                return n;
            }
        }

        bool[] _dropped = Array.Empty<bool>();

        /// <summary>Whether tab <paramref name="index"/> was taken out of the window (<see cref="Drop{T}"/>).</summary>
        public bool IsDropped(int index) => index >= 0 && index < _dropped.Length && _dropped[index];

        /// <summary>
        /// Take out of the window every tab whose page carries a <typeparamref name="T"/> (on the page or under it): its
        /// button and page are switched off for good, <see cref="Plain"/> lays the others out without it, and
        /// <see cref="Show"/> and the bumpers pass over it. The indexes of the other tabs do not change. Call before
        /// <see cref="Plain"/>. The Quests tab left this way for its own window (Shombith, 2026-11-13: "Quest should be a
        /// seperate screen"; <see cref="QuestsModal"/>); the Saga and Episodes tabs can leave the same way.
        /// </summary>
        public void Drop<T>() where T : Component
        {
            if (_dropped.Length != tabs.Length) Array.Resize(ref _dropped, tabs.Length);
            for (int i = 0; i < tabs.Length; i++)
            {
                var t = tabs[i];
                if (_dropped[i] || !t.page || !t.page.GetComponentInChildren<T>(true)) continue;
                DropAt(i);
            }
            if (IsDropped(_shown)) Show(First());
        }

        /// <summary>The Saga and Episodes tabs (the tooltip's order). Both are locked and shown together at the foot of the
        /// Voyages page instead (<see cref="VoyageLockedStrip"/>, Frogma Rev 103 A).</summary>
        public const int SagaTab = 1, EpisodesTab = 3;

        /// <summary>Tab <paramref name="index"/>'s button in the top row, or null.</summary>
        public Button ButtonOf(int index) => index >= 0 && index < tabs.Length ? tabs[index].button : null;

        /// <summary>
        /// <see cref="Drop{T}"/> by place in the row, for a tab whose page carries nothing of its own to find it by: the
        /// Saga and Episodes tabs (Shombith, 2026-11-13: "Lock both saga and episodes for now. I do not think they should
        /// be different panels"). The Voyages tab (0) never leaves.
        /// </summary>
        public void Drop(int index)
        {
            if (index <= 0 || index >= tabs.Length) return;
            if (_dropped.Length != tabs.Length) Array.Resize(ref _dropped, tabs.Length);
            if (!_dropped[index]) DropAt(index);
            if (IsDropped(_shown)) Show(First());
        }

        void DropAt(int i)
        {
            var t = tabs[i];
            _dropped[i] = true;
            if (t.button) t.button.gameObject.SetActive(false);
            if (t.mark) t.mark.SetActive(false);
            if (t.page) t.page.SetActive(false);
        }

        // The first tab still in the window.
        int First()
        {
            for (int i = 0; i < tabs.Length; i++)
                if (!IsDropped(i)) return i;
            return -1;
        }

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
            Show(First());
            _openedFrame = Time.frameCount;
            if (shoulderInput && (!_window || _window.IsOpen)) HubShoulders.Claim(this);
            ColorTrace.Changed += Repaint;
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
            ColorTrace.Changed -= Repaint;
        }

        // A colour set was worn: the tab labels take its colours.
        void Repaint() => Show(_shown);

        void OnDestroy()
        {
            foreach (var t in tabs)
                if (t.button) t.button.onClick.RemoveAllListeners();
        }

        // Every visit opens on Voyages.
        void Opened()
        {
            Show(First());
            _openedFrame = Time.frameCount;
            if (shoulderInput) HubShoulders.Claim(this);
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
            if (ModePreviewWindow.AnyHasFocus || Time.frameCount == _openedFrame) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            int direction = pad.rightShoulder.wasPressedThisFrame ? +1 : pad.leftShoulder.wasPressedThisFrame ? -1 : 0;
            if (direction == 0) return;
            int to = Step(_shown, direction, tabs.Length);
            while (IsDropped(to) && to != Step(to, direction, tabs.Length)) to = Step(to, direction, tabs.Length);
            if (!IsDropped(to)) Show(to);
        }

        bool _plain;

        /// <summary>
        /// The tabs as the Voyages board draws them (Frogma Rev 87-98, Docs/MENU_REDESIGN_B.md sections 165-184): plain
        /// words in <paramref name="font"/>'s capitals, <paramref name="gap"/> apart from the left, the shown one light
        /// and underlined (its mark, 2 units), the others idle. No frame of any kind, and no LB / RB: a tab is pressed
        /// (Shombith, 2026-11-13: "it does not need an LB/RB support for now ... just clicks work fine"). Once is enough:
        /// later calls do nothing.
        /// <para>The frames are switched off as objects, not recoloured: every frame piece carries a kit painter, and the
        /// menu's theme pass (<c>UIThemeLive</c>) repaints every painter it finds, disabled ones too, which put the dark
        /// boxes back over a cleared fill.</para>
        /// </summary>
        public void Plain(TMP_FontAsset font, float size, float gap)
        {
            if (_plain) return;
            _plain = true;

            // No bumpers: the LB / RB keys go, and the window leaves the bumpers to the hub.
            shoulderInput = false;
            HubShoulders.Release(this);
            var first = tabs.Length > 0 && tabs[0].button ? (RectTransform)tabs[0].button.transform : null;
            var strip = first ? first.parent as RectTransform : null;
            if (strip)
                foreach (Transform child in strip)
                    if (!IsTab(child)) child.gameObject.SetActive(false);
                    else if (IsDroppedButton(child)) child.gameObject.SetActive(false);

            float left = 0f;
            for (int i = 0; i < tabs.Length; i++)
            {
                var t = tabs[i];
                if (!t.button || IsDropped(i)) continue;
                var rt = (RectTransform)t.button.transform;
                Strip(t);
                float lockW = 0f;
                var lockT = rt.Find("Lock") as RectTransform;
                if (lockT && lockT.gameObject.activeSelf)
                {
                    lockW = 22f;
                    lockT.anchorMin = lockT.anchorMax = new Vector2(0f, 0.5f);
                    lockT.pivot = new Vector2(0f, 0.5f);
                    lockT.anchoredPosition = Vector2.zero;
                }
                float width = 80f;
                if (t.label)
                {
                    if (font)
                    {
                        t.label.font = font;
                        t.label.fontSharedMaterial = font.material;
                    }
                    t.label.enableAutoSizing = false;
                    t.label.fontSize = size;
                    t.label.fontStyle = FontStyles.UpperCase;
                    t.label.characterSpacing = 14f;
                    t.label.textWrappingMode = TextWrappingModes.NoWrap;
                    t.label.alignment = TextAlignmentOptions.BottomLeft;
                    var lrt = t.label.rectTransform;
                    lrt.anchorMin = Vector2.zero;
                    lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = new Vector2(lockW, 10f);
                    lrt.offsetMax = Vector2.zero;
                    width = t.label.GetPreferredValues(t.label.text).x + lockW + 4f;
                }
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(width, 0f);
                rt.anchoredPosition = new Vector2(left, 0f);
                if (t.mark && t.mark.transform is RectTransform mark)
                {
                    mark.anchorMin = Vector2.zero;
                    mark.anchorMax = new Vector2(1f, 0f);
                    mark.pivot = new Vector2(0.5f, 0f);
                    mark.sizeDelta = new Vector2(0f, 2f);
                    mark.anchoredPosition = Vector2.zero;
                }
                left += width + gap;
            }
        }

        bool IsDroppedButton(Transform child)
        {
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i].button && tabs[i].button.transform == child) return IsDropped(i);
            return false;
        }

        bool IsTab(Transform child)
        {
            foreach (var t in tabs)
                if (t.button && t.button.transform == child) return true;
            return false;
        }

        /// <summary>
        /// Take one tab down to its words: every child but the label, the mark and the lock is switched off, the kit's
        /// painters on what is left are removed, and the button gets its own clear hit area with no colour transition.
        /// </summary>
        void Strip(Tab t)
        {
            var root = t.button.transform;
            foreach (Transform child in root)
            {
                bool keep = (t.label && child == t.label.transform) || (t.mark && child == t.mark.transform) || child.name == "Lock";
                if (!keep) child.gameObject.SetActive(false);
            }
            foreach (var mb in t.button.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb && mb is not Graphic && mb is not Selectable && mb.GetType().Name.StartsWith("Kit")) Destroy(mb);
            if (!root.TryGetComponent<Image>(out var hit)) hit = root.gameObject.AddComponent<Image>();
            hit.sprite = null;
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            t.button.targetGraphic = hit;
            t.button.transition = Selectable.Transition.None;
        }

        public static int Step(int from, int direction, int count) =>
            count <= 0 ? -1 : Mathf.Clamp(from + Math.Sign(direction), 0, count - 1);

        /// <summary>Show tab <paramref name="index"/> and hide the others.</summary>
        public void Show(int index)
        {
            if (index < 0 || index >= tabs.Length || IsDropped(index)) return;
            _shown = index;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = i == index;
                var t = tabs[i];
                if (IsDropped(i)) continue;
                if (t.page && t.page.activeSelf != on) t.page.SetActive(on);
                if (t.mark) t.mark.SetActive(on);
                if (t.label) t.label.color = ColorTrace.Retrace(ColorTrace.Ui, on ? selectedColor : idleColor);
            }
        }
    }
}
