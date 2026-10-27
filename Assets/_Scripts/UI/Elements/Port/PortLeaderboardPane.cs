using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Port's WEEKLY LEADERBOARD tab (Docs/MENU_REDESIGN_B.md section 90; Shombith 2026-10-27: "The weekly
    /// leaderboard can also go to the port for now"). It is the weekly challenge window's own board (the
    /// LeaderboardView prefab: World / Regional / Friends, podium, rows, your best run, rewards) drawn as a tab, with
    /// no BACK. The window behind the configure card's LEADERBOARD button is unchanged; this pane drives the same
    /// <see cref="WeeklyChallengeLeaderboardPanel"/> the way <see cref="WeeklyChallengeLeaderboardModal"/> does, minus
    /// the window.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Port Leaderboard Pane")]
    public class PortLeaderboardPane : MonoBehaviour
    {
        [Inject] FriendsDataSO friendsData;

        [SerializeField] private WeeklyChallengeLeaderboardPanel panel;
        [Tooltip("The scope tabs: World, Regional, Friends.")]
        [SerializeField] private KitTile worldTab;
        [SerializeField] private KitTile regionalTab;
        [SerializeField] private KitTile friendsTab;
        [Tooltip("Friends boards are not open yet; the tab stays drawn, dimmed.")]
        [SerializeField] private bool friendsTabEnabled;

        [Header("Header")]
        [Tooltip("This week's challenge, by its card's name.")]
        [SerializeField] private TMP_Text titleText;
        [Tooltip("Shown while no challenge is set this week.")]
        [SerializeField] private string titleFallback = "Weekly challenge";
        [SerializeField] private TMP_Text countdownText;
        [Tooltip("The cards, for this week's mode's name.")]
        [SerializeField] private SO_GameList games;

        readonly Dictionary<LeaderboardScope, KitTile> _tabs = new();
        LeaderboardScope _scope = LeaderboardScope.World;
        bool _shown;
        float _clock;

        void Awake()
        {
            if (panel) panel.DrivenExternally = true;
            Register(LeaderboardScope.World, worldTab);
            Register(LeaderboardScope.Regional, regionalTab);
            Register(LeaderboardScope.Friends, friendsTab);
        }

        void Register(LeaderboardScope scope, KitTile tile)
        {
            if (!tile) return;
            _tabs[scope] = tile;
            tile.onClick.AddListener(() => Select(scope, false));
        }

        /// <summary>The Port shows or hides this tab. Each show fetches the board fresh.</summary>
        public void SetShown(bool shown)
        {
            _shown = shown;
            if (!shown) return;
            PublishFriendSource();
            RedrawHeader();
            Select(Usable(_scope) ? _scope : LeaderboardScope.World, true);
        }

        void Update()
        {
            if (!_shown) return;
            // 1 Hz: the countdown shows whole seconds.
            _clock += Time.unscaledDeltaTime;
            if (_clock < 1f) return;
            _clock = 0f;
            RedrawHeader();
        }

        void Select(LeaderboardScope scope, bool force)
        {
            if (!Usable(scope) || (!force && scope == _scope)) return;
            _scope = scope;
            foreach (var pair in _tabs)
            {
                bool usable = Usable(pair.Key);
                pair.Value.interactable = usable;
                pair.Value.IsSelected = usable && pair.Key == _scope;
                pair.Value.IsLocked = !usable;
            }
            if (panel) panel.SetScope(scope, forceRefresh: force);
        }

        bool Usable(LeaderboardScope scope)
        {
            if (scope == LeaderboardScope.Friends && !friendsTabEnabled) return false;
            var service = WeeklyChallengeService.Instance;
            return service != null && service.Leaderboard.IsScopeAvailable(scope);
        }

        void RedrawHeader()
        {
            var service = WeeklyChallengeService.Instance;
            if (titleText)
            {
                var challenge = service != null ? service.ThisWeek : default;
                titleText.text = challenge.IsValid ? CardName(challenge.GameMode) ?? titleFallback : titleFallback;
            }
            if (countdownText)
                countdownText.text = service != null
                    ? WeeklyChallengeLeaderboardModal.FormatDaysCountdown(service.TimeUntilNextChallenge)
                    : string.Empty;
        }

        string CardName(GameModes mode)
        {
            if (!games || games.Games == null) return null;
            foreach (var game in games.Games)
                if (game && game.Mode == mode && !string.IsNullOrEmpty(game.DisplayName))
                    return game.DisplayName;
            return null;
        }

        /// <summary>The leaderboard service's friend ids, published the way the weekly window does it.</summary>
        void PublishFriendSource()
        {
            if (friendsData == null) return;
            WeeklyChallengeService.FriendIdSource = () =>
            {
                if (friendsData == null || !friendsData.IsInitialized || friendsData.Friends == null) return null;
                var ids = new List<string>(friendsData.Friends.Count);
                foreach (var friend in friendsData.Friends)
                    if (!string.IsNullOrWhiteSpace(friend.PlayerId)) ids.Add(friend.PlayerId);
                return ids;
            };
        }
    }
}
