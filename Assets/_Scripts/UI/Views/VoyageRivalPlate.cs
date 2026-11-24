using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The rival crew's plate (Frogma Rev 137, ArkMeet to Rout; approved by Shombith 2026-11-24): top right, while a rival
    /// is in the squad's cell. A rule in the threat red over the crew's name with its mark (their Ark, or the warning of a
    /// runaway crew) and its faction and temper; a rival Ark's hold and hull, or what a runaway crew carries; a row per
    /// escort with what it is doing (guarding, silenced, running, turning back); and one line on how to beat them now.
    /// Only a rival wears the threat red (Rev 134 WorldEvents). It reads <see cref="IVoyageScreenSource.GetRivals"/> and
    /// never takes a press. Every word is <see cref="VoyageLines"/>' Rival* lines.
    /// </summary>
    public sealed class VoyageRivalPlate : MonoBehaviour
    {
        const int SortingOrder = 20002;
        const float RefreshSeconds = 0.2f, FadeSeconds = 0.25f;
        // Rev 137 ArkMeet, on the 1920 x 1080 frame.
        const float PlateX = 1464f, PlateY = 96f, PlateW = 416f, PadX = 18f, RowH = 40f;

        VoyageKit _kit;
        VoyageLines _lines;
        CanvasGroup _group;
        RectTransform _plate;
        float _nextRefresh, _alpha;
        bool _shown;
        string _drawn;
        readonly List<VoyageRivalFix> _rivals = new();

        public void Configure(VoyageHarbour harbour)
        {
            var content = harbour && harbour.Content ? harbour.Content : VoyageContentSO.Load();
            _lines = content ? content.Lines : new VoyageLines();
            _kit = new VoyageKit(VoyageBoardStyleSO.Load(), _lines, null);
            var go = new GameObject("VoyageRivalPlate", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var root = VoyageHudColors.Overlay(go, SortingOrder, hits: false);
            _group = go.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            var safe = VoyageKit.Node("Safe", root);
            VoyageKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>().Apply();
            var frame = VoyageKit.Node("Frame", safe);
            VoyageKit.Fit(frame, new Vector2(1920f, 1080f));
            _plate = VoyageKit.Node("Plate", frame);
            VoyageKit.At(_plate, PlateX, PlateY, PlateW, 300f);
        }

        void Update()
        {
            _alpha = Mathf.MoveTowards(_alpha, _shown ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            _group.alpha = _alpha;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            var source = VoyageScreenFeed.Source;
            _rivals.Clear();
            source?.GetRivals(_rivals);
            // The rival in the squad's own cell, the nearest trouble first: attacking, then racing, then what they left.
            VoyageRivalFix rival = null;
            foreach (var r in _rivals)
                if (r.Act != VoyageRivalAct.NextDoor && (rival == null || Rank(r.Act) > Rank(rival.Act))) rival = r;
            _shown = rival != null && !VoyageScoreboard.IsOpen && !VoyageIntro.IsPlaying;
            if (rival == null) return;
            string key = Key(rival);
            if (key == _drawn) return;
            _drawn = key;
            Draw(rival);
        }

        static int Rank(VoyageRivalAct act) => act switch
        {
            VoyageRivalAct.Attacking => 4,
            VoyageRivalAct.Racing => 3,
            VoyageRivalAct.Broken => 2,
            VoyageRivalAct.Routed => 1,
            _ => 0,
        };

        /// <summary>What the plate shows, as text: it redraws only when that changes (the silence clock in whole seconds).</summary>
        static string Key(VoyageRivalFix r)
        {
            var sb = new System.Text.StringBuilder(96);
            sb.Append(r.Id).Append('|').Append((int)r.Act).Append('|').Append(r.Hold).Append('|')
                .Append(Mathf.RoundToInt(r.Hull * 100f)).Append('|').Append(Mathf.CeilToInt(r.OpenSeconds));
            foreach (var e in r.Escorts) sb.Append('|').Append((int)e.Vessel).Append(':').Append((int)e.State);
            return sb.ToString();
        }

        void Draw(VoyageRivalFix r)
        {
            for (int i = _plate.childCount - 1; i >= 0; i--) Destroy(_plate.GetChild(i).gameObject);
            bool ark = r.Kind == VoyageRivalKind.Ark;
            var threat = VoyageHudColors.Threat;
            float y = 0f;

            var fill = VoyageKit.Node("Fill", _plate);
            var rule = VoyageKit.Node("Rule", _plate);
            VoyageKit.At(rule, 0f, 0f, PlateW, 2f);
            VoyageKit.Block(rule, threat);

            y += 12f;
            var mark = VoyageKit.Node("Mark", _plate);
            VoyageKit.At(mark, PadX, y + 4f, ark ? 20f : 14f, ark ? 10f : 14f);
            var glyph = mark.gameObject.AddComponent<VoyageGlyph>();
            glyph.Form = ark ? VoyageGlyph.Shape.Ark : VoyageGlyph.Shape.Warn;
            glyph.color = threat;
            glyph.raycastTarget = false;
            Text(_kit.Label(_plate, 16, threat), PadX + 28f, y, 260f, 22f, r.Name);
            Text(_kit.Text(_plate, 13, VoyageHudColors.Second, TextAlignmentOptions.Right), PlateW - PadX - 150f, y + 2f, 150f, 20f,
                TemplateText.Fill(_lines.RivalTemper, "faction", FactionName(r.Faction), "temper", TemperName(r.Temper)));
            y += 32f;

            // A rival Ark's hold and hull; a runaway crew's haul.
            Text(_kit.Label(_plate, 12, VoyageHudColors.Second), PadX, y, 90f, 20f, ark ? _lines.RivalHold : _lines.RivalCarrying);
            var gem = VoyageKit.Node("Gem", _plate);
            VoyageKit.At(gem, PadX + 96f, y + 2f, 12f, 16f);
            var g = gem.gameObject.AddComponent<VoyageGlyph>();
            g.Form = VoyageGlyph.Shape.Gem;
            g.color = _kit.Light;
            g.raycastTarget = false;
            Text(_kit.Text(_plate, 16, _kit.Light, TextAlignmentOptions.Left), PadX + 114f, y - 1f, 120f, 22f, r.Hold.ToString());
            y += 26f;
            if (ark)
            {
                Text(_kit.Label(_plate, 12, VoyageHudColors.Second), PadX, y, 90f, 20f, _lines.RivalHull);
                var bar = VoyageKit.Node("Hull", _plate);
                VoyageKit.At(bar, PadX + 96f, y + 7f, 210f, 6f);
                var track = VoyageKit.Node("Track", bar);
                VoyageKit.Stretch(track);
                VoyageKit.Block(track, VoyageKit.Fade(_kit.Outline, 0.45f));
                var hull = VoyageKit.Node("Fill", bar);
                VoyageKit.Block(hull, threat);
                VoyageHudColors.SetFill(hull, r.Hull);
                Text(_kit.Text(_plate, 13, _kit.Light, TextAlignmentOptions.Right), PlateW - PadX - 70f, y, 70f, 20f,
                    TemplateText.Fill(_lines.RivalHullValue, "hull", Mathf.RoundToInt(Mathf.Clamp01(r.Hull) * 100f).ToString()));
                y += 26f;
            }

            // One row per escort: the vessel and what it is doing; a silenced one dims.
            foreach (var e in r.Escorts)
            {
                bool quiet = e.State is VoyageEscortState.Silenced;
                var ink = quiet ? VoyageKit.Fade(_kit.Sub, 0.7f) : _kit.Light;
                Text(_kit.Text(_plate, 14, ink, TextAlignmentOptions.Left), PadX + 96f, y, 220f, 18f, e.Vessel.ToString());
                Text(_kit.Text(_plate, 13, quiet ? ink : VoyageHudColors.Second, TextAlignmentOptions.Left), PadX + 96f, y + 18f, 220f, 18f,
                    StateName(e));
                y += RowH;
            }

            Text(_kit.Text(_plate, 13, VoyageHudColors.Second, TextAlignmentOptions.TopLeft), PadX, y + 4f, PlateW - 2f * PadX, 36f,
                Note(r)).enableWordWrapping = true;
            y += 48f;
            VoyageKit.At(fill, 0f, 0f, PlateW, y);
            VoyageKit.Block(fill, VoyageKit.Fade(_kit.Popup, 0.78f));
            fill.SetAsFirstSibling();
        }

        static TMP_Text Text(TMP_Text t, float x, float y, float w, float h, string words)
        {
            VoyageKit.At(t.rectTransform, x, y, w, h);
            t.text = words ?? string.Empty;
            return t;
        }

        string StateName(in VoyageEscortFix e) => e.State switch
        {
            VoyageEscortState.Silenced => e.SilencedSeconds > 0f
                ? TemplateText.Fill(_lines.RivalSilencedFor, "time", VoyageHudColors.Clock(Mathf.Ceil(e.SilencedSeconds)))
                : _lines.RivalSilenced,
            VoyageEscortState.Running => _lines.RivalRunning,
            VoyageEscortState.TurningBack => _lines.RivalTurningBack,
            _ => _lines.RivalGuarding,
        };

        /// <summary>How to beat them now (Rev 137): break a rival Ark once her escorts are silenced; rout a runaway crew by
        /// silencing all three at once; and what they left when it is done.</summary>
        string Note(VoyageRivalFix r)
        {
            if (r.Act == VoyageRivalAct.Broken) return _lines.RivalNoteBroken;
            if (r.Act == VoyageRivalAct.Routed) return _lines.RivalNoteRouted;
            int silenced = r.Silenced;
            if (r.Kind == VoyageRivalKind.Ark)
                return silenced > 0 ? TemplateText.Fill(_lines.RivalNoteArkOpen, "count", silenced.ToString()) : _lines.RivalNoteArk;
            return silenced > 0
                ? TemplateText.Fill(_lines.RivalNoteRunawayPart, "count", silenced.ToString(), "total", r.Escorts.Count.ToString())
                : _lines.RivalNoteRunaway;
        }

        string TemperName(RivalTemper t) => t switch
        {
            RivalTemper.Careful => _lines.TemperCareful,
            RivalTemper.Bold => _lines.TemperBold,
            _ => _lines.TemperGreedy,
        };

        string FactionName(FactionType f) => VoyageCrewRules.FactionName(_lines, f) ?? string.Empty;
    }
}
