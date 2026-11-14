using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Quests tab as the Sea table (Rev 90, Docs/MENU_REDESIGN_B.md sections 171-172): the episode's quests as chips,
    /// the HyperSea on the table (the episode is "The Hypersea"), the quests as a track on the front face (a claimed
    /// quest filled, one to claim ringed in the selection colour, a locked one dim, the picked one underlined), and on
    /// the right the picked quest (its picture, name, goal and state) over the screen's one button: Claim, or Play quest.
    ///
    /// <para>The quests and their states are the page's own quest track's (<see cref="QuestTrackView.QuestList"/>,
    /// <see cref="QuestFrontier"/>, <see cref="GameModeProgressionService"/>): the table reads them and changes no rule.
    /// It opens on the frontier quest.</para>
    /// </summary>
    public class VoyageQuestSlab : VoyageSlabPage
    {
        QuestTrackView _source;
        readonly List<Chip> _chips = new();
        readonly List<Station> _stations = new();
        TMP_Text _kicker, _questName, _goal, _state, _ctaWords, _waitWords;
        Image _art;
        Button _cta;
        GameObject _wait;
        int _picked = -1;
        GameModeProgressionService _heard;

        SO_UnlockList Quests => _source ? _source.QuestList : null;

        protected override void OnEnable()
        {
            base.OnEnable();
            _heard = GameModeProgressionService.Instance;
            if (_heard != null) _heard.OnProgressionChanged += Changed;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_heard != null) _heard.OnProgressionChanged -= Changed;
            _heard = null;
        }

        void Changed(GameModeProgressionData _) => Redraw();

        protected override void BuildPanel()
        {
            _source = GetComponentInChildren<QuestTrackView>(true);
            var column = VoyageKit.Node("Quest", Panel);
            VoyageKit.Stretch(column);
            column.offsetMin = new Vector2(0f, 72f);
            VoyageKit.Column(column, 0f);
            _kicker = Kit.Label(column, 13, Kit.Sub);
            var gap = VoyageKit.Node("Gap", column);
            VoyageKit.Le(gap).preferredHeight = 14f;
            var art = VoyageKit.Node("Picture", column);
            VoyageKit.Le(art).preferredHeight = 299f;
            VoyageKit.Sliced(art, Kit.Style.Sliver10Fill, Kit.Popup);
            var picture = VoyageKit.Node("Art", art);
            VoyageKit.Stretch(picture);
            picture.offsetMin = new Vector2(12f, 12f);
            picture.offsetMax = new Vector2(-12f, -12f);
            _art = VoyageKit.Picture(picture);
            var gap2 = VoyageKit.Node("Gap", column);
            VoyageKit.Le(gap2).preferredHeight = 20f;
            _questName = Kit.Text(column, 36, Kit.Light, TextAlignmentOptions.Left);
            var gap3 = VoyageKit.Node("Gap", column);
            VoyageKit.Le(gap3).preferredHeight = 10f;
            _goal = Kit.Text(column, 18, Kit.Light, TextAlignmentOptions.TopLeft);
            _goal.textWrappingMode = TextWrappingModes.Normal;
            var gap4 = VoyageKit.Node("Gap", column);
            VoyageKit.Le(gap4).preferredHeight = 14f;
            _state = Kit.Label(column, 15, Kit.Light);

            // The foot: the one button (Claim or Play quest), or the quest's state where there is nothing to press.
            var foot = VoyageKit.Node("Foot", Panel);
            VoyageKit.Foot(foot, 0f, 56f);
            (_cta, _ctaWords) = Kit.Cta(foot, Lines?.BoardSailKey, null, Press);
            var wait = VoyageKit.Node("State", Panel);
            VoyageKit.Foot(wait, 0f, 56f);
            VoyageKit.Sliced(wait, Kit.Style.Sliver10Line, Kit.Outline);
            _waitWords = Kit.Label(wait, 18, Kit.Sub, TextAlignmentOptions.Center);
            VoyageKit.Stretch(_waitWords.rectTransform);
            _wait = wait.gameObject;
        }

        QuestItemState StateOf(int index) => QuestFrontier.StateOf(Quests, index, GameModeProgressionService.Instance);

        string StateLine(int index, QuestItemState state) => state switch
        {
            QuestItemState.Claimed => Lines?.SlabQuestClaimed,
            QuestItemState.ReadyToClaim => Lines?.SlabQuestReady,
            QuestItemState.Unlocked => Lines?.SlabQuestNext,
            _ => TemplateText.Fill(Lines?.SlabQuestLocked, "name",
                index > 0 && Quests.Quests[index - 1] ? Quests.Quests[index - 1].DisplayName : null),
        };

        protected override void Draw()
        {
            var list = Quests;
            int count = list && list.Quests != null ? list.Quests.Count : 0;
            Picture.Zoom = 1.9f;
            Picture.Show(VoyageGalaxyArt.Texture(), new Rect(0f, 0f, 1f, 1f), VoyageGalaxyArt.Aspect);
            if (count == 0)
            {
                ShowChips(null, -1, null);
                ShowTrack(null);
                ShowName(Lines?.SlabQuestsKicker, Lines?.SlabQuestsName, null, null);
                ShowQuest(-1, QuestItemState.Locked);
                return;
            }
            if (_picked < 0 || _picked >= count) _picked = Mathf.Max(0, QuestFrontier.FrontierIndex(list, GameModeProgressionService.Instance));

            _chips.Clear();
            _stations.Clear();
            int claimed = 0;
            for (int i = 0; i < count; i++)
            {
                var quest = list.Quests[i];
                var state = StateOf(i);
                if (state == QuestItemState.Claimed) claimed++;
                string n = (i + 1).ToString("00");
                string line = StateLine(i, state);
                bool locked = state == QuestItemState.Locked;
                _chips.Add(new Chip
                {
                    Title = TemplateText.Fill(Lines?.SlabChip, "n", n, "name", quest ? quest.DisplayName : null),
                    Meta = line,
                    Art = quest ? quest.Icon : null,
                    Dim = locked,
                });
                _stations.Add(new Station
                {
                    Key = n,
                    Name = quest ? quest.DisplayName : null,
                    Note = line,
                    Ring = state == QuestItemState.ReadyToClaim ? Kit.Cyan : locked ? Kit.Outline : Kit.Light,
                    Fill = state == QuestItemState.Claimed ? Kit.Light : VoyageKit.Clear,
                    Dim = locked,
                    Picked = i == _picked,
                });
            }
            ShowChips(_chips, _picked, Pick);
            ShowTrack(_stations);
            ShowName(Lines?.SlabQuestsKicker, Lines?.SlabQuestsName, null,
                TemplateText.Fill(Lines?.SlabQuestsProgress, "claimed", claimed.ToString("0"), "total", count.ToString("0")));
            ShowQuest(_picked, StateOf(_picked));
        }

        void ShowQuest(int index, QuestItemState state)
        {
            var list = Quests;
            var quest = index >= 0 && list ? list.Quests[index] : null;
            VoyageKit.Set(_kicker, quest ? TemplateText.Fill(Lines?.SlabQuestKicker, "n", (index + 1).ToString("00"),
                "count", list.Quests.Count.ToString("00")) : null);
            VoyageKit.Show(_art, quest ? quest.Icon : null);
            VoyageKit.Set(_questName, quest ? quest.DisplayName : null);
            VoyageKit.Set(_goal, quest ? quest.Description : null);
            string line = quest ? StateLine(index, state) : null;
            VoyageKit.Set(_state, line);
            bool claim = quest && state == QuestItemState.ReadyToClaim, play = quest && state == QuestItemState.Unlocked;
            _cta.gameObject.SetActive(claim || play);
            _wait.SetActive(quest && !claim && !play);
            VoyageKit.Set(_ctaWords, claim ? Lines?.SlabQuestClaim : Lines?.SlabQuestPlay);
            VoyageKit.Set(_waitWords, line);
        }

        void Pick(int index)
        {
            if (index == _picked) return;
            _picked = index;
            Redraw();
        }

        /// <summary>The one button: claim a quest that is done (the next opens and is picked), or go and play the next.</summary>
        void Press()
        {
            var list = Quests;
            if (!list || _picked < 0 || _picked >= list.Quests.Count) return;
            var quest = list.Quests[_picked];
            switch (StateOf(_picked))
            {
                case QuestItemState.ReadyToClaim:
                    GameModeProgressionService.Instance?.ClaimQuestAndUnlockNext(quest.GameMode);
                    _picked = -1;
                    Redraw();
                    break;
                case QuestItemState.Unlocked:
                    if (_source) _source.OpenArcadeForQuest();
                    break;
            }
        }
    }
}
