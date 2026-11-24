using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The detour vote (Frogma Rev 134 Vote B, picked by Shombith 2026-11-24, "Vote B"; VoteTie and VoteSolo its states):
    /// low on the left, the pilot who spotted the event asks in their own voice (their portrait, "name · whose pilot",
    /// the quote, what the detour costs and wins), the two choices on the D-pad (left: Detour, right: Press on), the five
    /// seconds counting down, a ballot per seat as the presses come in, and the rule under them (a tie goes to the host;
    /// alone, one press decides). When it closes the outcome stays a moment, then the panel fades. It reads
    /// <see cref="IVoyageScreenSource.OpenVote"/>; a press is <see cref="IVoyageScreenSource.CastVote"/>. Every word is
    /// <see cref="VoyageLines"/>' Vote* lines.
    /// </summary>
    public sealed class VoyageVotePanel : MonoBehaviour
    {
        const int SortingOrder = 20002;      // with the voyage HUD, under the pause chart (24000)
        const float RefreshSeconds = 0.1f, LingerSeconds = 1.6f, FadeSeconds = 0.25f;
        // Rev 134 Vote, on the 1920 x 1080 frame.
        const float PanelX = 40f, PanelY = 640f, PanelW = 660f, PanelH = 272f;
        const float FaceX = 18f, FaceY = 18f, FaceW = 82f, FaceH = 100f, TextX = 120f, TextW = 500f;
        const float ChoiceY = 146f, ChoiceW = 144f, ChoiceH = 44f, BallotY = 202f, BallotFace = 26f;

        VoyageKit _kit;
        VoyageLines _lines;
        RectTransform _root, _panel, _ballots;
        CanvasGroup _group;
        Image _face;
        TMP_Text _who, _quote, _cost, _clock, _rule;
        Button _detour, _pressOn;
        VoyageOutline _detourEdge, _pressOnEdge;
        VoyageVote _shown;
        float _closedAt = -1f, _nextRefresh, _alpha;
        readonly List<VoyageScreenSeat> _seats = new();

        public void Configure(VoyageHarbour harbour)
        {
            var content = harbour && harbour.Content ? harbour.Content : VoyageContentSO.Load();
            _lines = content ? content.Lines : new VoyageLines();
            _kit = new VoyageKit(VoyageBoardStyleSO.Load(), _lines, null);
            Build();
        }

        void Build()
        {
            var go = new GameObject("VoyageVote", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _root = VoyageHudColors.Overlay(go, SortingOrder, hits: true);
            _group = go.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            var safe = VoyageKit.Node("Safe", _root);
            VoyageKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>().Apply();
            var frame = VoyageKit.Node("Frame", safe);
            VoyageKit.Fit(frame, new Vector2(1920f, 1080f));

            _panel = VoyageKit.Node("Panel", frame);
            VoyageKit.At(_panel, PanelX, PanelY, PanelW, PanelH);
            // A band that fades out to the right (the feed's look, Rev 120 B) with a stripe down its left edge.
            VoyageKit.Band(_panel, VoyageKit.Fade(_kit.Popup, 0.82f), 0.35f);
            var stripe = VoyageKit.Node("Stripe", _panel);
            VoyageKit.At(stripe, 0f, 0f, 2f, PanelH);
            VoyageKit.Block(stripe, _kit.Light);

            var faceBox = VoyageKit.Node("Face", _panel);
            VoyageKit.At(faceBox, FaceX, FaceY, FaceW, FaceH);
            VoyageKit.Block(faceBox, VoyageKit.Fade(_kit.Outline, 0.45f));
            _face = VoyageKit.CoverBox(faceBox);

            _who = Text(_kit.Label(_panel, 15, _kit.Light), TextX, 20f, TextW, 22f);
            _quote = Text(_kit.Text(_panel, 20, _kit.Light, TextAlignmentOptions.TopLeft), TextX, 44f, TextW, 54f);
            _quote.enableWordWrapping = true;
            _cost = Text(_kit.Text(_panel, 15, VoyageHudColors.Second, TextAlignmentOptions.TopLeft), TextX, 104f, TextW, 38f);
            _cost.enableWordWrapping = true;

            _detour = Choice(TextX, VoyageGlyph.Shape.Left, true, _lines.VoteDetour, VoyageVote.Detour, out _detourEdge);
            _pressOn = Choice(TextX + ChoiceW + 12f, VoyageGlyph.Shape.Right, false, _lines.VotePressOn, VoyageVote.PressOn, out _pressOnEdge);
            _clock = Text(_kit.Text(_panel, 24, _kit.Light, TextAlignmentOptions.Right), TextX + 2f * ChoiceW + 40f, ChoiceY + 6f, 120f, 32f);

            _ballots = VoyageKit.Node("Ballots", _panel);
            VoyageKit.At(_ballots, TextX, BallotY, TextW, BallotFace + 4f);
            VoyageKit.Row(_ballots, 16f, TextAnchor.MiddleLeft, expandWidth: false);
            _rule = Text(_kit.Text(_panel, 13, VoyageHudColors.Second, TextAlignmentOptions.TopLeft), TextX, BallotY + 38f, TextW, 20f);
        }

        static TMP_Text Text(TMP_Text t, float x, float y, float w, float h)
        {
            VoyageKit.At(t.rectTransform, x, y, w, h);
            return t;
        }

        /// <summary>One of the two choices: a keycap with the D-pad arrow and the words, outlined in the light once pressed.</summary>
        Button Choice(float x, VoyageGlyph.Shape arrow, bool arrowFirst, string words, int option, out VoyageOutline edge)
        {
            var rt = VoyageKit.Node(option == VoyageVote.Detour ? "Detour" : "PressOn", _panel);
            VoyageKit.At(rt, x, ChoiceY, ChoiceW, ChoiceH);
            var fill = VoyageKit.Block(rt, VoyageKit.Fade(_kit.Popup, 0.8f), hit: true);
            var line = VoyageKit.Node("Edge", rt);
            VoyageKit.Stretch(line);
            edge = line.gameObject.AddComponent<VoyageOutline>();
            edge.raycastTarget = false;
            edge.Solid(VoyageKit.Fade(_kit.Sub, 0.6f));
            var row = VoyageKit.Node("Row", rt);
            VoyageKit.Stretch(row);
            VoyageKit.Row(row, 10f, TextAnchor.MiddleCenter, expandWidth: false);
            void Key()
            {
                var cap = VoyageKit.Node("Key", row);
                var le = VoyageKit.Le(cap);
                le.minWidth = le.preferredWidth = 28f;
                le.minHeight = le.preferredHeight = 24f;
                cap.gameObject.AddComponent<VoyageOutline>().Solid(_kit.Light);
                var g = VoyageKit.Node("Arrow", cap);
                VoyageKit.At(g, 7f, 5f, 14f, 14f);
                var glyph = g.gameObject.AddComponent<VoyageGlyph>();
                glyph.Form = arrow;
                glyph.color = _kit.Light;
                glyph.raycastTarget = false;
            }
            if (arrowFirst) Key();
            var t = _kit.Label(row, 14, _kit.Light, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.UpperCase | FontStyles.Bold;
            t.text = words ?? string.Empty;
            VoyageKit.Le(t).preferredWidth = t.GetPreferredValues(t.text).x + 4f;
            if (!arrowFirst) Key();
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => VoyageScreenFeed.Source?.CastVote(option));
            return b;
        }

        // ── Live ────────────────────────────────────────────────────────────

        void Update()
        {
            var source = VoyageScreenFeed.Source;
            var open = source?.OpenVote;
            if (open != null && !ReferenceEquals(open, _shown))
            {
                _shown = open;
                _closedAt = -1f;
                source.GetSeats(_seats);
                Ask(open);
            }
            if (_shown != null && _shown.Closed && _closedAt < 0f) _closedAt = Time.unscaledTime;
            bool showing = _shown != null && (_closedAt < 0f || Time.unscaledTime - _closedAt < LingerSeconds);
            if (!showing && _closedAt >= 0f && _alpha <= 0f) _shown = null;

            _alpha = Mathf.MoveTowards(_alpha, showing ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            _group.alpha = _alpha;
            _group.blocksRaycasts = showing && !_shown.Closed;
            if (_shown == null) return;

            // The D-pad (and the arrow keys) press while the vote is open.
            if (!_shown.Closed && source != null)
            {
                var pad = Gamepad.current;
                var keys = Keyboard.current;
                if (pad != null && pad.dpad.left.wasPressedThisFrame || keys != null && keys.leftArrowKey.wasPressedThisFrame)
                    source.CastVote(VoyageVote.Detour);
                else if (pad != null && pad.dpad.right.wasPressedThisFrame || keys != null && keys.rightArrowKey.wasPressedThisFrame)
                    source.CastVote(VoyageVote.PressOn);
            }
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            Tally(_shown);
        }

        /// <summary>The asker's face and words, set once as the vote opens.</summary>
        void Ask(VoyageVote v)
        {
            var asker = v.Asker >= 0 && v.Asker < _seats.Count ? _seats[v.Asker] : default;
            VoyageKit.ShowCover(_face, asker.Portrait as Sprite, new Vector2(FaceW, FaceH), VoyageKit.FaceFocus);
            string whose = asker.You ? _lines.VoteAskerYours
                : asker.Human ? TemplateText.Fill(_lines.VoteAskerFriend, "friend", FriendOf(v.Asker))
                : _lines.VoteAskerWingmate;
            _who.text = TemplateText.Fill(_lines.VoteAsker, "pilot", asker.Pilot ?? string.Empty, "whose", whose);
            string place = string.IsNullOrEmpty(v.AboutTitle) ? KindName(v.About) : v.AboutTitle;
            _quote.text = TemplateText.Fill(v.Solo ? _lines.VoteQuoteSolo : QuoteOf(v.About), "event", place);
            _cost.text = TemplateText.Fill(_lines.VoteCost, "time", VoyageHudColors.Clock(v.DetourSeconds), "gain", GainOf(v.About));
            _rule.text = v.Solo ? _lines.VoteRuleSolo : _lines.VoteRuleParty;
        }

        /// <summary>The asker is a friend's seat: their player's name.</summary>
        string FriendOf(int seat) => seat >= 0 && seat < _seats.Count ? _seats[seat].Player ?? string.Empty : string.Empty;

        /// <summary>The clock, each seat's ballot, which choice this pilot pressed, and how it ended.</summary>
        void Tally(VoyageVote v)
        {
            _clock.text = VoyageHudColors.Clock(v.Closed ? 0f : Mathf.Ceil(v.SecondsLeft));
            int mine = -1;
            for (int i = 0; i < _seats.Count; i++) if (_seats[i].You) mine = v.Ballot(i);
            int lit = v.Closed ? v.Outcome : mine;
            _detourEdge.Solid(lit == VoyageVote.Detour ? _kit.Light : VoyageKit.Fade(_kit.Sub, 0.6f));
            _pressOnEdge.Solid(lit == VoyageVote.PressOn ? _kit.Light : VoyageKit.Fade(_kit.Sub, 0.6f));
            _detour.interactable = _pressOn.interactable = !v.Closed;

            for (int i = _ballots.childCount - 1; i >= 0; i--) Destroy(_ballots.GetChild(i).gameObject);
            if (!v.Solo)
                for (int seat = 0; seat < Mathf.Min(v.Seats, _seats.Count); seat++)
                    Ballot(seat, v.Ballot(seat), seat == v.Host);
            if (v.Closed) _rule.text = EndLine(v);
        }

        void Ballot(int seat, int option, bool host)
        {
            var cell = VoyageKit.Node("Seat" + seat, _ballots);
            VoyageKit.Row(cell, 8f, TextAnchor.MiddleLeft, expandWidth: false);
            var faceBox = VoyageKit.Node("Face", cell);
            var le = VoyageKit.Le(faceBox);
            le.minWidth = le.preferredWidth = le.minHeight = le.preferredHeight = BallotFace;
            VoyageKit.Block(faceBox, VoyageKit.Fade(_kit.Outline, 0.45f));
            var face = VoyageKit.CoverBox(faceBox);
            VoyageKit.ShowCover(face, _seats[seat].Portrait as Sprite, new Vector2(BallotFace, BallotFace), VoyageKit.FaceFocus);
            string choice = option switch
            {
                VoyageVote.Detour => _lines.VoteDetourWord,
                VoyageVote.PressOn => _lines.VotePressOnWord,
                _ => _lines.VoteNoBallot,
            };
            if (host && option != VoyageVote.None) choice = TemplateText.Fill(_lines.VoteBallotHost, "choice", choice);
            var t = _kit.Text(cell, 15, option == VoyageVote.None ? VoyageKit.Fade(_kit.Sub, 0.7f) : _kit.Light, TextAlignmentOptions.Left);
            t.text = choice;
            VoyageKit.Le(t).preferredWidth = t.GetPreferredValues(choice).x + 2f;
        }

        string EndLine(VoyageVote v)
        {
            string choice = v.Outcome == VoyageVote.Detour ? _lines.VoteDetourWord : _lines.VotePressOnWord;
            string template = v.End switch
            {
                VoyageVoteEnd.HostBrokeTie => _lines.VoteEndTie,
                VoyageVoteEnd.NoPress => _lines.VoteEndNoPress,
                _ => _lines.VoteEndChosen,
            };
            return TemplateText.Fill(template, "choice", choice);
        }

        string KindName(VoyageNeighbourKind kind) => kind switch
        {
            VoyageNeighbourKind.Wreck => _lines.ChartWreck,
            VoyageNeighbourKind.Bloom => _lines.ChartBloom,
            VoyageNeighbourKind.Distress => _lines.ChartDistress,
            VoyageNeighbourKind.Predators => _lines.ChartPredators,
            _ => _lines.ChartRivalCrew,
        };

        string QuoteOf(VoyageNeighbourKind kind) => kind switch
        {
            VoyageNeighbourKind.Wreck => _lines.VoteQuoteWreck,
            VoyageNeighbourKind.Bloom => _lines.VoteQuoteBloom,
            VoyageNeighbourKind.Distress => _lines.VoteQuoteDistress,
            VoyageNeighbourKind.Predators => _lines.VoteQuotePredators,
            _ => _lines.VoteQuoteRival,
        };

        string GainOf(VoyageNeighbourKind kind) => kind switch
        {
            VoyageNeighbourKind.Wreck => _lines.VoteGainWreck,
            VoyageNeighbourKind.Bloom => _lines.VoteGainBloom,
            VoyageNeighbourKind.Distress => _lines.VoteGainDistress,
            VoyageNeighbourKind.Predators => _lines.VoteGainPredators,
            _ => _lines.VoteGainRival,
        };
    }
}
