using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The end board of a named voyage (Frogma Rev 134 Awards A, picked by Shombith 2026-11-24, "Awards A"; Lost its fallen
    /// state; Rev 137 Fall's Rival Breaker): over the dimmed sea, the voyage's name, its cells and its time as a kicker, then
    /// what happened as the title; the run drawn as a line through its places; one award card per seat (what the seat did
    /// best, the number behind it, the hull it flew, who flew it); the pay; and the board's own ways out
    /// (<see cref="VoyageScoreboardView"/> draws the buttons, so every choice keeps its rules). A fallen Ark reads as one
    /// centred card: where the voyage ended, what went down with her, and the squad. Crystal Run keeps its own board: this
    /// draws only a board with the voyage's places (<see cref="VoyageScoreboard.Stops"/>). Every word is
    /// <see cref="VoyageLines"/>' Awards* lines.
    /// </summary>
    public sealed class VoyageAwardsBoard
    {
        // Rev 134 Awards, on the 1920 x 1080 frame.
        const float KickerY = 70f, TitleY = 96f, LineY = 230f, LineX0 = 260f, LineX1 = 1660f, LineDip = 40f;
        const float CardsY = 340f, CardW = 500f, CardH = 400f, CardGap = 40f, PayY = 784f;
        const float LostKickerY = 300f;

        readonly VoyageKit _kit;
        readonly VoyageLines _lines;
        readonly SO_VesselList _vessels;

        public VoyageAwardsBoard(VoyageKit kit, VoyageLines lines)
        {
            _kit = kit;
            _lines = lines;
            var intro = VoyageIntroSO.LoadCrystalRun();
            _vessels = intro ? intro.Vessels : null;
        }

        /// <summary>True for the boards this draws: a named voyage's (it carries its places).</summary>
        public static bool Draws(VoyageScoreboard b) =>
            b != null && b.Stops.Count > 0 && b.Board is VoyageScoreboard.Kind.CrystalRun or VoyageScoreboard.Kind.Lost;

        public void Build(RectTransform frame, VoyageScoreboard b)
        {
            if (b.Board == VoyageScoreboard.Kind.Lost) Lost(frame, b);
            else Awards(frame, b);
        }

        // ── Awards A ────────────────────────────────────────────────────────

        void Awards(RectTransform frame, VoyageScoreboard b)
        {
            string time = VoyageHudColors.Clock(b.Seconds);
            Line(frame, 0f, KickerY, 1920f, 16f, _kit.Sub, TextAlignmentOptions.Center, 12, label: true,
                TemplateText.Fill(_lines.AwardsKicker, "name", b.Name, "cells", b.Cells.ToString(), "time", time));
            Line(frame, 0f, TitleY, 1920f, 56f, _kit.Light, TextAlignmentOptions.Center, 40, label: true,
                b.Succeeded ? _lines.AwardsTitleDone : _lines.AwardsTitleMissed);
            RunLine(frame, b.Stops);
            Cards(frame, b);
            Pay(frame, b);
        }

        /// <summary>The run as a line through its places, a dot at each, the first and last named under them.</summary>
        void RunLine(RectTransform frame, IReadOnlyList<string> stops)
        {
            int n = stops.Count;
            if (n < 2) return;
            var points = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                // A gentle swell, so the line reads as a course and not a ruler.
                points.Add(new Vector2(Mathf.Lerp(LineX0, LineX1, t), LineY - Mathf.Sin(t * Mathf.PI) * LineDip * 0.25f));
            }
            var rt = VoyageKit.Node("RunLine", frame);
            VoyageKit.At(rt, 0f, 0f, 1920f, 1080f);
            var g = rt.gameObject.AddComponent<VoyageChartLine>();
            g.color = VoyageKit.Fade(_kit.Light, 0.85f);
            g.Set(points, 2f, 0f, 0f, 0f);
            for (int i = 0; i < n; i++)
            {
                var dot = VoyageKit.Node("Stop", frame);
                VoyageKit.At(dot, points[i].x - 5f, points[i].y - 5f, 10f, 10f);
                dot.gameObject.AddComponent<VoyageRing>().Set(_kit.Light, _kit.Light);
                if (i != 0 && i != n - 1 && n > 4) continue;
                Line(frame, points[i].x - 140f, points[i].y + 12f, 280f, 20f, VoyageHudColors.Second, TextAlignmentOptions.Center, 13,
                    label: false, stops[i]);
            }
        }

        /// <summary>One card per seat, its award picked by <see cref="VoyageAwards.Pick"/>.</summary>
        void Cards(RectTransform frame, VoyageScoreboard b)
        {
            var scores = new List<VoyageAwardScore>(b.Rows.Count);
            foreach (var r in b.Rows)
                scores.Add(new VoyageAwardScore { Crystals = r.Crystals, Kills = r.Kills, Guarded = r.Guarded, RivalHull = r.RivalHull });
            var awards = VoyageAwards.Pick(scores);
            int count = Mathf.Min(3, b.Rows.Count);
            float total = count * CardW + (count - 1) * CardGap;
            float x = 960f - total * 0.5f;
            for (int i = 0; i < count; i++, x += CardW + CardGap)
                Card(frame, x, b.Rows[i], awards[i], scores, i);
        }

        void Card(RectTransform frame, float x, VoyageScoreboard.Row row, VoyageAward award, List<VoyageAwardScore> scores, int seat)
        {
            var card = VoyageKit.Node("Award", frame);
            VoyageKit.At(card, x, CardsY, CardW, CardH);
            var glass = _kit.Glass(card, hit: false);
            var c = glass.Content ? glass.Content : card;
            var rule = VoyageKit.Node("Rule", card);
            VoyageKit.At(rule, 0f, 0f, CardW, 2f);
            VoyageKit.Block(rule, row.You ? _kit.Light : VoyageKit.Fade(_kit.Sub, 0.8f));

            Line(c, 22f, 22f, CardW - 44f, 28f, _kit.Light, TextAlignmentOptions.Left, 22, label: true, AwardName(award));
            Line(c, 22f, 54f, CardW - 44f, 20f, VoyageHudColors.Second, TextAlignmentOptions.Left, 15, label: false,
                AwardLine(award, row, scores, seat));

            // The hull this seat flew, large across the card.
            var vessel = _vessels && _vessels.TryGetVesselByClass(row.Vessel, out var v, reportMissing: false) ? v : null;
            var hullBox = VoyageKit.Node("Hull", c);
            VoyageKit.At(hullBox, 40f, 90f, CardW - 80f, 210f);
            var hull = VoyageKit.Picture(hullBox);
            hull.preserveAspect = true;
            VoyageKit.Show(hull, VoyageSeats.HullOf(vessel));

            // Who flew it.
            string who = TemplateText.Fill(row.You ? _lines.AwardsYou : _lines.AwardsSeat, "name", row.Name,
                "vessel", vessel ? vessel.CardName : row.Vessel.ToString());
            Line(c, 22f, CardH - 74f, CardW - 44f, 24f, _kit.Light, TextAlignmentOptions.Left, 16, label: false, who);
        }

        string AwardName(VoyageAward award) => award switch
        {
            VoyageAward.TopSalvager => _lines.AwardTopSalvager,
            VoyageAward.WallOfTheVoyage => _lines.AwardWall,
            VoyageAward.RivalBreaker => _lines.AwardRivalBreaker,
            VoyageAward.Hunter => _lines.AwardHunter,
            _ => _lines.AwardCrew,
        };

        string AwardLine(VoyageAward award, VoyageScoreboard.Row row, List<VoyageAwardScore> scores, int seat) => award switch
        {
            VoyageAward.TopSalvager => TemplateText.Fill(_lines.AwardTopSalvagerLine, "count", row.Crystals.ToString()),
            VoyageAward.WallOfTheVoyage => TemplateText.Fill(_lines.AwardWallLine, "share",
                VoyageAwards.GuardedPercent(scores, seat).ToString()),
            VoyageAward.RivalBreaker => TemplateText.Fill(_lines.AwardRivalBreakerLine, "share",
                Mathf.RoundToInt(Mathf.Clamp01(row.RivalHull) * 100f).ToString()),
            VoyageAward.Hunter => TemplateText.Fill(_lines.AwardHunterLine, "count", row.Kills.ToString()),
            _ => _lines.AwardCrewLine,
        };

        /// <summary>The pay under the cards: what the job pays, and whether it is banked or still at stake.</summary>
        void Pay(RectTransform frame, VoyageScoreboard b)
        {
            var row = VoyageKit.Node("Pay", frame);
            VoyageKit.At(row, 0f, PayY, 1920f, 36f);
            VoyageKit.Row(row, 12f, TextAnchor.MiddleCenter, expandWidth: false);
            var gem = VoyageKit.Node("Gem", row);
            var le = VoyageKit.Le(gem);
            le.minWidth = le.preferredWidth = 16f;
            le.minHeight = le.preferredHeight = 20f;
            var g = gem.gameObject.AddComponent<VoyageGlyph>();
            g.Form = VoyageGlyph.Shape.Gem;
            g.color = _kit.Light;
            g.raycastTarget = false;
            Inline(row, 26, _kit.Light, b.Pay.ToString());
            Inline(row, 15, VoyageHudColors.Second, _lines.AwardsPay);
            Inline(row, 15, b.Banked ? _kit.CtaFill : VoyageHudColors.Second, b.Banked ? _lines.ScoreBanked : _lines.ScoreAtStake);
        }

        // ── Lost ────────────────────────────────────────────────────────────

        void Lost(RectTransform frame, VoyageScoreboard b)
        {
            Line(frame, 0f, LostKickerY, 1920f, 20f, VoyageHudColors.Threat, TextAlignmentOptions.Center, 16, label: true,
                _lines.AwardsLostKicker);
            Line(frame, 0f, LostKickerY + 26f, 1920f, 56f, _kit.Light, TextAlignmentOptions.Center, 44, label: true,
                TemplateText.Fill(_lines.AwardsLostTitle, "name", b.Name, "place", string.IsNullOrEmpty(b.LostAt) ? b.To : b.LostAt));
            Line(frame, 0f, LostKickerY + 90f, 1920f, 22f, VoyageHudColors.Second, TextAlignmentOptions.Center, 15, label: false,
                TemplateText.Fill(_lines.AwardsLostLine, "cell", Mathf.Max(1, b.LostCell).ToString(),
                    "cells", Mathf.Max(1, b.Stops.Count - 1).ToString(), "time", VoyageHudColors.Clock(b.Seconds),
                    "count", b.CrystalTotal.ToString()));

            var row = VoyageKit.Node("Squad", frame);
            VoyageKit.At(row, 0f, LostKickerY + 140f, 1920f, 40f);
            VoyageKit.Row(row, 30f, TextAnchor.MiddleCenter, expandWidth: false);
            foreach (var r in b.Rows)
            {
                var vessel = _vessels && _vessels.TryGetVesselByClass(r.Vessel, out var v, reportMissing: false) ? v : null;
                Inline(row, 15, r.You ? _kit.Light : VoyageHudColors.Second,
                    TemplateText.Fill(r.You ? _lines.AwardsYou : _lines.AwardsSeat, "name", r.Name,
                        "vessel", vessel ? vessel.CardName : r.Vessel.ToString()));
            }
        }

        // ── Text ────────────────────────────────────────────────────────────

        TMP_Text Line(RectTransform parent, float x, float y, float w, float h, Color color, TextAlignmentOptions align, float size,
            bool label, string words)
        {
            var t = label ? _kit.Label(parent, size, color, align) : _kit.Text(parent, size, color, align);
            VoyageKit.At(t.rectTransform, x, y, w, h);
            t.text = words ?? string.Empty;
            return t;
        }

        void Inline(RectTransform row, float size, Color color, string words)
        {
            if (string.IsNullOrEmpty(words)) return;
            var t = _kit.Text(row, size, color, TextAlignmentOptions.Left);
            t.text = words;
            var le = VoyageKit.Le(t);
            le.preferredWidth = t.GetPreferredValues(words).x + 2f;
        }
    }
}
