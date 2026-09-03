using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The weekly challenge leaderboard WINDOW: the countdown, the three scope tabs, the reward
    /// tooltip and the close button.
    ///
    /// <para><b>The modal owns the decisions; the panel owns the rows.</b> Everything about which
    /// rows exist, what they say and how they animate in lives in
    /// <see cref="WeeklyChallengeLeaderboardPanel"/>; this class decides WHICH population is being
    /// shown and drives the chrome around it. The same split the arcade launch panel records — and
    /// the reason a tab press here is one call rather than a rebuild.</para>
    ///
    /// <para><b>Every reference is optional.</b> A modal with only a close button still opens and
    /// closes; one with only the panel still lists the week. Nothing here logs about a field that
    /// was left empty, because "this window does not have that piece" is a legitimate layout, not
    /// a misconfiguration.</para>
    /// </summary>
    /// <remarks>
    /// <b>Scopes and what each one needs.</b>
    /// <list type="bullet">
    /// <item><b>World</b> — always available once a board id is authored on the catalog.</item>
    /// <item><b>Regional</b> — needs a row in <c>WeeklyChallengeCatalogSO.regionalLeaderboards</c>
    /// matching this player's region. UGS has no region concept, so a region is its OWN board; see
    /// <see cref="WeeklyChallengeRegion"/> for how the key is resolved and why filtering the world
    /// page client-side is not an equivalent.</item>
    /// <item><b>Friends</b> — needs the Friends service initialised. It ranks friends against each
    /// other on the world board, so a friend's time is the same time it is everywhere else.</item>
    /// </list>
    /// A tab whose scope has nothing configured is DIMMED AND NON-INTERACTABLE rather than hidden:
    /// a tab that vanishes changes the row's layout every time the answer changes, and a player
    /// who saw three tabs yesterday reads two as a broken build.
    /// </remarks>
    public class WeeklyChallengeLeaderboardModal : ModalWindowManager
    {
        [Inject] FriendsDataSO friendsData;

        [Header("Rows")]
        [Tooltip("The row list. Left empty, one is looked for in this modal's own children.")]
        [SerializeField] WeeklyChallengeLeaderboardPanel panel;

        [Header("Header")]
        [Tooltip("'Time Left 12:28:36' — counts down to the next UTC Monday, in hours:minutes:seconds.")]
        [SerializeField] TMP_Text timeLeftText;

        [SerializeField] string timeLeftPrefix = "Time Left ";

        [Tooltip("Optional: the week's mode, e.g. 'SCURRY'.")]
        [SerializeField] TMP_Text challengeTitleText;

        [Header("Scope tabs (all optional)")]
        [SerializeField] Button worldTab;
        [SerializeField] Button regionalTab;
        [SerializeField] Button friendsTab;

        [Tooltip("Which tab is selected when the window opens. Falls back to World when the " +
                 "chosen one has nothing configured.")]
        [SerializeField] LeaderboardScope defaultScope = LeaderboardScope.World;

        [Tooltip("Off keeps the Friends tab dimmed and unpressable even when the friends service " +
                 "IS available - the switch for shipping the board before the friends flow is " +
                 "ready, rather than deleting the tab and re-adding it later.")]
        [SerializeField] bool friendsTabEnabled;

        [Header("Tab look")]
        [Tooltip("The selected tab's background colour, alpha included.")]
        [SerializeField] Color activeTabColor = new(0.15f, 0.85f, 0.95f, 1f);

        [Tooltip("An available but unselected tab.")]
        [SerializeField] Color inactiveTabColor = new(1f, 1f, 1f, 0.35f);

        [Tooltip("A tab whose scope has no board configured. Distinct from merely unselected: " +
                 "one is a choice you have not made, the other is a choice you do not have.")]
        [SerializeField] Color unavailableTabColor = new(1f, 1f, 1f, 0.12f);

        [SerializeField] Color activeTabLabelColor = Color.black;
        [SerializeField] Color inactiveTabLabelColor = Color.white;

        [Tooltip("How long a tab takes to cross-fade between states. 0 snaps.")]
        [SerializeField, Range(0f, 0.4f)] float tabFadeDuration = 0.15f;

        [Tooltip("The selected tab swells by this much. 1 disables the pop.")]
        [SerializeField, Range(1f, 1.2f)] float activeTabScale = 1.05f;

        [Header("Rank reward")]
        [Tooltip("Opens the reward tooltip. Optional.")]
        [SerializeField] Button rankRewardButton;

        [Tooltip("The tooltip itself. Starts hidden; a click anywhere on its backdrop closes it.")]
        [SerializeField] GameObject rankRewardPanel;

        [Tooltip("The tooltip's own full-bleed backdrop. Clicking it closes the tooltip - so it " +
                 "MUST be a raycast target, and it is the one piece this modal will add a Button " +
                 "to itself if the art did not author one.")]
        [SerializeField] RectTransform rankRewardBackdrop;

        [SerializeField, Range(0f, 0.5f)] float rewardFadeDuration = 0.18f;

        [Header("Close")]
        [SerializeField] Button closeButton;

        [Header("Window animation")]
        [Tooltip("The window's own content root, scaled and faded on open. Left empty, the first " +
                 "child named 'Content' is used, else this transform.")]
        [SerializeField] RectTransform contentRoot;

        [Tooltip("0 disables the open flourish and leaves the Animator (if any) to it.")]
        [SerializeField, Range(0f, 0.6f)] float openDuration = 0.25f;

        [SerializeField, Range(0.5f, 1f)] float openStartScale = 0.92f;

        // ── State ──────────────────────────────────────────────────────────────

        readonly Dictionary<LeaderboardScope, Button> _tabs = new();
        CanvasGroup _rewardGroup;
        LeaderboardScope _scope = LeaderboardScope.World;
        float _countdownAccumulator;
        bool _wired;

        protected override void Start()
        {
            base.Start();
            Wire();
        }

        void OnEnable()
        {
            // The modal may be opened before Start has run (a modal authored inactive opens with
            // SetActive + ModalWindowIn in the same call), so wiring is idempotent and happens at
            // whichever of the two comes first.
            Wire();

            PublishFriendSource();
            CloseRewardPanel(instant: true);
            RedrawHeader();
            SelectScope(ResolveOpeningScope(), force: true);
            PlayOpenAnimation();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // A killed tween leaves its target mid-flight, so the content root is snapped back to
            // rest - otherwise a modal closed 40 ms into its own open animation re-opens at 0.94
            // scale and half alpha, permanently.
            if (contentRoot)
            {
                DOTween.Kill(contentRoot, complete: false);
                contentRoot.localScale = Vector3.one;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;

            // 1 Hz: the countdown shows whole seconds, so anything faster is work nobody sees.
            _countdownAccumulator += Time.unscaledDeltaTime;
            if (_countdownAccumulator < 1f) return;
            _countdownAccumulator = 0f;
            RedrawHeader();
        }

        // ── Wiring ─────────────────────────────────────────────────────────────

        void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (!panel) panel = GetComponentInChildren<WeeklyChallengeLeaderboardPanel>(true);

            // This modal decides WHEN to fetch (it owns the tabs), so the panel must not also
            // fetch on enable - the window would otherwise cost two round trips on every open.
            if (panel) panel.DrivenExternally = true;
            if (!contentRoot) contentRoot = ResolveContentRoot();

            RegisterTab(LeaderboardScope.World, worldTab);
            RegisterTab(LeaderboardScope.Regional, regionalTab);
            RegisterTab(LeaderboardScope.Friends, friendsTab);

            if (closeButton) closeButton.onClick.AddListener(ModalWindowOut);
            if (rankRewardButton) rankRewardButton.onClick.AddListener(OpenRewardPanel);

            WireRewardBackdrop();
        }

        void RegisterTab(LeaderboardScope scope, Button button)
        {
            if (!button) return;
            _tabs[scope] = button;
            button.onClick.AddListener(() => SelectScope(scope));
        }

        /// <summary>
        /// The reward tooltip closes on a click anywhere over its backdrop. That needs a raycast
        /// target and a click handler, and the art authors neither — so both are ADDED here rather
        /// than asked of the layout, because "the panel would not close" is a bug nobody can see
        /// in the hierarchy.
        /// </summary>
        void WireRewardBackdrop()
        {
            if (!rankRewardBackdrop) return;

            var image = rankRewardBackdrop.GetComponent<Image>();
            if (image) image.raycastTarget = true;

            var button = rankRewardBackdrop.GetComponent<Button>();
            if (!button)
            {
                button = rankRewardBackdrop.gameObject.AddComponent<Button>();
                // The backdrop is artwork, not a control: a colour tint on press would flash the
                // whole tooltip. It still needs to be a Button so it takes the click.
                button.transition = Selectable.Transition.None;
            }
            button.onClick.AddListener(() => CloseRewardPanel());
        }

        /// <summary>
        /// Hand the leaderboard service its friend ids. The service is a hidden runtime-created
        /// object with no injection of its own, so the ONE thing it cannot reach — the
        /// DI-registered friends data — is published to it by whoever can.
        /// </summary>
        void PublishFriendSource()
        {
            if (friendsData == null) return;

            WeeklyChallengeService.FriendIdSource = () =>
            {
                if (friendsData == null || !friendsData.IsInitialized || friendsData.Friends == null)
                    return null;   // "cannot ask" - NOT the same as an empty list

                var ids = new List<string>(friendsData.Friends.Count);
                foreach (var friend in friendsData.Friends)
                    if (!string.IsNullOrWhiteSpace(friend.PlayerId))
                        ids.Add(friend.PlayerId);

                return ids;
            };
        }

        RectTransform ResolveContentRoot()
        {
            var content = transform.Find("Content") as RectTransform;
            return content ? content : transform as RectTransform;
        }

        // ── Scopes ─────────────────────────────────────────────────────────────

        LeaderboardScope ResolveOpeningScope() =>
            IsScopeUsable(defaultScope) ? defaultScope : LeaderboardScope.World;

        /// <summary>
        /// Usable = the service says it has a board AND, for Friends, the local switch is on. The
        /// switch is separate from availability on purpose: "we cannot ask" and "we are not
        /// shipping this yet" are different facts and only one of them changes at runtime.
        /// </summary>
        bool IsScopeUsable(LeaderboardScope scope)
        {
            if (scope == LeaderboardScope.Friends && !friendsTabEnabled) return false;

            var service = WeeklyChallengeService.Instance;
            return service != null && service.Leaderboard.IsScopeAvailable(scope);
        }

        public void SelectScope(LeaderboardScope scope, bool force = false)
        {
            if (!force && scope == _scope) return;
            if (!IsScopeUsable(scope)) return;

            _scope = scope;
            RedrawTabs();
            if (panel) panel.SetScope(scope, forceRefresh: force);
        }

        // Inspector-friendly wrappers, so a tab can also be wired straight to onClick in the scene
        // without the modal having to be the one that registered it.
        public void SelectWorld() => SelectScope(LeaderboardScope.World);
        public void SelectRegional() => SelectScope(LeaderboardScope.Regional);
        public void SelectFriends() => SelectScope(LeaderboardScope.Friends);

        void RedrawTabs()
        {
            foreach (var pair in _tabs)
                RedrawTab(pair.Key, pair.Value);
        }

        void RedrawTab(LeaderboardScope scope, Button button)
        {
            if (!button) return;

            bool usable = IsScopeUsable(scope);
            bool selected = usable && scope == _scope;

            button.interactable = usable && !selected;   // pressing the open tab does nothing

            var target = !usable ? unavailableTabColor
                       : selected ? activeTabColor
                       : inactiveTabColor;

            var labelTarget = selected ? activeTabLabelColor : inactiveTabLabelColor;
            if (!usable) labelTarget.a *= 0.4f;

            var background = button.targetGraphic ? button.targetGraphic : button.GetComponent<Graphic>();
            FadeGraphic(background, target);

            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                FadeGraphic(label, labelTarget);

            var rect = button.transform as RectTransform;
            if (rect)
            {
                float scale = selected ? activeTabScale : 1f;
                DOTween.Kill(rect, complete: false);
                if (tabFadeDuration <= 0f)
                    rect.localScale = Vector3.one * scale;
                else
                    rect.DOScale(scale, tabFadeDuration)
                        .SetEase(Ease.OutBack).SetUpdate(true).SetLink(rect.gameObject);
            }
        }

        void FadeGraphic(Graphic graphic, Color target)
        {
            if (!graphic) return;

            DOTween.Kill(graphic, complete: false);
            if (tabFadeDuration <= 0f)
            {
                graphic.color = target;
                return;
            }

            graphic.DOColor(target, tabFadeDuration)
                .SetEase(Ease.OutQuad).SetUpdate(true).SetLink(graphic.gameObject);
        }

        // ── Header ─────────────────────────────────────────────────────────────

        void RedrawHeader()
        {
            var service = WeeklyChallengeService.Instance;

            if (challengeTitleText)
            {
                var challenge = service != null ? service.ThisWeek : default;
                challengeTitleText.text = challenge.IsValid
                    ? challenge.GameMode.ToString().ToUpperInvariant()
                    : "WEEKLY CHALLENGE";
            }

            if (timeLeftText)
            {
                timeLeftText.text = service != null
                    ? timeLeftPrefix + FormatHoursMinutesSeconds(service.TimeUntilNextChallenge)
                    : string.Empty;
            }
        }

        /// <summary>
        /// <c>HH:MM:SS</c>, with hours running past 24 rather than rolling over.
        ///
        /// <para>A week is up to <b>168 hours</b>, so this reads <c>167:59:59</c> at the top of a
        /// week — deliberately, because the alternative is <c>6:23:59:59</c> or a silent rollover
        /// to <c>23:59:59</c>, and a countdown that lies about the day is worse than one with a
        /// three-digit hour. Hours are padded to two so the string never changes WIDTH within an
        /// hour, which is what stops the label jittering under a proportional font.</para>
        ///
        /// <para>Deliberately NOT <c>WeeklyChallengeCard.FormatCountdown</c>: that one switches
        /// units as the week runs down (<c>6d 3h</c> → <c>7:12:33</c> → <c>1:04</c>) because it is
        /// glanced at on a card. This is a clock and stays a clock.</para>
        /// </summary>
        public static string FormatHoursMinutesSeconds(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            return $"{(int)span.TotalHours:D2}:{span.Minutes:D2}:{span.Seconds:D2}";
        }

        // ── Rank reward tooltip ────────────────────────────────────────────────

        public void OpenRewardPanel()
        {
            if (!rankRewardPanel) return;

            rankRewardPanel.SetActive(true);

            var group = EnsureRewardGroup();
            if (!group) return;

            DOTween.Kill(group, complete: false);
            if (rewardFadeDuration <= 0f)
            {
                group.alpha = 1f;
                return;
            }

            group.alpha = 0f;
            group.DOFade(1f, rewardFadeDuration)
                .SetEase(Ease.OutQuad).SetUpdate(true).SetLink(rankRewardPanel);
        }

        public void CloseRewardPanel() => CloseRewardPanel(false);

        /// <summary>
        /// <paramref name="instant"/> is the OPEN path: the panel has to be hidden before the
        /// window is on screen, and a fade there would show the tooltip for a frame every time the
        /// leaderboard opens.
        /// </summary>
        public void CloseRewardPanel(bool instant)
        {
            if (!rankRewardPanel) return;

            var group = EnsureRewardGroup();

            if (instant || rewardFadeDuration <= 0f || !group)
            {
                if (group)
                {
                    DOTween.Kill(group, complete: false);
                    group.alpha = 0f;
                }
                rankRewardPanel.SetActive(false);
                return;
            }

            DOTween.Kill(group, complete: false);
            group.DOFade(0f, rewardFadeDuration)
                .SetEase(Ease.InQuad).SetUpdate(true).SetLink(rankRewardPanel)
                // Deactivated at the END, not at the call: deactivating first kills the tween's
                // own target and the fade never runs.
                .OnComplete(() => { if (rankRewardPanel) rankRewardPanel.SetActive(false); });
        }

        CanvasGroup EnsureRewardGroup()
        {
            if (_rewardGroup) return _rewardGroup;
            if (!rankRewardPanel) return null;

            _rewardGroup = rankRewardPanel.GetComponent<CanvasGroup>()
                        ?? rankRewardPanel.AddComponent<CanvasGroup>();
            return _rewardGroup;
        }

        // ── Window animation ───────────────────────────────────────────────────

        /// <summary>
        /// A short scale-up on the content root, on top of whatever the Animator does. It touches
        /// SCALE only and never alpha: the base class drives the modal's CanvasGroup, and a second
        /// writer to that alpha is how a modal ends up invisible after a fast open-close-open.
        /// </summary>
        void PlayOpenAnimation()
        {
            if (!contentRoot || openDuration <= 0f) return;

            DOTween.Kill(contentRoot, complete: false);
            contentRoot.localScale = Vector3.one * openStartScale;
            contentRoot.DOScale(1f, openDuration)
                .SetEase(Ease.OutBack).SetUpdate(true).SetLink(contentRoot.gameObject);
        }

        // ── Public entry point ─────────────────────────────────────────────────

        /// <summary>Open the leaderboard. Wire this to whatever button offers it.</summary>
        public void Open()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            ModalWindowIn();
        }
    }
}
