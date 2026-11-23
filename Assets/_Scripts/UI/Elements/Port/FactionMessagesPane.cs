using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Port's FACTION MESSAGES tab (Docs/MENU_REDESIGN_B.md section 90; the Rev 27 boards, Shombith 2026-10-27).
    /// Today every player is in one faction, All Pilots; a faction is a Vivox text channel
    /// (<see cref="FactionChatService"/>), joined while this tab is shown and left when it is not.
    ///
    /// <para>The feed holds the channel's recent history and every message pushed after it, newest at the foot. The
    /// field sends with Enter, the pad's X or SEND; the rules (140 characters, one message every 3 s, a player not
    /// surely 13 reads only) are checked here and each refusal has its own authored line. A message's actions (A or Y
    /// on it, or a click) are Report, which goes to UGS Moderation, and Mute, kept on this device.</para>
    ///
    /// <para>Every word is an authored field (CLAUDE.md, player-facing text); an empty one shows nothing.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Faction Messages Pane")]
    public class FactionMessagesPane : MonoBehaviour
    {
        [Header("Faction")]
        [Tooltip("The faction this pane shows. Every player is in \"all\" until factions of their own exist.")]
        [SerializeField] private string factionId = "all";
        [Tooltip("The faction's name, as the card and the hints say it.")]
        [SerializeField] private string factionName = "All Pilots";
        [SerializeField] private TMP_Text factionNameText;

        [Header("Rules")]
        [SerializeField, Min(1)] private int maxLength = 140;
        [SerializeField, Min(0f)] private float minIntervalSeconds = 3f;
        [Tooltip("A player not surely this old by the birth year the age screen recorded can read but not send.")]
        [SerializeField, Min(0)] private int minPostAge = 13;
        [Tooltip("How many recent messages opening the tab reads from the channel's history.")]
        [SerializeField, Range(1, 100)] private int historyCount = 50;
        [Tooltip("The most messages the feed keeps.")]
        [SerializeField, Range(10, 200)] private int feedCapacity = 100;
        [Tooltip("Seconds between tries while the channel cannot be reached.")]
        [SerializeField, Min(1f)] private float retrySeconds = 15f;

        [Header("Feed")]
        [SerializeField] private ScrollRect feedScroll;
        [SerializeField] private RectTransform feedContent;
        [SerializeField] private FactionMessageRow rowPrefab;
        [Tooltip("Resolves a sender's avatar id to a face (the profile icons).")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("Feed states")]
        [Tooltip("Shown over an empty feed: nothing written yet, tuning in, or offline.")]
        [SerializeField] private CanvasGroup statusGroup;
        [SerializeField] private TMP_Text statusTitle;
        [SerializeField] private TMP_Text statusLine;
        [Tooltip("The spinner, shown while tuning in.")]
        [SerializeField] private CanvasGroup loadingGroup;
        [Tooltip("TRY AGAIN, shown while offline.")]
        [SerializeField] private CanvasGroup retryGroup;
        [SerializeField] private Button retryButton;
        [SerializeField] private string emptyTitle = "No messages yet";
        [SerializeField] private string emptyLine = "Say hello to every pilot in the faction.";
        [SerializeField] private string loadingTitle = "Tuning in to the faction...";
        [SerializeField] private string offlineTitle = "Faction messages are offline";
        [SerializeField] private string offlineLine = "Check your connection. Port keeps trying every 15 seconds.";

        [Header("Composer")]
        [SerializeField] private TMP_InputField input;
        [SerializeField] private Button sendButton;
        [Tooltip("Dims the field and SEND while nothing can be sent.")]
        [SerializeField] private CanvasGroup composerGroup;
        [SerializeField] private TMP_Text countText;
        [Tooltip("{count} characters typed, {max} allowed.")]
        [SerializeField] private string countTemplate = "{count} / {max}";
        [SerializeField] private TMP_Text hintText;
        [Tooltip("The line under the field at rest. {faction} is the faction's name.")]
        [SerializeField] private string readyHint = "Enter sends. Every pilot in {faction} sees it.";
        [Tooltip("After a message went. {seconds} until the next may go.")]
        [SerializeField] private string sentHint = "Sent. Your next message can go in {seconds} s.";
        [SerializeField] private string tooLongHint = "Too long - {max} characters at most.";
        [Tooltip("{seconds} until the next message may go.")]
        [SerializeField] private string tooSoonHint = "Not sent: too fast. Wait {seconds} s, then send it again.";
        [SerializeField] private string readOnlyHint = "This account can read faction messages but not send them.";
        [SerializeField] private string offlineHint = "Not sent - faction messages are offline.";

        [Header("Time")]
        [SerializeField] private string timeNow = "now";
        [Tooltip("{n} minutes ago.")]
        [SerializeField] private string timeMinutes = "{n}m";
        [SerializeField] private string timeHours = "{n}h";
        [SerializeField] private string timeDays = "{n}d";

        [Header("Message actions")]
        [SerializeField] private CanvasGroup actionsGroup;
        [Tooltip("Moved beside the message whose actions are open.")]
        [SerializeField] private RectTransform actionsPanel;
        [SerializeField] private Button reportButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private TMP_Text muteLabel;
        [Tooltip("{name} is the sender's name.")]
        [SerializeField] private string muteTemplate = "Mute {name}";
        [SerializeField] private Button cancelButton;
        [Tooltip("Muted pilots on this device, with UNMUTE ALL. Hidden while nobody is muted.")]
        [SerializeField] private CanvasGroup mutedGroup;
        [SerializeField] private TMP_Text mutedText;
        [Tooltip("{count} pilots muted.")]
        [SerializeField] private string mutedTemplate = "Muted pilots: {count}";
        [SerializeField] private Button unmuteAllButton;

        [Header("Toast")]
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private string reportSent = "Report sent. Thank you.";
        [SerializeField] private string reportFailed = "The report could not be sent. Try again later.";
        [SerializeField, Min(0.5f)] private float toastSeconds = 2.6f;

        [Header("Colour")]
        [SerializeField] private UIThemeSO theme;

        enum FeedState { Loading, Ready, Offline }

        readonly List<FactionMessageRow> _rows = new();
        FactionChatLog _log;
        FeedState _state = FeedState.Loading;
        bool _shown, _joined, _connecting, _sending, _countingDown, _refocus;
        float _retryAt = -1f, _toastUntil = -1f, _clockAt;
        DateTime _lastSentUtc = DateTime.MinValue;
        FactionMessageRow _actionsFor;
        string Channel => FactionChatRules.ChannelFor(factionId);

        void Awake()
        {
            _log = new FactionChatLog(feedCapacity);
            if (sendButton) sendButton.onClick.AddListener(Send);
            if (retryButton) retryButton.onClick.AddListener(() => Connect().Forget());
            if (reportButton) reportButton.onClick.AddListener(() => ReportOpen().Forget());
            if (muteButton) muteButton.onClick.AddListener(MuteOpen);
            if (cancelButton) cancelButton.onClick.AddListener(CloseActions);
            if (unmuteAllButton) unmuteAllButton.onClick.AddListener(UnmuteAll);
            if (input)
            {
                input.characterLimit = maxLength;
                input.richText = false;
                input.onValueChanged.AddListener(_ => RedrawCount());
                // Enter sends and keeps the keyboard in the field, so the next message can be typed straight away.
                input.onSubmit.AddListener(_ => { _refocus = true; Send(); });
            }
            if (factionNameText) factionNameText.text = factionName;
            KitVisibility.SetShown(actionsGroup, false);
            KitVisibility.SetShown(toastGroup, false);
        }

        void OnEnable() => FactionChatService.MessageReceived += OnMessage;

        void OnDisable()
        {
            FactionChatService.MessageReceived -= OnMessage;
            if (_shown) SetShown(false);
        }

        /// <summary>The Port shows or hides this tab. Shown joins the faction's channel; hidden leaves it.</summary>
        public void SetShown(bool shown)
        {
            if (shown == _shown) return;
            _shown = shown;
            if (shown)
            {
                RedrawMuted();
                RedrawCount();
                RedrawHint(FactionSendRefusal.None, 0);
                Connect().Forget();
            }
            else
            {
                CloseActions();
                _retryAt = -1f;
                if (_joined) FactionChatService.LeaveAsync(Channel).Forget();
                _joined = false;
            }
        }

        void Update()
        {
            if (!_shown) return;
            float now = Time.unscaledTime;
            if (_retryAt > 0f && now >= _retryAt) Connect().Forget();
            if (_toastUntil > 0f && now >= _toastUntil)
            {
                _toastUntil = -1f;
                KitVisibility.SetShown(toastGroup, false);
            }
            if (now >= _clockAt)
            {
                _clockAt = now + 1f;
                RedrawTimes();
                // Only the "next message in N s" countdown ticks; a refusal line stays until the player acts.
                if (_countingDown) RedrawHint(FactionSendRefusal.None, Wait());
            }

            var pad = Gamepad.current;
            if (pad == null) return;
            if (KitVisibility.IsShown(actionsGroup))
            {
                if (pad.buttonEast.wasPressedThisFrame) CloseActions();
                return;
            }
            if (pad.buttonWest.wasPressedThisFrame) Send();
            else if (pad.buttonNorth.wasPressedThisFrame && SelectedRow() is { } row) OpenActions(row);
        }

        // ── Channel ────────────────────────────────────────────────────────────

        async UniTaskVoid Connect()
        {
            if (_connecting || _joined) return;
            _connecting = true;
            _retryAt = -1f;
            if (_log.Messages.Count == 0) SetState(FeedState.Loading);
            bool joined = await FactionChatService.JoinAsync(Channel, LocalName());
            _connecting = false;
            if (!this || !_shown)
            {
                if (joined) FactionChatService.LeaveAsync(Channel).Forget();
                return;
            }
            if (!joined)
            {
                SetState(FeedState.Offline);
                _retryAt = Time.unscaledTime + retrySeconds;
                return;
            }
            _joined = true;
            var history = await FactionChatService.HistoryAsync(Channel, historyCount);
            if (!this) return;
            foreach (var m in history) _log.Add(m);
            SetState(FeedState.Ready);
            Rebuild(scrollToEnd: true);
        }

        void OnMessage(string channel, FactionMessage message)
        {
            if (!_shown || channel != Channel || !_log.Add(message)) return;
            bool atEnd = !feedScroll || feedScroll.verticalNormalizedPosition <= 0.02f || message.FromSelf;
            Rebuild(atEnd);
        }

        void SetState(FeedState state)
        {
            _state = state;
            bool empty = _log.Messages.Count == 0;
            KitVisibility.SetShown(statusGroup, state != FeedState.Ready || empty);
            KitVisibility.SetShown(loadingGroup, state == FeedState.Loading);
            KitVisibility.SetShown(retryGroup, state == FeedState.Offline);
            if (statusTitle)
                statusTitle.text = state switch { FeedState.Loading => loadingTitle, FeedState.Offline => offlineTitle, _ => emptyTitle };
            if (statusLine)
                statusLine.text = state switch { FeedState.Loading => string.Empty, FeedState.Offline => offlineLine, _ => emptyLine };
            bool canSend = state == FeedState.Ready && CanPost;
            if (composerGroup) composerGroup.alpha = canSend ? 1f : 0.5f;
            if (input) input.interactable = canSend;
            if (sendButton) sendButton.interactable = canSend;
            RedrawHint(CanPost ? FactionSendRefusal.None : FactionSendRefusal.ReadOnly, 0);
        }

        // ── Feed ───────────────────────────────────────────────────────────────

        void Rebuild(bool scrollToEnd)
        {
            if (!rowPrefab || !feedContent) return;
            int shown = 0;
            var now = DateTime.UtcNow;
            foreach (var m in _log.Messages)
            {
                if (FactionMutes.IsMuted(m.SenderId)) continue;
                if (shown == _rows.Count)
                {
                    var row = Instantiate(rowPrefab, feedContent);
                    row.name = $"FactionMessage{_rows.Count + 1}";
                    row.Picked += OpenActions;
                    _rows.Add(row);
                }
                var r = _rows[shown++];
                r.gameObject.SetActive(true);
                r.Show(m, Ago(m.SentUtc, now), profileIcons);
            }
            for (int i = shown; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
            if (_state == FeedState.Ready) KitVisibility.SetShown(statusGroup, shown == 0);
            if (scrollToEnd && feedScroll)
            {
                Canvas.ForceUpdateCanvases();
                feedScroll.verticalNormalizedPosition = 0f;
            }
        }

        void RedrawTimes()
        {
            var now = DateTime.UtcNow;
            foreach (var row in _rows)
                if (row.gameObject.activeSelf) row.SetTime(Ago(row.Message.SentUtc, now));
        }

        string Ago(DateTime sentUtc, DateTime nowUtc)
        {
            var span = nowUtc - sentUtc;
            if (span.TotalMinutes < 1) return timeNow;
            if (span.TotalHours < 1) return TemplateText.Fill(timeMinutes, "n", ((int)span.TotalMinutes).ToString());
            if (span.TotalDays < 1) return TemplateText.Fill(timeHours, "n", ((int)span.TotalHours).ToString());
            return TemplateText.Fill(timeDays, "n", ((int)span.TotalDays).ToString());
        }

        // ── Sending ────────────────────────────────────────────────────────────

        bool CanPost => FactionChatRules.CanPost(FairPlayAge.BirthYear, DateTime.UtcNow.Year, minPostAge);

        int Wait() => FactionChatRules.WaitSeconds(_lastSentUtc, DateTime.UtcNow, minIntervalSeconds);

        /// <summary>Sends the field's message, or says why not and keeps the words in the field.</summary>
        public void Send()
        {
            if (!_shown || _sending || !input) return;
            string text = FactionChatRules.Clean(input.text);
            if (string.IsNullOrEmpty(text))
            {
                _refocus = false;
                return;
            }
            var refusal = _state != FeedState.Ready
                ? FactionSendRefusal.Offline
                : FactionChatRules.Check(text, maxLength, _lastSentUtc, DateTime.UtcNow, minIntervalSeconds, CanPost);
            if (refusal != FactionSendRefusal.None)
            {
                RedrawHint(refusal, Wait());
                if (_refocus) input.ActivateInputField();
                _refocus = false;
                return;
            }
            SendAsync(text).Forget();
        }

        async UniTaskVoid SendAsync(string text)
        {
            _sending = true;
            bool sent = await FactionChatService.SendAsync(Channel, text, LocalAvatarId(), LocalName());
            _sending = false;
            if (!this) return;
            if (!sent)
            {
                RedrawHint(FactionSendRefusal.Offline, 0);
                _refocus = false;
                return;
            }
            _lastSentUtc = DateTime.UtcNow;
            input.text = string.Empty;
            RedrawHint(FactionSendRefusal.None, Wait());
            if (_refocus) input.ActivateInputField();
            _refocus = false;
        }

        void RedrawCount()
        {
            if (!countText) return;
            int n = FactionChatRules.Length(input ? input.text : string.Empty);
            countText.text = TemplateText.Fill(countTemplate, "count", n.ToString(), "max", maxLength.ToString());
        }

        void RedrawHint(FactionSendRefusal refusal, int wait)
        {
            if (!hintText) return;
            if (refusal == FactionSendRefusal.None && !CanPost) refusal = FactionSendRefusal.ReadOnly;
            string line = refusal switch
            {
                FactionSendRefusal.TooLong => TemplateText.Fill(tooLongHint, "max", maxLength.ToString()),
                FactionSendRefusal.TooSoon => TemplateText.Fill(tooSoonHint, "seconds", wait.ToString()),
                FactionSendRefusal.ReadOnly => readOnlyHint,
                FactionSendRefusal.Offline => offlineHint,
                _ => wait > 0 ? TemplateText.Fill(sentHint, "seconds", wait.ToString())
                              : TemplateText.Fill(readyHint, "faction", factionName),
            };
            hintText.text = line;
            _countingDown = refusal == FactionSendRefusal.None && wait > 0;
            // A problem reads in Danger; everything else is quiet information (Docs/UI_COLOR_RULES.md).
            bool problem = refusal is FactionSendRefusal.TooLong or FactionSendRefusal.TooSoon or FactionSendRefusal.Offline;
            hintText.color = UITheme.Resolve(theme, problem ? UIColorToken.Danger : UIColorToken.NeutralLightest);
        }

        static string LocalName()
        {
            var profile = PlayerDataService.Instance ? PlayerDataService.Instance.CurrentProfile : null;
            return profile?.Identity?.DisplayName ?? string.Empty;
        }

        static int LocalAvatarId()
        {
            var profile = PlayerDataService.Instance ? PlayerDataService.Instance.CurrentProfile : null;
            return profile?.Identity?.AvatarId ?? -1;
        }

        // ── Message actions ────────────────────────────────────────────────────

        FactionMessageRow SelectedRow()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (!selected) return null;
            foreach (var row in _rows)
                if (row.gameObject.activeSelf && row.Button && row.Button.gameObject == selected) return row;
            return null;
        }

        void OpenActions(FactionMessageRow row)
        {
            if (!row || row.Message.FromSelf) return;
            _actionsFor = row;
            if (muteLabel) muteLabel.text = TemplateText.Fill(muteTemplate, "name", row.Message.SenderName);
            if (reportButton) reportButton.interactable = !PlayerReports.RecentlyReported(row.Message.SenderId);
            if (actionsPanel && actionsPanel.parent is RectTransform space)
            {
                // Beside the message: the panel's top at the row's foot, kept inside the feed.
                var corners = new Vector3[4];
                ((RectTransform)row.transform).GetWorldCorners(corners);
                Vector2 local = space.InverseTransformPoint(corners[0]);
                float top = Mathf.Clamp(local.y, space.rect.yMin + actionsPanel.rect.height, space.rect.yMax);
                actionsPanel.anchoredPosition = new Vector2(actionsPanel.anchoredPosition.x, top - space.rect.yMax);
            }
            KitVisibility.SetShown(actionsGroup, true);
            if (EventSystem.current && reportButton)
                EventSystem.current.SetSelectedGameObject((reportButton.interactable ? reportButton : muteButton).gameObject);
        }

        void CloseActions()
        {
            if (!KitVisibility.IsShown(actionsGroup)) return;
            KitVisibility.SetShown(actionsGroup, false);
            if (EventSystem.current && _actionsFor && _actionsFor.Button && _actionsFor.gameObject.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(_actionsFor.Button.gameObject);
            _actionsFor = null;
        }

        async UniTaskVoid ReportOpen()
        {
            if (!_actionsFor) return;
            string sender = _actionsFor.Message.SenderId;
            CloseActions();
            bool sent = await PlayerReports.ReportAsync(sender, PlayerReportReason.OffensiveMessage);
            if (!this) return;
            Toast(sent ? reportSent : reportFailed);
        }

        void MuteOpen()
        {
            if (!_actionsFor) return;
            FactionMutes.Mute(_actionsFor.Message.SenderId);
            _actionsFor = null;
            KitVisibility.SetShown(actionsGroup, false);
            RedrawMuted();
            Rebuild(scrollToEnd: false);
            if (EventSystem.current && input) EventSystem.current.SetSelectedGameObject(input.gameObject);
        }

        void UnmuteAll()
        {
            FactionMutes.UnmuteAll();
            RedrawMuted();
            Rebuild(scrollToEnd: true);
        }

        void RedrawMuted()
        {
            int n = FactionMutes.Count;
            KitVisibility.SetShown(mutedGroup, n > 0);
            if (mutedText) mutedText.text = TemplateText.Fill(mutedTemplate, "count", n.ToString());
        }

        void Toast(string line)
        {
            if (!toastGroup) return;
            if (toastText) toastText.text = line;
            KitVisibility.SetShown(toastGroup, !string.IsNullOrEmpty(line));
            _toastUntil = Time.unscaledTime + toastSeconds;
        }
    }
}
