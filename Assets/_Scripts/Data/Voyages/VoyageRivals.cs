using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The other crews on the HyperSea (Docs/Voyages/DEPTH.md §3). A rival crew is authored (<see cref="RivalCrew"/>)
    /// and lives on the record (<see cref="RivalState"/>). Every day an idle crew draws a job from the same templates the
    /// squad's pilots draw from, over the same news, by its temper; it sails a day a lane; on arrival it succeeds with a
    /// chance set by its strength against the course and the place, and its outcome writes the same events a pilot's
    /// would. A crew that fails at a rough place may fall, and its fall is a wreck. A crew with a grudge, a pirate with a
    /// tip on a laden Ark, or a bounty hunter on a notorious one sails for the squad instead. Pure, like the rest.
    ///
    /// <para>What waits for V2: a rival's Ark in the world (the host-owned networked Ark, VOYAGES.md §8). Until then a
    /// rival's fall is resolved here, and its vessels, not its Ark, stand at the stop (<c>VoyageRun</c>).</para>
    /// </summary>
    public static class VoyageRivals
    {
        /// <summary>The rival crews standing at <paramref name="locationId"/> (out, arrived, not bound elsewhere).</summary>
        public static List<RivalState> StandingAt(VoyageState state, VoyageContent content, string locationId)
        {
            var standing = new List<RivalState>();
            if (state?.Rivals == null || string.IsNullOrEmpty(locationId)) return standing;
            foreach (var rs in state.Rivals)
                if (rs != null && rs.IsOut && string.IsNullOrEmpty(rs.To) && rs.At == locationId && content?.Rival(rs.Id) != null)
                    standing.Add(rs);
            return standing;
        }

        /// <summary>
        /// What stands between the squad and a rival it meets (DEPTH.md §3): co-op for the squad's own domain; hostile
        /// for a grudge of two or more, a pirate when the Ark is laden, a bounty hunter when the crew is notorious; a race
        /// where there are finds to take; nothing otherwise.
        /// </summary>
        public static RivalRelation Relation(VoyageState state, VoyageContent content, RivalCrew crew, RivalState rs, VoyageLocation here)
        {
            if (crew == null || rs == null || state == null) return RivalRelation.None;
            var w = content?.World ?? new VoyageWorldRules();
            if (crew.Domain == VoyageRules.SquadDomain(state, content)) return RivalRelation.CoOp;
            if (rs.Grudge >= 2) return RivalRelation.Hostile;
            if (crew.Faction == FactionType.Pirate && state.Aboard.Count >= w.LadenRumourAt) return RivalRelation.Hostile;
            if (crew.Faction == FactionType.BountyHunter && state.Notoriety >= w.BountyAt) return RivalRelation.Hostile;
            if (here != null)
            {
                var place = state.PlaceIfAny(here.Id);
                bool finds = (VoyageRules.HasSealedFinds(here) && (place == null || place.Sealed != 0)) || state.WreckAt(here.Id) != null;
                if (finds) return RivalRelation.Race;
            }
            return RivalRelation.None;
        }

        // ── The day ─────────────────────────────────────────────────────────

        /// <summary>One day for every crew: the fallen return, the sailing arrive and resolve, the hunters hunt, the idle
        /// draw work or wander to the next port.</summary>
        internal static void TickDay(VoyageState state, VoyageContent content, int day)
        {
            var w = content.World ?? new VoyageWorldRules();
            foreach (var crew in content.Rivals ?? new List<RivalCrew>())
            {
                if (crew == null) continue;
                var rs = state.Rival(crew.Id);
                if (rs == null) continue;
                if (!rs.IsOut)
                {
                    if (day >= rs.ReturnDay) Return(content, crew, rs);
                    continue;
                }
                if (!string.IsNullOrEmpty(rs.To))
                {
                    if (day >= rs.ArriveDay) Arrive(state, content, crew, rs, day);
                    continue;
                }
                // Standing where the squad's Ark lies, a crew stays: the meeting is on, and the squad's arrival decides it.
                if (!state.AtSea && rs.At == state.At && content.Location(rs.At)?.Experience != VoyageExperience.Harbour) continue;
                if (TryHunt(state, content, crew, rs, day)) continue;
                var job = Draw(state, content, crew, rs, day);
                if (job != null)
                {
                    rs.To = job.To;
                    rs.JobTemplate = job.TemplateId;
                    rs.ArriveDay = day + Math.Max(1, job.Hops);
                    continue;
                }
                // Nothing to do here: wander to the next port, so the crew is seen about.
                var next = NearestOtherPort(content, rs.At);
                if (next == null) continue;
                var route = VoyageRules.Route(content, rs.At, next.Id);
                rs.To = next.Id;
                rs.JobTemplate = string.Empty;
                rs.ArriveDay = day + Math.Max(1, (route?.Count ?? 2) - 1);
            }
            PostBounties(state, content, day);
            PostAccords(state, content, day);
        }

        static VoyageLocation NearestOtherPort(VoyageContent content, string from)
        {
            var dist = VoyageRules.Distances(content, from);
            VoyageLocation best = null;
            int bestHops = int.MaxValue;
            foreach (var l in content.Locations)
                if (VoyageRules.IsPort(l) && l.Id != from && dist.TryGetValue(l.Id, out int hops) && hops < bestHops) { best = l; bestHops = hops; }
            return best;
        }

        static void Return(VoyageContent content, RivalCrew crew, RivalState rs)
        {
            rs.At = content.Location(crew.Home) != null ? crew.Home : rs.At;
            rs.To = string.Empty;
            rs.JobTemplate = string.Empty;
            rs.WreckedDay = -1;
            rs.Notoriety = 0;
            rs.Hold.Clear();
        }

        /// <summary>A crew with a reason sails for the squad: a grudge of two, a pirate's tip on a laden Ark it has heard
        /// of, a bounty hunter's warrant. It arrives where the squad is, or where it is bound.</summary>
        static bool TryHunt(VoyageState state, VoyageContent content, RivalCrew crew, RivalState rs, int day)
        {
            var w = content.World ?? new VoyageWorldRules();
            if (crew.Domain == VoyageRules.SquadDomain(state, content)) return false;
            bool reason = rs.Grudge >= 2;
            if (!reason && crew.Faction == FactionType.Pirate && state.Aboard.Count >= w.LadenRumourAt)
            {
                // The tip has to have reached a port the crew reads: the one it stands at, or the one nearest it.
                var port = VoyageRules.NearestPort(content, rs.At);
                if (port != null)
                    foreach (var n in state.Ledger(port.Id).News)
                        if (n != null && n.Tag == NewsTag.Rumour && n.Rival == VoyageRules.SelfRival) { reason = true; break; }
            }
            if (!reason && crew.Faction == FactionType.BountyHunter && state.Notoriety >= w.BountyAt) reason = true;
            if (!reason) return false;
            string target = state.AtSea && state.Current != null ? state.Current.To : state.At;
            if (string.IsNullOrEmpty(target) || target == rs.At || content.Location(target) == null) return false;
            // No rival squad in a harbour (VOYAGES.md §6.7 rule 2).
            if (content.Location(target).Experience == VoyageExperience.Harbour) return false;
            var route = VoyageRules.Route(content, rs.At, target);
            if (route == null) return false;
            rs.To = target;
            rs.JobTemplate = string.Empty;
            rs.ArriveDay = day + Math.Max(1, route.Count - 1);
            return true;
        }

        /// <summary>The job a crew takes from where it stands, by its temper: careful the nearest, bold the roughest, greedy
        /// the best paid. Null when nothing fits.</summary>
        public static VoyageJob Draw(VoyageState state, VoyageContent content, RivalCrew crew, RivalState rs, int day)
        {
            var here = content.Location(rs.At);
            if (here == null) return null;
            string newsPort = VoyageRules.IsPort(here) ? here.Id : VoyageRules.NearestPort(content, here.Id)?.Id;
            if (newsPort == null) return null;
            var ledger = state.Ledger(newsPort);
            var options = VoyageRules.Candidates(state, content, here, ledger, crew.AsPerson(), null, null, forRival: true);
            if (options.Count == 0) return null;
            VoyageJob best = null;
            double bestScore = double.MinValue;
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                double score = crew.Temper switch
                {
                    RivalTemper.Bold => o.Intensity * 10 - o.Hops,
                    RivalTemper.Greedy => o.Pay * 10 - o.Hops,
                    _ => -o.Hops * 10 - o.Intensity,
                };
                // Ties: a stable draw, so the same galaxy sails the same way on every machine.
                score += VoyageRules.Roll(day, crew.Id, o.TemplateId + o.To);
                if (score > bestScore) { bestScore = score; best = o; }
            }
            return best;
        }

        /// <summary>A crew arrives where it was bound and resolves its job against the place.</summary>
        static void Arrive(VoyageState state, VoyageContent content, RivalCrew crew, RivalState rs, int day)
        {
            var w = content.World ?? new VoyageWorldRules();
            string from = rs.At;
            rs.At = rs.To;
            rs.To = string.Empty;
            var t = content.Job(rs.JobTemplate);
            rs.JobTemplate = string.Empty;
            var loc = content.Location(rs.At);
            if (t == null || loc == null) return;
            var place = state.Place(loc.Id);

            var route = VoyageRules.Route(content, from, rs.At);
            int intensity = VoyageRules.Intensity(state, content, route);
            double chance = w.RivalSuccessBase + w.RivalStrengthWeight * rs.Strength - w.RivalIntensityWeight * (intensity - 1)
                            - w.RivalPressureWeight * place.Pressure;
            chance = Math.Clamp(chance, 0.05, 0.95);
            bool ok = VoyageRules.Roll(day, crew.Id, "resolve") < chance;

            var job = new VoyageJob
            {
                TemplateId = t.Id, Kind = t.Kind, PilotId = crew.Id, From = from, To = rs.At, Verb = t.Verb, Goal = t.Goal,
                Source = SourceNews(state, t.Needs, rs.At), Hops = Math.Max(1, (route?.Count ?? 2) - 1), Intensity = intensity,
            };
            if (!ok)
            {
                place.Pressure = Math.Clamp(place.Pressure + w.FailurePressure, 0f, 3f);
                rs.Deeds++;
                if (intensity >= 2 && VoyageRules.Roll(day, crew.Id, "wreck") < w.RivalWreckChance) Fall(state, content, crew, rs, loc, day);
                return;
            }

            // The work's own mark on the world: a find taken, a place surveyed.
            if (t.Verb == WorkVerb.Stow) TakeFind(state, content, loc, place, rs, day);
            if (t.GoesUncharted || job.Source?.Tag == NewsTag.Chart) place.KnownToOthers = true;
            var ev = content.Event(t.OnSuccess);
            if (ev != null) VoyageRules.Apply(state, content, ev, job, null);
            rs.Notoriety += t.NotorietyDelta;
            rs.Deeds++;
        }

        static NewsItem SourceNews(VoyageState state, NewsTag tag, string about)
        {
            if (tag == NewsTag.None) return null;
            foreach (var l in state.Ledgers)
            {
                var n = l.Find(tag, about);
                if (n != null) return n;
            }
            return new NewsItem { Tag = tag, About = about, Day = state.Day };
        }

        /// <summary>A crew takes one find here: a sealed shell's artifact, or one from a wreck.</summary>
        static void TakeFind(VoyageState state, VoyageContent content, VoyageLocation loc, LocationState place, RivalState rs, int day)
        {
            if (VoyageRules.HasSealedFinds(loc) && loc.Finds is { Count: > 0 })
            {
                if (place.Sealed < 0) place.Sealed = loc.Vaults;
                if (place.Sealed > 0)
                {
                    place.Sealed--;
                    rs.Hold.Add(loc.Finds[VoyageRules.Mod(VoyageRules.Hash(day, loc.Id, rs.Id), loc.Finds.Count)]);
                    if (place.Sealed == 0 && place.RestockDay <= 0)
                        place.RestockDay = day + Math.Max(1, loc.RestockDays > 0 ? loc.RestockDays : content.World?.RestockDays ?? 4);
                }
                return;
            }
            var wreck = state.WreckAt(loc.Id);
            if (wreck == null) return;
            rs.Hold.Add(wreck.Artifacts[0]);
            wreck.Artifacts.RemoveAt(0);
            if (wreck.Artifacts.Count == 0)
            {
                state.Wrecks.Remove(wreck);
                VoyageWorld.ForgetNews(state, NewsTag.Wreck, wreck.At);
            }
        }

        /// <summary>A crew falls at <paramref name="where"/>: what it held lies there as a wreck, news at the nearest port;
        /// it is off the sea for the world's respawn days, then sails again from home.</summary>
        static void Fall(VoyageState state, VoyageContent content, RivalCrew crew, RivalState rs, VoyageLocation where, int day)
        {
            var w = content.World ?? new VoyageWorldRules();
            if (rs.Hold.Count > 0)
            {
                var wreck = state.WreckAt(where.Id);
                if (wreck == null)
                {
                    wreck = new WreckRecord { At = where.Id, Line = VoyageRules.Fill(content.WreckLine, where, where, where, null, crew.Name), Day = day };
                    state.Wrecks.Add(wreck);
                }
                wreck.Artifacts.AddRange(rs.Hold);
                rs.Hold.Clear();
            }
            var port = VoyageRules.NearestPort(content, where.Id);
            if (port != null)
                VoyageWorld.Post(state, port, NewsTag.Wreck, where,
                    VoyageRules.Fill(content.Words?.RivalFellNews, where, where, where, null, crew.Name), day, onlyIfUnknown: true, rival: crew.Id);
            rs.WreckedDay = day;
            rs.ReturnDay = day + Math.Max(1, w.RivalRespawnDays);
            rs.To = string.Empty;
            rs.JobTemplate = string.Empty;
            state.Log.RivalsWrecked++;
            VoyageWorld.ForgetNews(state, NewsTag.Bounty, rs.At);
        }

        /// <summary>A notorious crew is wanted at every port, where it stands now; the word is refreshed daily.</summary>
        static void PostBounties(VoyageState state, VoyageContent content, int day)
        {
            var w = content.World ?? new VoyageWorldRules();
            foreach (var crew in content.Rivals ?? new List<RivalCrew>())
            {
                if (crew == null) continue;
                var rs = state.Rival(crew.Id);
                if (rs == null) continue;
                foreach (var l in state.Ledgers) l.News.RemoveAll(n => n != null && n.Tag == NewsTag.Bounty && n.Rival == crew.Id);
                if (!rs.IsOut || rs.Notoriety < w.BountyAt) continue;
                var at = content.Location(rs.At);
                if (at == null) continue;
                string line = VoyageRules.Fill(content.Words?.BountyOnRival, at, at, at, null, crew.Name);
                foreach (var p in content.Locations)
                    if (VoyageRules.IsPort(p)) VoyageWorld.Post(state, p, NewsTag.Bounty, at, line, day, onlyIfUnknown: false, rival: crew.Id);
            }
        }

        /// <summary>A crew with a grudge can be parleyed with: an accord to carry to its home port (the diplomats' work).</summary>
        static void PostAccords(VoyageState state, VoyageContent content, int day)
        {
            foreach (var crew in content.Rivals ?? new List<RivalCrew>())
            {
                if (crew == null) continue;
                var rs = state.Rival(crew.Id);
                var home = content.Location(crew.Home);
                if (rs == null || home == null) continue;
                if (rs.Grudge < 2 || !rs.IsOut)
                {
                    foreach (var l in state.Ledgers) l.News.RemoveAll(n => n != null && n.Tag == NewsTag.Accord && n.Rival == crew.Id);
                    continue;
                }
                var port = VoyageRules.NearestPort(content, state.AtSea ? state.LastPort : state.At);
                if (port == null) continue;
                string line = VoyageRules.Fill(content.Words?.RelationHostile, home, home, home, null, crew.Name);
                VoyageWorld.Post(state, port, NewsTag.Accord, home, line, day, onlyIfUnknown: true, rival: crew.Id);
            }
        }

        // ── The squad and a rival, face to face ─────────────────────────────

        /// <summary>The squad stowed a find where a racing rival stands: the rival remembers (once a day).</summary>
        internal static void OnPlayerStowed(VoyageState state, VoyageContent content, string here)
        {
            var loc = content.Location(here);
            foreach (var rs in StandingAt(state, content, here))
            {
                var crew = content.Rival(rs.Id);
                if (crew == null || rs.GrudgeDay == state.Day) continue;
                if (Relation(state, content, crew, rs, loc) != RivalRelation.Race) continue;
                rs.Grudge = Math.Min(3, rs.Grudge + 1);
                rs.GrudgeDay = state.Day;
            }
        }

        /// <summary>A racing rival opened a shell before the squad: the find is theirs. False when nothing stood to open.</summary>
        public static bool RivalOpens(VoyageState state, VoyageContent content, string rivalId, VoyageRules.Resolution result)
        {
            var crew = content.Rival(rivalId);
            var rs = state.Rival(rivalId);
            var loc = content.Location(state.At);
            if (crew == null || rs == null || loc == null || !VoyageRules.HasSealedFinds(loc) || loc.Finds is not { Count: > 0 }) return false;
            var place = state.Place(loc.Id);
            if (place.Sealed < 0) place.Sealed = loc.Vaults;
            if (place.Sealed <= 0) return false;
            TakeFind(state, content, loc, place, rs, state.Day);
            VoyageRules.Say(state, result, VoyageRules.Fill(content.Words?.RivalRace, loc, loc, loc, null, crew.Name));
            return true;
        }

        /// <summary>
        /// The squad's Ark fell where a hostile crew stood: that crew is credited with everything aboard, promoted a
        /// strength, and remembered as this crew's nemesis (DEPTH.md §3). False when no hostile crew stands there.
        /// </summary>
        internal static bool CreditFall(VoyageState state, VoyageContent content, string where, IReadOnlyList<string> spilled,
            VoyageRules.Resolution result)
        {
            var loc = content.Location(where);
            foreach (var rs in StandingAt(state, content, where))
            {
                var crew = content.Rival(rs.Id);
                if (crew == null || Relation(state, content, crew, rs, loc) != RivalRelation.Hostile) continue;
                if (spilled != null) rs.Hold.AddRange(spilled);
                rs.Strength = Math.Min(3, rs.Strength + 1);
                rs.Notoriety++;
                rs.Grudge = 0;
                rs.Deeds++;
                state.Log.Nemesis = crew.Id;
                state.Log.NemesisDay = state.Day;
                VoyageRules.Say(state, result, VoyageRules.Fill(content.Words?.Nemesis, loc, loc, loc, null, crew.Name));
                return true;
            }
            return false;
        }

        /// <summary>A bounty's work done: the crew is caught where it stands, what it held spills as a wreck there, its
        /// notoriety is spent, and it is off the sea for a while; it will remember.</summary>
        internal static void Catch(VoyageState state, VoyageContent content, RivalCrew crew, string where, VoyageRules.Resolution result)
        {
            var rs = state.Rival(crew.Id);
            var loc = content.Location(where);
            if (rs == null || loc == null || !rs.IsOut) return;
            var w = content.World ?? new VoyageWorldRules();
            if (rs.Hold.Count > 0)
            {
                var wreck = state.WreckAt(loc.Id);
                if (wreck == null)
                {
                    wreck = new WreckRecord { At = loc.Id, Line = VoyageRules.Fill(content.WreckLine, loc, loc, loc, null, crew.Name), Day = state.Day };
                    state.Wrecks.Add(wreck);
                }
                wreck.Artifacts.AddRange(rs.Hold);
                rs.Hold.Clear();
                var port = VoyageRules.NearestPort(content, loc.Id);
                if (port != null)
                    VoyageWorld.Post(state, port, NewsTag.Wreck, loc, VoyageRules.Fill(content.WreckNewsLine, loc, loc, loc, null, crew.Name), state.Day);
            }
            rs.WreckedDay = state.Day;
            rs.ReturnDay = state.Day + Math.Max(1, w.RivalRespawnDays);
            rs.Notoriety = 0;
            rs.Grudge = Math.Min(3, rs.Grudge + 1);
            rs.To = string.Empty;
            rs.JobTemplate = string.Empty;
            state.Log.RivalsCaught++;
            foreach (var l in state.Ledgers) l.News.RemoveAll(n => n != null && n.Tag == NewsTag.Bounty && n.Rival == crew.Id);
        }
    }
}
