using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// An online pilot's profile, popped beside their avatar in the party panel (Rev 7, Docs/MENU_REDESIGN_B.md §47.3;
    /// the approved "Friend profile" board; the owner, 2026-10-17: "when friend is online and once we hover over their
    /// picture we should be able to see their profile as a popup and basic details"): their avatar in their domain
    /// ring, name, FRIEND (when they are on your friends list) and ONLINE, where they are (online, a lobby n / max, your
    /// party, a full party, a match and its game), and JOIN / SPECTATE and INVITE - the same presses as their row's.
    ///
    /// <para>One card for the scene. <see cref="FriendProfileHover"/> on each avatar (pointer) and on each row's buttons
    /// (pad focus) opens it on a <see cref="IFriendProfileSource"/>; the card reads what that row or seat already draws
    /// and repaints when it raises <see cref="IFriendProfileSource.ProfileChanged"/>. It sits beside the drawer's window,
    /// level with the avatar. A hover opens it after a short delay so a pointer crossing the list does not flash cards;
    /// leaving the avatar closes it unless the pointer moved onto the card. Every word is an authored field.</para>
    /// </summary>
    public class FriendProfilePopup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>The scene's card, while it is enabled.</summary>
        public static FriendProfilePopup Active { get; private set; }

        [Header("Wiring")]
        [Tooltip("The card's group: shown while a profile is open.")]
        [SerializeField] CanvasGroup group;
        [Tooltip("The card, placed beside the window and level with the avatar (pivot on its left edge).")]
        [SerializeField] RectTransform card;
        [Tooltip("The panel the card sits beside (the friends drawer's window). Empty: beside the avatar.")]
        [SerializeField] RectTransform edge;
        [Tooltip("Who is a friend: their FRIEND tag shows.")]
        [SerializeField] FriendsDataSO friendsData;
        [Tooltip("The chrome theme, for the ring when the domain is not known.")]
        [SerializeField] UIThemeSO theme;

        [Header("Card")]
        [SerializeField] Image avatar;
        [Tooltip("The octagon round the avatar: the pilot's domain colour, Space when it is not known.")]
        [SerializeField] Image ring;
        [SerializeField] TMP_Text nameText;
        [SerializeField] CanvasGroup friendTag;
        [Tooltip("Where they are: Online, In a lobby, In your party, Party full, In a match.")]
        [SerializeField] KitText stateLabel;
        [Tooltip("Beside it: their party's size (countTemplate).")]
        [SerializeField] KitText stateValue;
        [Tooltip("The game row, shown while they are in a match.")]
        [SerializeField] CanvasGroup gameRow;
        [SerializeField] KitText gameValue;
        [SerializeField] KitButton joinButton;
        [SerializeField] KitText joinLabel;
        [SerializeField] CanvasGroup joinGroup;
        [SerializeField] KitButton inviteButton;
        [SerializeField] KitText inviteLabel;
        [SerializeField] CanvasGroup inviteGroup;
        [SerializeField] Button closeButton;

        [Header("Report and Block (fair play, Threat Board T8)")]
        [Tooltip("Optional. Left empty, the popup makes Report, Block and the three reason buttons from a copy of the " +
                 "Invite control when it starts, so the scene needs no edit. Wire dedicated controls here to lay them out " +
                 "by hand. Words and the anti-spam cooldown: Resources/PlayerReportConfig.")]
        [SerializeField] KitButton reportButton;
        [SerializeField] KitText reportLabel;
        [SerializeField] CanvasGroup reportGroup;
        [SerializeField] KitButton blockButton;
        [SerializeField] KitText blockLabel;
        [SerializeField] CanvasGroup blockGroup;

        [Header("Words (empty = blank)")]
        [SerializeField] string onlineWord = "Online";
        [SerializeField] string inLobbyWord = "In a lobby";
        [SerializeField] string inYourPartyWord = "In your party";
        [SerializeField] string partyFullWord = "Party full";
        [SerializeField] string inMatchWord = "In a match";
        [Tooltip("Their party's size. {count} and {max} are filled in.")]
        [SerializeField] string countTemplate = "{count} / {max}";
        [SerializeField] string joinWord = "Join";
        [SerializeField] string spectateWord = "Spectate";
        [SerializeField] string inviteWord = "Invite";
        [SerializeField] string invitedWord = "Invited";

        [Header("Timing")]
        [Tooltip("Seconds the pointer rests on an avatar before the card opens.")]
        [SerializeField, Min(0f)] float showDelay = 0.3f;
        [Tooltip("Seconds after the pointer leaves before the card closes (time to reach the card).")]
        [SerializeField, Min(0f)] float hideDelay = 0.25f;
        [Tooltip("Gap, in canvas units, between the window's edge and the card.")]
        [SerializeField] float gap = 16f;

        IFriendProfileSource _source;
        IFriendProfileSource _pending;
        [Inject] FriendsServiceFacade friendsService;

        struct ActionRow
        {
            public KitButton Button;
            public KitText Label;
            public CanvasGroup Group;
            public bool Exists => Button;
        }

        ActionRow _report, _block;
        readonly ActionRow[] _reasons = new ActionRow[3];
        static readonly PlayerReportReason[] ReasonOrder =
            { PlayerReportReason.Cheating, PlayerReportReason.OffensiveName, PlayerReportReason.Trolling };
        bool _choosingReason;
        readonly HashSet<string> _reportFailed = new();

        RectTransform _anchor;
        bool _overSource, _overCard, _focused;
        Coroutine _timer;
        static readonly Vector3[] Corners = new Vector3[4];

        void Awake()
        {
            if (joinButton) joinButton.onClick.AddListener(PressJoin);
            if (inviteButton) inviteButton.onClick.AddListener(PressInvite);
            if (closeButton) closeButton.onClick.AddListener(HideNow);
            BuildSafetyRows();
        }

        // ---------------- Report and Block ----------------

        void BuildSafetyRows()
        {
            _report = reportButton ? new ActionRow { Button = reportButton, Label = reportLabel, Group = reportGroup }
                                   : CopyInviteRow("Report");
            _block = blockButton ? new ActionRow { Button = blockButton, Label = blockLabel, Group = blockGroup }
                                 : CopyInviteRow("Block");
            for (int i = 0; i < _reasons.Length; i++)
                _reasons[i] = CopyInviteRow("Report Reason " + ReasonOrder[i]);

            if (_report.Exists) _report.Button.onClick.AddListener(PressReport);
            if (_block.Exists) _block.Button.onClick.AddListener(PressBlock);
            for (int i = 0; i < _reasons.Length; i++)
            {
                if (!_reasons[i].Exists) continue;
                var reason = ReasonOrder[i];
                _reasons[i].Button.onClick.AddListener(() => PressReason(reason));
            }

            var words = PlayerReportConfigSO.LoadOrDefault();
            SetLabel(_reasons[0], words.CheatingWord);
            SetLabel(_reasons[1], words.NameWord);
            SetLabel(_reasons[2], words.TrollingWord);
        }

        /// <summary>A copy of the Invite control (its group, or the button alone) beside it, with no click listeners.</summary>
        ActionRow CopyInviteRow(string name)
        {
            GameObject template = inviteGroup ? inviteGroup.gameObject : inviteButton ? inviteButton.gameObject : null;
            if (!template) return default;

            var copy = Instantiate(template, template.transform.parent);
            copy.name = name;
            var button = copy.GetComponentInChildren<KitButton>(true);
            if (!button) { Destroy(copy); return default; }
            // Drop Invite's own listeners: runtime ones, and any wired in the inspector (switched off).
            button.onClick.RemoveAllListeners();
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            if (!copy.TryGetComponent(out CanvasGroup copyGroup)) copyGroup = copy.AddComponent<CanvasGroup>();
            KitVisibility.SetShown(copyGroup, false);
            return new ActionRow { Button = button, Label = copy.GetComponentInChildren<KitText>(true), Group = copyGroup };
        }

        static void SetLabel(ActionRow row, string word)
        {
            if (row.Label) row.Label.SetText(word);
        }

        static void SetShown(ActionRow row, bool shown)
        {
            if (row.Group) KitVisibility.SetShown(row.Group, shown);
            else if (row.Button) row.Button.gameObject.SetActive(shown);
        }

        void RepaintSafetyRows(string playerId)
        {
            bool canAct = !string.IsNullOrEmpty(playerId);
            if (_choosingReason && canAct)
            {
                KitVisibility.SetShown(joinGroup, false);
                KitVisibility.SetShown(inviteGroup, false);
                SetShown(_report, false);
                SetShown(_block, false);
                foreach (var row in _reasons) SetShown(row, true);
                return;
            }

            foreach (var row in _reasons) SetShown(row, false);
            var words = PlayerReportConfigSO.LoadOrDefault();

            SetShown(_report, canAct);
            if (_report.Exists && canAct)
            {
                bool reported = PlayerReports.RecentlyReported(playerId);
                _report.Button.interactable = !reported;
                SetLabel(_report, reported ? words.ReportedWord
                    : _reportFailed.Contains(playerId) ? words.ReportFailedWord : words.ReportWord);
            }

            bool canBlock = canAct && friendsService != null;
            SetShown(_block, canBlock);
            if (_block.Exists && canBlock)
            {
                bool blocked = friendsService.IsBlocked(playerId);
                _block.Button.interactable = !blocked;
                SetLabel(_block, blocked ? words.BlockedWord : words.BlockWord);
            }
        }

        void PressReport()
        {
            if (_source == null) return;
            _choosingReason = true;
            Repaint();
        }

        void PressReason(PlayerReportReason reason)
        {
            if (_source == null || !_source.TryGetProfile(out var p)) return;
            _choosingReason = false;
            SendReportAsync(p.PlayerId, reason).Forget();
            Repaint();
        }

        async UniTaskVoid SendReportAsync(string playerId, PlayerReportReason reason)
        {
            bool sent = await PlayerReports.ReportAsync(playerId, reason);
            if (sent) _reportFailed.Remove(playerId);
            else _reportFailed.Add(playerId);
            if (this && _source != null) Repaint();
        }

        void PressBlock()
        {
            if (_source == null || friendsService == null || !_source.TryGetProfile(out var p)) return;
            BlockAsync(p.PlayerId).Forget();
        }

        async UniTaskVoid BlockAsync(string playerId)
        {
            await friendsService.BlockPlayerAsync(playerId);
            if (this && _source != null) Repaint();
        }

        void OnEnable()
        {
            Active = this;
            KitVisibility.SetShown(group, false);
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            HideNow();
        }

        /// <summary>The pointer rests on <paramref name="source"/>'s avatar.</summary>
        public void Hover(IFriendProfileSource source, RectTransform anchor)
        {
            _overSource = true;
            if (source == _source) { CancelTimer(); return; }
            // Moving from one avatar to the next while a card is open swaps it at once.
            if (_source != null) { Show(source, anchor); return; }
            _pending = source;
            _anchor = anchor;
            Restart(ShowAfter(showDelay));
        }

        /// <summary>The pointer left <paramref name="source"/>'s avatar.</summary>
        public void Unhover(IFriendProfileSource source)
        {
            if (source == _pending && _source == null) { _pending = null; CancelTimer(); }
            if (source != _source) return;
            _overSource = false;
            HideSoon();
        }

        /// <summary>The pad moved onto one of <paramref name="source"/>'s controls: the card opens at once.</summary>
        public void Focus(IFriendProfileSource source, RectTransform anchor)
        {
            _focused = true;
            Show(source, anchor);
        }

        /// <summary>The pad moved off <paramref name="source"/>.</summary>
        public void Unfocus(IFriendProfileSource source)
        {
            if (source != _source) return;
            _focused = false;
            HideSoon();
        }

        /// <summary><paramref name="source"/> went away (its row was rebuilt or hidden).</summary>
        public void Release(IFriendProfileSource source)
        {
            if (source == _pending) { _pending = null; CancelTimer(); }
            if (source == _source) HideNow();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _overCard = true;
            if (_source != null) CancelTimer();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _overCard = false;
            HideSoon();
        }

        void Show(IFriendProfileSource source, RectTransform anchor)
        {
            CancelTimer();
            _pending = null;
            if (_source != source)
            {
                if (_source != null) _source.ProfileChanged -= Repaint;
                _choosingReason = false;
                _source = source;
                _source.ProfileChanged += Repaint;
            }
            _anchor = anchor;
            Repaint();
            if (_source == null) return;
            Place();
            KitVisibility.SetShown(group, true);
        }

        void HideSoon()
        {
            if (_overSource || _overCard || _focused || _source == null) return;
            Restart(HideAfter(hideDelay));
        }

        void HideNow()
        {
            CancelTimer();
            if (_source != null) _source.ProfileChanged -= Repaint;
            _source = null;
            _pending = null;
            _overSource = _overCard = _focused = false;
            _choosingReason = false;
            KitVisibility.SetShown(group, false);
        }

        // Unscaled: the menu runs paused.
        IEnumerator ShowAfter(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
            _timer = null;
            if (_pending != null && _overSource) Show(_pending, _anchor);
        }

        IEnumerator HideAfter(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
            _timer = null;
            if (!_overSource && !_overCard && !_focused) HideNow();
        }

        void Restart(IEnumerator routine)
        {
            CancelTimer();
            if (isActiveAndEnabled) _timer = StartCoroutine(routine);
        }

        void CancelTimer()
        {
            if (_timer == null) return;
            StopCoroutine(_timer);
            _timer = null;
        }

        void Repaint()
        {
            if (_source == null) return;
            if (!_source.TryGetProfile(out var p)) { HideNow(); return; }

            if (avatar)
            {
                avatar.sprite = p.Avatar;
                avatar.enabled = p.Avatar;
            }
            if (ring) ring.color = p.DomainColour ?? UITheme.Resolve(theme, UIColorToken.NeutralLightest);
            if (nameText) nameText.text = p.DisplayName ?? string.Empty;
            KitVisibility.SetShown(friendTag, IsFriend(p.PlayerId));

            if (stateLabel) stateLabel.SetText(StateWord(p.Status));
            bool counted = p.PartyMaxSlots > 0 && p.Status is OnlineInfoEntry.Status.InLobby
                or OnlineInfoEntry.Status.InYourParty or OnlineInfoEntry.Status.LobbyFull;
            if (stateValue)
                stateValue.SetText(counted
                    ? countTemplate.Replace("{count}", p.PartyMemberCount.ToString()).Replace("{max}", p.PartyMaxSlots.ToString())
                    : string.Empty);
            bool inMatch = p.Status == OnlineInfoEntry.Status.InMatch && !string.IsNullOrEmpty(p.MatchName);
            KitVisibility.SetShown(gameRow, inMatch);
            if (gameValue) gameValue.SetText(inMatch ? p.MatchName : string.Empty);

            bool spectate = p.Join is OnlineInfoEntry.JoinMode.Spectate or OnlineInfoEntry.JoinMode.SpectateDisabled;
            KitVisibility.SetShown(joinGroup, p.Join != OnlineInfoEntry.JoinMode.Hidden);
            if (joinButton)
            {
                joinButton.Variant = spectate ? KitButtonVariant.Default : KitButtonVariant.Play;   // joining is the call to action; watching is neutral
                joinButton.interactable = p.Join is OnlineInfoEntry.JoinMode.Join or OnlineInfoEntry.JoinMode.Spectate;
            }
            if (joinLabel) joinLabel.SetText(spectate ? spectateWord : joinWord);

            KitVisibility.SetShown(inviteGroup, p.CanInvite || p.InvitePending);
            if (inviteButton) inviteButton.interactable = p.CanInvite;
            if (inviteLabel) inviteLabel.SetText(p.InvitePending ? invitedWord : inviteWord);

            RepaintSafetyRows(p.PlayerId);
        }

        string StateWord(OnlineInfoEntry.Status status) => status switch
        {
            OnlineInfoEntry.Status.InLobby => inLobbyWord,
            OnlineInfoEntry.Status.InMatch => inMatchWord,
            OnlineInfoEntry.Status.LobbyFull => partyFullWord,
            OnlineInfoEntry.Status.InYourParty => inYourPartyWord,
            _ => onlineWord,
        };

        bool IsFriend(string playerId)
        {
            if (!friendsData || friendsData.Friends == null || string.IsNullOrEmpty(playerId)) return false;
            foreach (var friend in friendsData.Friends)
                if (friend.PlayerId == playerId) return true;
            return false;
        }

        // Beside the window, level with the avatar, kept inside the card's parent.
        void Place()
        {
            if (!card || !(card.parent is RectTransform parent)) return;
            var from = edge ? edge : _anchor;
            float x = 0f, y = 0f;
            if (from)
            {
                from.GetWorldCorners(Corners);
                x = parent.InverseTransformPoint(Corners[2]).x + gap;
            }
            if (_anchor) y = parent.InverseTransformPoint(_anchor.TransformPoint(_anchor.rect.center)).y;
            var bounds = parent.rect;
            float half = card.rect.height * card.localScale.y / 2f;
            y = Mathf.Clamp(y, bounds.yMin + half + gap, Mathf.Max(bounds.yMin + half + gap, bounds.yMax - half - gap));
            x = Mathf.Min(x, bounds.xMax - card.rect.width - gap);
            card.localPosition = new Vector3(x, y, 0f);
        }

        void PressJoin()
        {
            var source = _source;
            HideNow();
            source?.RequestJoin();
        }

        void PressInvite()
        {
            if (_source == null) return;
            _source.RequestInvite();
            if (_source == null) return;
            // The row raises ProfileChanged when its invite goes out; repaint in case it already had.
            Repaint();
        }
    }
}
