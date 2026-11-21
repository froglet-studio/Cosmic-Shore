using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicShore.UI
{
    /// <summary>
    /// The PORT screen, the top bar's last tab, opened on 2026-10-27 (Docs/MENU_REDESIGN_B.md section 90; the Rev 27
    /// boards, Shombith: "lets open up the port now. Right now the port will only contain one tab called faction
    /// messages", then "The weekly leaderboard can also go to the port for now"). Two tabs, stepped with LB / RB:
    /// FACTION MESSAGES (<see cref="FactionMessagesPane"/>) and WEEKLY LEADERBOARD (<see cref="PortLeaderboardPane"/>).
    /// Only the shown tab is live: the messages tab holds its faction channel only while it is shown.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Port Screen")]
    public class PortScreen : MonoBehaviour, IScreen
    {
        [Tooltip("FACTION MESSAGES, then WEEKLY LEADERBOARD.")]
        [SerializeField] private KitTextTab[] tabs = new KitTextTab[2];
        [Tooltip("Each tab's pane, in the tabs' order.")]
        [SerializeField] private CanvasGroup[] panes = new CanvasGroup[2];
        [SerializeField] private FactionMessagesPane messages;
        [SerializeField] private PortLeaderboardPane leaderboard;
        [Tooltip("Step the tabs with the pad's shoulder buttons (LB / RB).")]
        [SerializeField] private bool shoulderInput = true;

        int _tab;
        bool _active;

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                if (tabs[i]) tabs[i].onClick.AddListener(() => Show(index));
            }
            for (int i = 0; i < panes.Length; i++) KitVisibility.SetShown(panes[i], false);
        }

        public void OnScreenEnter()
        {
            _active = true;
            Show(_tab, force: true);
        }

        public void OnScreenExit()
        {
            _active = false;
            if (messages) messages.SetShown(false);
            if (leaderboard) leaderboard.SetShown(false);
        }

        void Update()
        {
            if (!_active || !shoulderInput) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            if (pad.rightShoulder.wasPressedThisFrame) Show(Mathf.Min(_tab + 1, panes.Length - 1));
            else if (pad.leftShoulder.wasPressedThisFrame) Show(Mathf.Max(_tab - 1, 0));
        }

        public void Show(int tab) => Show(tab, false);

        void Show(int tab, bool force)
        {
            if (tab < 0 || tab >= panes.Length || (!force && tab == _tab)) return;
            _tab = tab;
            for (int i = 0; i < panes.Length; i++) KitVisibility.SetShown(panes[i], i == tab);
            for (int i = 0; i < tabs.Length; i++)
                if (tabs[i]) tabs[i].IsSelected = i == tab;
            if (messages) messages.SetShown(_active && tab == 0);
            if (leaderboard) leaderboard.SetShown(_active && tab == 1);

            // The pad lands inside the shown pane (a hidden pane's controls would hold it invisibly).
            var pane = panes[tab];
            var es = EventSystem.current;
            if (!es || !pane) return;
            var current = es.currentSelectedGameObject;
            if (current && current.transform.IsChildOf(pane.transform)) return;
            foreach (var s in pane.GetComponentsInChildren<UnityEngine.UI.Selectable>(false))
                if (s.IsInteractable())
                {
                    es.SetSelectedGameObject(s.gameObject);
                    break;
                }
        }
    }
}
