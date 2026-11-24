using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The run's feed headlines for the Milestone 2 events (Frogma Rev 134 Crossing, Event, Detour, Rival, Hostile, Joins and
    /// Rev 137 ArkMeet to Rout): one call per moment, each a headline and a line under it in the feed under the chip, in the
    /// mood the board gives it (only a rival wears the threat red; a good turn is green). The gameplay calls these as its
    /// events happen; the words are <see cref="VoyageLines"/>' Toast* lines.
    /// </summary>
    public static class VoyageScreenToasts
    {
        /// <summary>A named voyage crosses into a cell (Rev 134 Crossing): the cell's name over "Cell n of m · the sea".</summary>
        public static void Crossing(ArkwayRun sea, VoyageLines lines, string place, int cell, int cells, int difficulty)
        {
            if (lines == null) return;
            VoyageToasts.Show(sea, ArkwayVoyageHud.Mood.Event, TemplateText.Fill(lines.ToastCellNamed, "place", place),
                TemplateText.Fill(difficulty > 0 ? lines.ToastCellNamedSub : lines.ToastCellNamedSubCalm, "cell", cell.ToString(),
                    "cells", cells.ToString(), "sea", SeaName(lines, difficulty)));
        }

        /// <summary>An event lights up next door (Rev 134 Event): what and where, how long, and that the squad will vote.</summary>
        public static void Neighbour(ArkwayRun sea, VoyageLines lines, in VoyageNeighbourFix n)
        {
            if (lines == null) return;
            VoyageToasts.Show(sea, n.Kind == VoyageNeighbourKind.Rival ? ArkwayVoyageHud.Mood.Threat : ArkwayVoyageHud.Mood.Event,
                TemplateText.Fill(lines.ToastEventNext, "event", EventName(lines, n), "side", Side(lines, n.Bearing)),
                TemplateText.Fill(lines.ToastEventNextSub, "time", VoyageHudColors.Clock(n.SecondsLeft)));
        }

        /// <summary>The squad turned off the lane (Rev 134 Detour).</summary>
        public static void Detour(ArkwayRun sea, VoyageLines lines, in VoyageNeighbourFix n)
        {
            if (lines == null) return;
            VoyageToasts.Show(sea, ArkwayVoyageHud.Mood.Event, TemplateText.Fill(lines.ToastDetour, "event", EventName(lines, n)),
                TemplateText.Fill(lines.ToastDetourSub, "verb", VoyagePauseChart.VerbName(lines, n.Verb)));
        }

        /// <summary>A friend takes an AI's seat at the next cell (Rev 134 Joins).</summary>
        public static void Joins(ArkwayRun sea, VoyageLines lines, string friend, string place)
        {
            if (lines == null) return;
            VoyageToasts.Show(sea, ArkwayVoyageHud.Mood.Good, TemplateText.Fill(lines.ToastJoins, "friend", friend),
                TemplateText.Fill(lines.ToastJoinsSub, "place", place));
        }

        /// <summary>A rival crew's moment (Rev 134 Rival and Hostile, Rev 137 ArkMeet, ArkOpen, ArkBreak, Runaway, Rout).</summary>
        public static void Rival(ArkwayRun sea, VoyageLines lines, VoyageRivalFix r, int spilled = 0)
        {
            if (lines == null || r == null) return;
            bool ark = r.Kind == VoyageRivalKind.Ark;
            string head, under;
            var mood = ArkwayVoyageHud.Mood.Threat;
            switch (r.Act)
            {
                case VoyageRivalAct.Attacking:
                    head = TemplateText.Fill(ark ? lines.ToastRivalAttack : lines.ToastRunaway, "name", r.Name);
                    under = ark ? lines.ToastRivalAttackSub : lines.ToastRunawaySub;
                    break;
                case VoyageRivalAct.Broken:
                    mood = ArkwayVoyageHud.Mood.Good;
                    head = TemplateText.Fill(lines.ToastRivalBurst, "ark", r.ArkName);
                    under = TemplateText.Fill(lines.ToastRivalBurstSub, "count", spilled.ToString());
                    break;
                case VoyageRivalAct.Routed:
                    mood = ArkwayVoyageHud.Mood.Good;
                    head = TemplateText.Fill(lines.ToastRout, "name", r.Name);
                    under = TemplateText.Fill(lines.ToastRoutSub, "count", spilled.ToString());
                    break;
                default:
                    if (ark && r.Silenced > 0)
                    {
                        mood = ArkwayVoyageHud.Mood.Event;
                        head = TemplateText.Fill(lines.ToastRivalOpen, "ark", r.ArkName);
                        under = TemplateText.Fill(lines.ToastRivalOpenSub, "count", r.Silenced.ToString(),
                            "time", VoyageHudColors.Clock(Mathf.Ceil(r.OpenSeconds)));
                    }
                    else if (ark)
                    {
                        head = TemplateText.Fill(lines.ToastRivalArk, "name", r.Name);
                        under = TemplateText.Fill(lines.ToastRivalArkSub, "ark", r.ArkName, "hold", r.Hold.ToString());
                    }
                    else
                    {
                        head = TemplateText.Fill(lines.ToastRivalHere, "name", r.Name);
                        under = lines.ToastRivalHereSub;
                    }
                    break;
            }
            VoyageToasts.Show(sea, mood, head, under);
        }

        /// <summary>The sea's danger as the player reads it: the voyage's difficulty (1-9), or nothing without one.</summary>
        public static string SeaName(VoyageLines lines, int difficulty) =>
            difficulty > 0 ? TemplateText.Fill(lines.SeaDifficulty, "difficulty", difficulty.ToString()) : string.Empty;

        static string EventName(VoyageLines lines, in VoyageNeighbourFix n) =>
            !string.IsNullOrEmpty(n.Title) ? n.Title : n.Kind switch
            {
                VoyageNeighbourKind.Wreck => lines.ChartWreck,
                VoyageNeighbourKind.Bloom => lines.ChartBloom,
                VoyageNeighbourKind.Distress => lines.ChartDistress,
                VoyageNeighbourKind.Predators => lines.ChartPredators,
                _ => lines.ChartRivalCrew,
            };

        static string Side(VoyageLines lines, float bearing) => bearing >= 0f ? lines.SidePort : lines.SideStarboard;
    }
}
