using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The living galaxy (Docs/Voyages/DEPTH.md §2): what the HyperSea does by itself between the squad's jobs. Every
    /// day (a job taken, an escort berthed, a day away) each place drifts back toward its rest, blooms ripen, picked
    /// vault fields are lit again, unclaimed wrecks are grazed away, cores under pressure call for aid, the Kaiju is
    /// sighted and moves, the rival crews sail and resolve (<see cref="VoyageRivals"/>), sagas post their first chapter
    /// when a crew is ready for it, and word of a notorious or a laden Ark reaches the ports. Pure: every roll is a
    /// stable hash of the day and the thing rolled, so the same galaxy on the same day is the same everywhere.
    ///
    /// <para>"Nothing is minted" (VOYAGES.md §6.3): every piece of news written here is born from a state change.</para>
    /// </summary>
    public static class VoyageWorld
    {
        /// <summary>
        /// Every place, rival and tally the record needs, made if missing: a new galaxy's, or a record saved before the
        /// depth pass. Idempotent; cheap when nothing is missing. The Kaiju is sighted at the first beginning.
        /// </summary>
        public static void Begin(VoyageState state, VoyageContent content)
        {
            if (state == null || content == null) return;
            state.Normalize();
            foreach (var l in content.Locations)
            {
                if (l == null || string.IsNullOrEmpty(l.Id) || state.PlaceIfAny(l.Id) != null) continue;
                var place = state.Place(l.Id);
                place.Pressure = Math.Clamp(l.RestPressure, 0f, 3f);
                place.Bloom = Math.Clamp(l.RestBloom, 0, 3);
                place.Charted = !l.Uncharted;
                place.Sealed = -1;
            }
            foreach (var crew in content.Rivals ?? new List<RivalCrew>())
            {
                if (crew == null || string.IsNullOrEmpty(crew.Id) || state.Rival(crew.Id) != null) continue;
                state.Rivals.Add(new RivalState
                {
                    Id = crew.Id,
                    At = content.Location(crew.Home) != null ? crew.Home : state.Home,
                    To = string.Empty,
                    Strength = Math.Clamp(crew.Strength, 1, 3),
                    WreckedDay = -1,
                    GrudgeDay = -1,
                });
            }
            while (state.Standing.Count <= (int)FactionType.Diplomat) state.Standing.Add(0);
            var k = state.Kaiju;
            if (content.Kaiju is { Enabled: true } && !k.Alive && string.IsNullOrEmpty(k.At) && k.NextDay == 0 && k.Tier == 0)
                KaijuAppear(state, content, state.Day);
        }

        /// <summary>
        /// A new galaxy is already alive when the squad first looks at it: the day-zero pass posts what the sea knows
        /// (the stocked hoard, the pens, the distress calls, the blooms) and sets the rival crews moving, without
        /// advancing the day.
        /// </summary>
        public static void Awaken(VoyageState state, VoyageContent content)
        {
            if (state == null || content == null) return;
            Begin(state, content);
            TickDay(state, content, state.Day);
        }

        /// <summary>Ticks every day since the last tick, up to the record's day.</summary>
        public static void Tick(VoyageState state, VoyageContent content)
        {
            if (state == null || content == null) return;
            Begin(state, content);
            for (int day = state.TickedDay + 1; day <= state.Day; day++) TickDay(state, content, day);
            state.TickedDay = Math.Max(state.TickedDay, state.Day);
        }

        /// <summary>Passes <paramref name="days"/> days (time away from the game): each ticks the galaxy once.</summary>
        public static void TickDays(VoyageState state, VoyageContent content, int days)
        {
            if (state == null || content == null) return;
            for (int i = 0; i < days; i++)
            {
                state.Day++;
                Tick(state, content);
            }
        }

        /// <summary>One day of the galaxy. Order: the squad's notoriety fades; places drift, bloom, restock and call for
        /// help; wrecks are grazed; the Kaiju; the rivals; the sagas; word about the squad; old news is forgotten.</summary>
        internal static void TickDay(VoyageState state, VoyageContent content, int day)
        {
            var w = content.World ?? new VoyageWorldRules();
            var words = content.Words ?? new VoyageWords();
            var squadDomain = VoyageRules.SquadDomain(state, content);

            if (state.Notoriety > 0 && w.NotorietyDecayDays > 0 && day - state.NotorietyDay >= w.NotorietyDecayDays)
            {
                state.Notoriety--;
                state.NotorietyDay = day;
            }

            foreach (var l in content.Locations)
            {
                if (l == null) continue;
                var place = state.Place(l.Id);
                var port = VoyageRules.NearestPort(content, l.Id);

                // Pressure drifts back toward the place's rest.
                place.Pressure = Math.Clamp(place.Pressure + (l.RestPressure - place.Pressure) * Math.Clamp(w.PressureDrift, 0f, 1f), 0f, 3f);

                switch (l.Experience)
                {
                    case VoyageExperience.Nursery:
                        if (w.BloomDays > 0 && day % w.BloomDays == 0 && place.Bloom < 3) place.Bloom++;
                        if (place.Bloom >= 3) Post(state, port, NewsTag.Harvest, l, Fill(words.HarvestNews, l), day);
                        else ForgetNews(state, NewsTag.Harvest, l.Id);
                        break;
                    case VoyageExperience.VaultField:
                    case VoyageExperience.Hoard:
                        if (place.Sealed == 0 && place.RestockDay > 0 && day >= place.RestockDay)
                        {
                            place.Sealed = -1;
                            place.RestockDay = 0;
                            Post(state, port, l.Experience == VoyageExperience.Hoard ? NewsTag.Hoard : NewsTag.Sighting, l,
                                Fill(words.RestockNews, l), day);
                        }
                        else if (l.Experience == VoyageExperience.Hoard && place.Sealed != 0)
                            PostEverywhere(state, content, NewsTag.Hoard, l, Fill(l.Blurb, l), day);
                        break;
                    case VoyageExperience.Core:
                        if (place.Pressure >= w.AidPressure) Post(state, port, NewsTag.Aid, l, Fill(words.AidNews, l), day, onlyIfUnknown: true);
                        if (place.Pressure >= w.BreachPressure) Post(state, port, NewsTag.Breach, l, Fill(words.BreachNews, l), day, onlyIfUnknown: true);
                        else ForgetNews(state, NewsTag.Breach, l.Id);
                        if (place.HasHolder && place.Holder != squadDomain)
                            Post(state, port, NewsTag.Held, l, VoyageRules.Tokens(Fill(words.HeldNews, l), "holder", place.Holder.ToString()), day, onlyIfUnknown: true);
                        break;
                    case VoyageExperience.Lair:
                        if (place.Pressure >= w.AidPressure) Post(state, port, NewsTag.Lair, l, Fill(l.Blurb, l), day, onlyIfUnknown: true);
                        break;
                    case VoyageExperience.Pen:
                        if (place.Pressure >= 1f) PostEverywhere(state, content, NewsTag.Pen, l, Fill(l.Blurb, l), day);
                        else ForgetNews(state, NewsTag.Pen, l.Id);
                        break;
                    case VoyageExperience.Derelict:
                        if (place.Pressure >= 1f) PostEverywhere(state, content, NewsTag.Distress, l, Fill(l.Blurb, l), day);
                        else ForgetNews(state, NewsTag.Distress, l.Id);
                        break;
                }
            }

            // A port cut off by a core under pressure next to it posts a cargo contract (the merchants' convoy).
            foreach (var l in content.Locations)
            {
                if (!VoyageRules.IsPort(l) || l.Id == state.Home) continue;
                bool cutOff = false;
                foreach (var next in VoyageRules.Neighbours(content, l.Id))
                {
                    var n = content.Location(next);
                    if (n != null && n.Experience == VoyageExperience.Core && state.Place(n.Id).Pressure >= w.AidPressure) { cutOff = true; break; }
                }
                if (cutOff)
                    foreach (var p in content.Locations)
                        if (VoyageRules.IsPort(p) && p.Id != l.Id) Post(state, p, NewsTag.Contract, l, Fill(l.Blurb, l), day, onlyIfUnknown: true);
                else ForgetNews(state, NewsTag.Contract, l.Id);
            }

            // Unclaimed wrecks are grazed away by the food web.
            for (int i = state.Wrecks.Count - 1; i >= 0; i--)
            {
                var wreck = state.Wrecks[i];
                if (wreck == null || day - wreck.Day <= w.WreckDays) continue;
                state.Wrecks.RemoveAt(i);
                ForgetNews(state, NewsTag.Wreck, wreck.At);
            }

            TickKaiju(state, content, day);
            VoyageRivals.TickDay(state, content, day);
            StartSagas(state, content, day);
            WordOfTheSquad(state, content, day);

            foreach (var ledger in state.Ledgers) VoyageRules.Forget(ledger, day, content);
        }

        static string Fill(string template, VoyageLocation about) => VoyageRules.Fill(template, about, about, about, null);

        /// <summary>News at <paramref name="port"/> about <paramref name="about"/>; with <paramref name="onlyIfUnknown"/>
        /// nothing is written where the same news already stands.</summary>
        internal static void Post(VoyageState state, VoyageLocation port, NewsTag tag, VoyageLocation about, string line, int day,
            bool onlyIfUnknown = true, string rival = null)
        {
            if (port == null || about == null || tag == NewsTag.None) return;
            var ledger = state.Ledger(port.Id);
            if (onlyIfUnknown && ledger.Knows(tag, about.Id)) return;
            ledger.News.Add(new NewsItem { Tag = tag, About = about.Id, Line = line ?? string.Empty, Day = day, Rival = rival ?? string.Empty });
        }

        /// <summary>News every port and harbour hears (a distress call on every band, a cause, dock talk of a hoard).</summary>
        internal static void PostEverywhere(VoyageState state, VoyageContent content, NewsTag tag, VoyageLocation about, string line, int day)
        {
            foreach (var p in content.Locations)
                if (VoyageRules.IsPort(p)) Post(state, p, tag, about, line, day, onlyIfUnknown: true);
        }

        /// <summary>Every port forgets news of this tag about this place.</summary>
        public static void ForgetNews(VoyageState state, NewsTag tag, string about)
        {
            if (state?.Ledgers == null) return;
            foreach (var l in state.Ledgers)
                l.News?.RemoveAll(n => n != null && n.Tag == tag && n.About == about);
        }

        /// <summary>Every port forgets a chapter's news.</summary>
        public static void ForgetChapter(VoyageState state, string chapterId)
        {
            if (state?.Ledgers == null || string.IsNullOrEmpty(chapterId)) return;
            foreach (var l in state.Ledgers)
                l.News?.RemoveAll(n => n != null && n.Tag == NewsTag.Chapter && n.Chapter == chapterId);
        }

        // ── The Kaiju ───────────────────────────────────────────────────────

        static void TickKaiju(VoyageState state, VoyageContent content, int day)
        {
            var spec = content.Kaiju;
            if (spec is not { Enabled: true }) return;
            var k = state.Kaiju;
            if (!k.Alive)
            {
                if (k.NextDay > 0 && day >= k.NextDay) KaijuAppear(state, content, day);
                return;
            }
            if (day >= k.NextDay) KaijuMove(state, content, day);
        }

        static List<VoyageLocation> Lairs(VoyageState state, VoyageContent content, string notThis)
        {
            var charted = new List<VoyageLocation>();
            var any = new List<VoyageLocation>();
            foreach (var l in content.Locations)
            {
                if (l == null || l.Experience != VoyageExperience.Lair || l.Id == notThis) continue;
                any.Add(l);
                if (VoyageRules.IsVisible(state, content, l.Id)) charted.Add(l);
            }
            return charted.Count > 0 ? charted : any;
        }

        internal static void KaijuAppear(VoyageState state, VoyageContent content, int day)
        {
            var spec = content.Kaiju;
            var k = state.Kaiju;
            var lairs = Lairs(state, content, k.LastLair);
            if (lairs.Count == 0)
            {
                k.NextDay = day + Math.Max(1, spec.RespawnDays);
                return;
            }
            var lair = lairs[VoyageRules.Mod(VoyageRules.Hash(day, "kaiju", "appear"), lairs.Count)];
            k.At = lair.Id;
            k.Alive = true;
            k.Day = day;
            k.NextDay = day + Math.Max(1, spec.MoveDays);
            string line = VoyageRules.Tokens(Fill(spec.SightedLine, lair), "kaiju", spec.Name ?? string.Empty);
            foreach (var p in content.Locations)
                if (VoyageRules.IsPort(p)) Post(state, p, NewsTag.Kaiju, lair, line, day);
        }

        static void KaijuMove(VoyageState state, VoyageContent content, int day)
        {
            var spec = content.Kaiju;
            var k = state.Kaiju;
            var lairs = Lairs(state, content, k.At);
            if (lairs.Count == 0) { k.NextDay = day + Math.Max(1, spec.MoveDays); return; }
            ForgetNews(state, NewsTag.Kaiju, k.At);
            var lair = lairs[VoyageRules.Mod(VoyageRules.Hash(day, "kaiju", "move"), lairs.Count)];
            k.LastLair = k.At;
            k.At = lair.Id;
            k.Day = day;
            k.NextDay = day + Math.Max(1, spec.MoveDays);
            string line = VoyageRules.Tokens(Fill(spec.MovedLine, lair), "kaiju", spec.Name ?? string.Empty);
            foreach (var p in content.Locations)
                if (VoyageRules.IsPort(p)) Post(state, p, NewsTag.Kaiju, lair, line, day);
        }

        /// <summary>The Kaiju fell to the squad: it is gone for a while, and never easier again (DEPTH.md §3).</summary>
        public static void KaijuFelled(VoyageState state, VoyageContent content, VoyageRules.Resolution result)
        {
            var spec = content?.Kaiju;
            var k = state?.Kaiju;
            if (spec == null || k == null || !k.Alive) return;
            var lair = content.Location(k.At);
            ForgetNews(state, NewsTag.Kaiju, k.At);
            k.Alive = false;
            k.LastLair = k.At;
            k.At = string.Empty;
            k.Tier++;
            k.NextDay = state.Day + Math.Max(1, spec.RespawnDays);
            state.Log.KaijuFelled++;
            VoyageRules.Say(state, result, VoyageRules.Tokens(Fill(spec.FelledLine, lair), "kaiju", spec.Name ?? string.Empty,
                "tier", k.Tier.ToString()));
        }

        // ── Sagas ───────────────────────────────────────────────────────────

        /// <summary>A saga whose first chapter nobody has found yet is posted once its crew is ready (DEPTH.md §5).</summary>
        static void StartSagas(VoyageState state, VoyageContent content, int day)
        {
            foreach (var saga in content.Sagas ?? new List<Saga>())
            {
                if (saga?.Chapters == null || saga.Chapters.Count == 0 || state.RelicsFound.Contains(saga.Id)) continue;
                bool touched = false;
                foreach (var c in saga.Chapters)
                    if (c != null && (state.ChaptersOpen.Contains(c.Id) || state.ChaptersDone.Contains(c.Id))) { touched = true; break; }
                if (touched) continue;
                var first = saga.Chapters[0];
                if (first == null || day < saga.StartDay) continue;
                if (VoyageRules.Tier(state, content, first.Faction) < saga.StartTier) continue;
                string port = !string.IsNullOrEmpty(saga.StartPort) && content.Location(saga.StartPort) != null ? saga.StartPort : state.Home;
                PostChapter(state, content, saga, first, port);
            }
        }

        /// <summary>
        /// Posts a chapter's news, about where the chapter goes (a named place, or the nearest place of its kind to
        /// <paramref name="portId"/>). A saga is the crew's own story, so the chapter stands at every port and harbour:
        /// the crew reads it wherever it docks.
        /// </summary>
        public static void PostChapter(VoyageState state, VoyageContent content, Saga saga, SagaChapter chapter, string portId)
        {
            if (state == null || content == null || saga == null || chapter == null) return;
            if (state.ChaptersOpen.Contains(chapter.Id) || state.ChaptersDone.Contains(chapter.Id)) return;
            var port = content.Location(portId) ?? content.Location(state.Home);
            if (port == null) return;
            VoyageLocation about = !string.IsNullOrEmpty(chapter.At)
                ? content.Location(chapter.At)
                : VoyageRules.NearestOf(content, port.Id, chapter.AtExperience, port.Id);
            if (about == null) return;
            string line = VoyageRules.Tokens(VoyageRules.Fill(chapter.Line, port, about, about, null), "saga", saga.Name ?? string.Empty,
                "relic", saga.Relic?.Name ?? string.Empty);
            foreach (var p in content.Locations)
            {
                if (!VoyageRules.IsPort(p)) continue;
                state.Ledger(p.Id).News.Add(new NewsItem { Tag = NewsTag.Chapter, About = about.Id, Line = line, Day = state.Day, Chapter = chapter.Id });
            }
            state.ChaptersOpen.Add(chapter.Id);
        }

        // ── Word of the squad ───────────────────────────────────────────────

        /// <summary>A notorious crew has a price on it at every port; a laden Ark is dock talk at the nearest one
        /// (DEPTH.md §6-§7). Both are refreshed every day and withdrawn when they stop being true.</summary>
        static void WordOfTheSquad(VoyageState state, VoyageContent content, int day)
        {
            var w = content.World ?? new VoyageWorldRules();
            var words = content.Words ?? new VoyageWords();
            var home = content.Location(state.Home);

            foreach (var l in state.Ledgers)
                l.News.RemoveAll(n => n != null && n.Rival == VoyageRules.SelfRival && (n.Tag == NewsTag.Bounty || n.Tag == NewsTag.Rumour));

            if (state.Notoriety >= w.BountyAt && home != null)
            {
                string line = VoyageRules.Tokens(VoyageRules.Fill(words.BountyOnYou, home, home, home, null), "home", home.Name ?? string.Empty,
                    "n", state.Notoriety.ToString());
                foreach (var p in content.Locations)
                    if (VoyageRules.IsPort(p)) Post(state, p, NewsTag.Bounty, home, line, day, onlyIfUnknown: false, rival: VoyageRules.SelfRival);
            }
            if (!state.AtSea && state.Aboard.Count >= w.LadenRumourAt)
            {
                var at = content.Location(state.At);
                var port = VoyageRules.NearestPort(content, state.At);
                if (at != null && port != null)
                {
                    string line = VoyageRules.Tokens(VoyageRules.Fill(words.LadenRumour, at, at, at, null), "n", state.Aboard.Count.ToString());
                    Post(state, port, NewsTag.Rumour, at, line, day, onlyIfUnknown: false, rival: VoyageRules.SelfRival);
                }
            }
        }
    }
}
