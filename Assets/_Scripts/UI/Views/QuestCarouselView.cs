using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Quests window's Episode 1 page in the picked look (Rev 77 "Quests", Docs/MENU_REDESIGN_B.md sections 150 and
    /// 155). The episode's name and its claimed count with a bar sit above a coverflow of the quests, each card its game's
    /// art, muted. The picked quest's line runs underneath, with its goal and one Lime button: Claim when it is done, Play
    /// quest when it is next (the game's configure, <see cref="ScreenSwitcher.OpenGame(GameModes)"/>), the Hangar for the
    /// Hangar quest. A locked or claimed quest shows its state instead of a button.
    ///
    /// <para>The quest graph is <see cref="QuestFrontier"/>'s and <see cref="GameModeProgressionService"/>'s: the same
    /// states the Profile's quest track (<see cref="QuestTrackView"/>) and Home's quest card read. The cards are
    /// <see cref="VoyageFlowCard"/>s, posed by <see cref="VoyageCardRules.FlowPose"/> as the Voyages tab's are. Every
    /// word is a serialized line here; Tools/Build/author_voyage_window.py lays the page out and authors them.</para>
    /// </summary>
    public class QuestCarouselView : MonoBehaviour
    {
        [Header("Quests")]
        [Tooltip("The episode's quests, in order (Episode 1: GameModeQuestList).")]
        [SerializeField] SO_UnlockList questList;
        [Tooltip("The text whose font the page's words take (the window's title).")]
        [SerializeField] TMP_Text fontFrom;

        [Header("Words")]
        [Tooltip("The episode's name over the cards. Empty shows nothing.")]
        [SerializeField] string episodeTitle = "The Hypersea";
        [Tooltip("The count beside the bar: {claimed} quests claimed of {total}.")]
        [SerializeField] string progressLine = "{claimed} of {total} claimed";
        [Tooltip("A card's name: {n} its number (01), {name} the quest's name.")]
        [SerializeField] string cardLine = "{n}  {name}";
        [Tooltip("The picked quest's line: {n}, {name}, and {state} one of the four state words below.")]
        [SerializeField] string focusLine = "{n}  {name} - {state}";
        [Tooltip("State word: the quest is claimed.")]
        [SerializeField] string claimedWord = "Claimed";
        [Tooltip("State word: the quest is done and waits to be claimed.")]
        [SerializeField] string doneWord = "Done - claim it";
        [Tooltip("State word: the quest is the next to play.")]
        [SerializeField] string openWord = "Next to play";
        [Tooltip("State word: the quest is locked. {previous} is the quest before it.")]
        [SerializeField] string lockedWord = "After {previous}";
        [Tooltip("Under a locked quest, where the button would be: {previous} is the quest before it.")]
        [SerializeField] string lockedLine = "Unlocks after {previous}";
        [Tooltip("The goal of a quest still being written (SO_UnlockData.IsPlaceholder).")]
        [SerializeField] string comingSoonLine = "Coming soon";
        [Tooltip("The button on a quest that is done.")]
        [SerializeField] string claimLabel = "Claim";
        [Tooltip("The button on the next quest to play (a game mode: its configure opens).")]
        [SerializeField] string playLabel = "Play quest";
        [Tooltip("The button on the next quest when it is a screen (the Hangar quest): the screen opens.")]
        [SerializeField] string goLabel = "Open the Hangar";

        [Header("Look")]
        [Tooltip("Card size, px.")]
        [SerializeField] Vector2 cardSize = new(420f, 400f);
        [Tooltip("Card art's multiply: the board's muted place look (Rev 74 C3).")]
        [SerializeField] Color artTint = new(0.55f, 0.56f, 0.62f, 1f);
        [Tooltip("A locked quest's art multiply.")]
        [SerializeField] Color lockedTint = new(0.28f, 0.29f, 0.34f, 1f);

        readonly List<VoyageFlowCard> _cards = new();
        RectTransform _flow, _barFill;
        TMP_Text _title, _progress, _focus, _goal, _wait, _ctaLabel;
        Button _cta;
        bool _built;
        int _picked = -1;
        // The pick follows the frontier until the player picks a card: progress loads after the window is built, so the
        // first frontier read can be quest 1 for a player with four claimed (seen 2026-11-09).
        bool _chosen;
        ModalWindowManager _window;
        ScreenSwitcher _switcher;
        GameModeProgressionService _service;

        void Awake() => Build();

        void OnEnable()
        {
            _window = GetComponentInParent<ModalWindowManager>(true);
            if (_window) _window.OnModalOpened += Opened;
            Subscribe();
            FollowFrontier();
        }

        void OnDisable()
        {
            if (_window) _window.OnModalOpened -= Opened;
            _window = null;
            if (_service != null) _service.OnProgressionChanged -= Changed;
            _service = null;
        }

        // The window hides by its canvas group, so an open is not an enable: the frontier is re-read on every open.
        void Opened()
        {
            Subscribe();
            FollowFrontier();
        }

        void FollowFrontier()
        {
            _chosen = false;
            _picked = -1;
            Refresh();
        }

        void Subscribe()
        {
            var service = GameModeProgressionService.Instance;
            if (service == _service) return;
            if (_service != null) _service.OnProgressionChanged -= Changed;
            _service = service;
            if (_service != null) _service.OnProgressionChanged += Changed;
        }

        void Changed(GameModeProgressionData _)
        {
            if (!_chosen) _picked = -1;
            Refresh();
        }

        int Count => questList && questList.Quests != null ? questList.Quests.Count : 0;

        QuestItemState StateOf(int i) => QuestFrontier.StateOf(questList, i, GameModeProgressionService.Instance);

        string Number(int i) => (i + 1).ToString("00");

        string NameOf(int i) => Count > i && i >= 0 && questList.Quests[i] ? questList.Quests[i].DisplayName : string.Empty;

        /// <summary>Redraw from the quest graph now; the picked card starts on the frontier.</summary>
        public void Refresh()
        {
            if (!_built) Build();
            if (isActiveAndEnabled) Subscribe();
            int count = Count;
            if (_picked < 0 || _picked >= count) _picked = QuestFrontier.FrontierIndex(questList, GameModeProgressionService.Instance);

            int claimed = 0;
            for (int i = 0; i < count; i++)
                if (StateOf(i) == QuestItemState.Claimed) claimed++;
            _title.text = episodeTitle ?? string.Empty;
            _progress.text = TemplateText.Fill(progressLine, "claimed", claimed.ToString(), "total", count.ToString());
            _barFill.anchorMax = new Vector2(count > 0 ? (float)claimed / count : 0f, 1f);

            while (_cards.Count < count) _cards.Add(MakeCard(_cards.Count));
            for (int i = 0; i < count; i++)
            {
                var c = _cards[i];
                c.Name.text = TemplateText.Fill(cardLine, "n", Number(i), "name", NameOf(i));
                Art(c.Art, questList.Quests[i]);
                c.Art.color = StateOf(i) == QuestItemState.Locked ? lockedTint : artTint;
            }
            FlowUi.Stand(_cards, count, _picked, cardSize.x * 0.8f);

            DrawFocus();
        }

        void DrawFocus()
        {
            if (_picked < 0 || _picked >= Count)
            {
                _focus.text = _goal.text = _wait.text = string.Empty;
                _cta.gameObject.SetActive(false);
                return;
            }
            var quest = questList.Quests[_picked];
            var state = StateOf(_picked);
            string previous = NameOf(_picked - 1);
            string word = state switch
            {
                QuestItemState.Claimed => claimedWord,
                QuestItemState.ReadyToClaim => doneWord,
                QuestItemState.Unlocked => openWord,
                _ => TemplateText.Fill(lockedWord, "previous", previous),
            };
            _focus.text = TemplateText.Fill(focusLine, "n", Number(_picked), "name", NameOf(_picked), "state", word);
            _goal.text = quest && quest.IsPlaceholder ? comingSoonLine ?? string.Empty : quest ? quest.Description ?? string.Empty : string.Empty;

            bool claim = state == QuestItemState.ReadyToClaim;
            bool play = state == QuestItemState.Unlocked && CanGo(quest);
            _cta.gameObject.SetActive(claim || play);
            _ctaLabel.text = (claim ? claimLabel : quest && quest.FeatureKind == FeatureKind.GameMode ? playLabel : goLabel) ?? string.Empty;
            _wait.text = claim || play ? string.Empty
                : state == QuestItemState.Claimed ? claimedWord ?? string.Empty
                : state == QuestItemState.Locked ? TemplateText.Fill(lockedLine, "previous", previous)
                : string.Empty;
        }

        static bool CanGo(SO_UnlockData quest) =>
            quest && (quest.FeatureKind == FeatureKind.GameMode || (quest.FeatureKind == FeatureKind.Screen && quest.FeatureRef == "Hangar"));

        void Pick(int index)
        {
            if (index == _picked || index < 0 || index >= Count) return;
            _chosen = true;
            _picked = index;
            Refresh();
        }

        void Act()
        {
            if (_picked < 0 || _picked >= Count) return;
            var quest = questList.Quests[_picked];
            if (!quest) return;
            switch (StateOf(_picked))
            {
                case QuestItemState.ReadyToClaim:
                    // The service raises OnProgressionChanged, which redraws; the pick moves on to the next quest.
                    GameModeProgressionService.Instance?.ClaimQuestAndUnlockNext(quest.GameMode);
                    FollowFrontier();
                    break;
                case QuestItemState.Unlocked:
                    if (!_switcher) _switcher = FindFirstObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
                    if (!_switcher) return;
                    if (quest.FeatureKind == FeatureKind.GameMode) _switcher.OpenGame(quest.GameMode);
                    else if (quest.FeatureKind == FeatureKind.Screen && quest.FeatureRef == "Hangar")
                    {
                        if (_window) _window.ModalWindowOut();
                        _switcher.OnClickHangarNav();
                    }
                    break;
            }
        }

        // ── The card art: the quest's game card (the Arcade's own roster), else the quest's icon ──────────────────────

        static readonly Dictionary<GameModes, Sprite> s_Art = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Art.Clear();

        static Sprite ArtOf(SO_UnlockData quest)
        {
            if (!quest) return null;
            if (quest.FeatureKind != FeatureKind.GameMode) return quest.Icon;
            if (s_Art.TryGetValue(quest.GameMode, out var cached) && cached) return cached;
            foreach (var explore in FindObjectsByType<ArcadeExploreView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var game = explore.FindGameByMode(quest.GameMode);
                if (game && game.CardBackground)
                {
                    s_Art[quest.GameMode] = game.CardBackground;
                    return game.CardBackground;
                }
            }
            return quest.Icon;
        }

        void Art(RawImage img, SO_UnlockData quest) => FlowUi.Art(img, ArtOf(quest), cardSize.x / Mathf.Max(1f, cardSize.y));

        // ── Building (once) ──────────────────────────────────────────────────────────────────────────────────────────

        void Build()
        {
            if (_built) return;
            _built = true;
            var root = (RectTransform)transform;

            // The head: the episode's name on the left, the claimed count and its bar on the right.
            var head = FlowUi.Node("Head", root);
            FlowUi.Anchor(head, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), new Vector2(0f, 0f));
            _title = Text(head, 48, FlowUi.Light, TextAlignmentOptions.BottomLeft);
            FlowUi.Anchor(_title.rectTransform, Vector2.zero, new Vector2(0.6f, 1f), Vector2.zero, Vector2.zero);
            var bar = FlowUi.Node("Bar", head);
            FlowUi.Anchor(bar, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-320f, 10f), new Vector2(0f, 18f));
            FlowUi.Fill(bar, FlowUi.Track);
            _barFill = FlowUi.Node("Fill", bar);
            FlowUi.Anchor(_barFill, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            FlowUi.Fill(_barFill, FlowUi.Light);
            _progress = Text(head, 20, FlowUi.Light, TextAlignmentOptions.BottomRight);
            FlowUi.Anchor(_progress.rectTransform, new Vector2(0.6f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-340f, 0f));

            // The coverflow.
            _flow = FlowUi.Node("Flow", root);
            FlowUi.Anchor(_flow, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 200f), new Vector2(0f, -76f));
            _flow.gameObject.AddComponent<RectMask2D>();

            // The picked quest's line: its state, its goal and the one call to action.
            var focus = FlowUi.Node("Focus", root);
            FlowUi.Anchor(focus, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 180f));
            FlowUi.Fill(focus, FlowUi.Plate).raycastTarget = false;
            var fe = focus.gameObject.AddComponent<Outline>();
            fe.effectColor = FlowUi.Line;
            fe.effectDistance = new Vector2(1f, -1f);
            _focus = Text(focus, 28, FlowUi.Light, TextAlignmentOptions.BottomLeft);
            FlowUi.Anchor(_focus.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(36f, 0f), new Vector2(-360f, -28f));
            _goal = Text(focus, 18, FlowUi.Light, TextAlignmentOptions.TopLeft);
            _goal.textWrappingMode = TextWrappingModes.Normal;
            FlowUi.Anchor(_goal.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(36f, 28f), new Vector2(-360f, -10f));
            _wait = Text(focus, 18, FlowUi.Light, TextAlignmentOptions.Right);
            FlowUi.Anchor(_wait.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-340f, 0f), new Vector2(-36f, 0f));

            var cta = FlowUi.Node("Act", focus);
            FlowUi.Anchor(cta, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-316f, -32f), new Vector2(-36f, 32f));
            var ctaFill = FlowUi.Fill(cta, FlowUi.Lime);
            _cta = cta.gameObject.AddComponent<Button>();
            _cta.targetGraphic = ctaFill;
            _cta.onClick.AddListener(Act);
            _ctaLabel = Text(cta, 20, FlowUi.OnLime, TextAlignmentOptions.Center);
            FlowUi.Anchor(_ctaLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        VoyageFlowCard MakeCard(int index) => FlowUi.Card(_flow, index, cardSize, fontFrom, Pick);

        TMP_Text Text(Transform parent, float size, Color color, TextAlignmentOptions align) =>
            FlowUi.Text(parent, fontFrom, size, color, align);
    }
}
