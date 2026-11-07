#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The depth pass's rules (Docs/Voyages/DEPTH.md): the living galaxy, the rival crews, the work verbs, sagas and
    /// relics, standing and notoriety, charting, the warnings, a record from before the pass, and the scorecard itself
    /// on the shipped content (<see cref="VoyageDepthHarness"/>). The shipped content is read from the twin the
    /// generator writes beside the spec, so these tests hold what ships.
    /// </summary>
    [TestFixture]
    public class VoyageDepthTests
    {
        static VoyageContent Shipped() => VoyageDepthHarness.LoadShippedContent();

        static string PilotOf(VoyageContent c, FactionType type) => c.Crew.First(p => p.Faction == type).PilotId;

        static VoyageJob Offered(VoyageState s, VoyageContent c, string pilot, string templateId, string to = null) =>
            VoyageRules.Board(s, c, new[] { pilot }).FirstOrDefault(j => j.TemplateId == templateId && (to == null || j.To == to));

        /// <summary>Every job a pilot could bring from where the Ark is (the board shows one per pilot).</summary>
        static VoyageJob Candidate(VoyageState s, VoyageContent c, string pilot, string templateId, string to = null)
        {
            var here = c.Location(s.At);
            var ledger = s.Ledger(VoyageRules.IsPort(here) ? here.Id : s.LastPort);
            return VoyageRules.Candidates(s, c, here, ledger, c.Person(pilot), null, null, forRival: false)
                .FirstOrDefault(j => j.TemplateId == templateId && (to == null || j.To == to));
        }

        /// <summary>Take a job and arrive, then resolve it as done (stowing what stands there when it asks a stow).</summary>
        static VoyageRules.Resolution Sail(VoyageState s, VoyageContent c, VoyageJob job)
        {
            Assert.IsTrue(VoyageRules.Take(s, c, job), "the job is taken from where the Ark is");
            var arrival = VoyageRules.Arrive(s, c);
            Assert.IsNotNull(arrival);
            if (job.Verb == WorkVerb.Stow)
            {
                var finds = arrival.WreckFinds.Concat(arrival.VaultFinds).Take((int)job.Goal).ToList();
                Assert.IsNotEmpty(finds, $"{job.To} has something to stow");
                foreach (var id in finds) VoyageRules.Stowed(s, c, id);
            }
            return VoyageRules.Resolve(s, c, JobOutcome.Succeeded);
        }

        [Test]
        public void TheShippedContentValidatesAndItsTextIsAuthored()
        {
            var c = Shipped();
            CollectionAssert.IsEmpty(c.Validate());
            CollectionAssert.IsEmpty(VoyageDepthHarness.TextProblems(c));
            Assert.GreaterOrEqual(c.Locations.Count, 24, "a galaxy worth sailing");
            Assert.GreaterOrEqual(c.Rivals.Count, 8);
            Assert.GreaterOrEqual(c.Sagas.Count, 4);
            Assert.AreEqual(16, c.Crew.Select(p => p.Faction).Distinct().Count(), "every faction type has a person on the authored crew");
        }

        [Test]
        public void EveryFactionTypeBringsWorkFromEveryHarbourOnDayZero()
        {
            var c = Shipped();
            foreach (var home in new[] { Domains.Jade, Domains.Ruby, Domains.Gold })
            {
                var s = VoyageRules.NewGalaxy(c, home);
                var here = c.Location(s.At);
                var ledger = s.Ledger(s.At);
                var missing = new List<string>();
                foreach (var person in c.Crew)
                    if (VoyageRules.Candidates(s, c, here, ledger, person, null, null, forRival: false).Count == 0)
                        missing.Add($"{person.Faction} ({person.Name})");
                CollectionAssert.IsEmpty(missing, $"from {here.Name} every pilot card brings work");
            }
        }

        [Test]
        public void TheBoardLeadsWithTheSeatedPilotsAndFillsFromTheCrew()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var crew = c.Crew.Select(p => p.PilotId).ToList();
            var seated = new[] { PilotOf(c, FactionType.Hunter), PilotOf(c, FactionType.Merchant), PilotOf(c, FactionType.Priest) };
            var board = VoyageRules.Board(s, c, seated, crew);
            Assert.AreEqual(c.World.BoardSize, board.Count, "about six options at a port (the 2026-10-30 meeting)");
            Assert.AreEqual(FactionType.Hunter, c.Job(board[0].TemplateId).Faction, "the first seat's pilot leads");
            Assert.AreEqual(FactionType.Merchant, c.Job(board[1].TemplateId).Faction);
            Assert.AreEqual(FactionType.Priest, c.Job(board[2].TemplateId).Faction);
            Assert.AreEqual(board.Count, board.Select(j => j.TemplateId + j.To).Distinct().Count(), "no two cards are the same job to the same place");
            foreach (var j in board)
            {
                Assert.Greater(j.Pay, 0, "every job is priced");
                Assert.That(j.Intensity, Is.InRange(1, 4));
            }
        }

        [Test]
        public void TheGalaxyIsDeterministic()
        {
            var c = Shipped();
            var a = VoyageRules.NewGalaxy(c, Domains.Ruby);
            var b = VoyageRules.NewGalaxy(c, Domains.Ruby);
            VoyageWorld.TickDays(a, c, 12);
            VoyageWorld.TickDays(b, c, 12);
            Assert.AreEqual(UnityEngine.JsonUtility.ToJson(a), UnityEngine.JsonUtility.ToJson(b), "the same galaxy on the same day is the same everywhere");
        }

        [Test]
        public void RivalsSailAndTheirWorkWritesTheNews()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Gold);
            Assert.AreEqual(c.Rivals.Count, s.Rivals.Count, "every rival crew is on the sea from the start");
            VoyageWorld.TickDays(s, c, 20);
            Assert.Greater(s.Rivals.Sum(r => r.Deeds), 0, "rivals resolve work");
            Assert.IsTrue(s.Rivals.Any(r => r.At != c.Rival(r.Id).Home || !string.IsNullOrEmpty(r.To)), "rivals move");
            int news = s.Ledgers.Sum(l => l.News.Count);
            Assert.Greater(news, c.Seeds.Count, "the galaxy has more news than it was seeded with: the world moved without the player");
        }

        [Test]
        public void TheKaijuIsOneLitBeaconThatIsNeverEasierAgain()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            Assert.IsTrue(s.Kaiju.Alive, "sighted from the first day");
            var lair = c.Location(s.Kaiju.At);
            Assert.AreEqual(VoyageExperience.Lair, lair.Experience);
            foreach (var port in c.Locations.Where(l => l.Experience is VoyageExperience.Port or VoyageExperience.Harbour))
                Assert.IsTrue(s.Ledger(port.Id).Knows(NewsTag.Kaiju, lair.Id), $"{port.Name} knows where it lairs");

            var hunter = PilotOf(c, FactionType.Hunter);
            s.AddStanding(FactionType.Hunter, 2);   // Known: the hunt is tier 1 work
            var hunt = Candidate(s, c, hunter, c.Kaiju.HuntJob);
            Assert.IsNotNull(hunt, "a Known hunter is offered the hunt");
            Assert.AreEqual(c.Kaiju.CullGoal, hunt.Goal, "tier 0 asks the base cull");
            Assert.AreEqual(WorkVerb.Cull, hunt.Verb);

            Assert.IsTrue(VoyageRules.Take(s, c, hunt));
            var arrival = VoyageRules.Arrive(s, c);
            Assert.IsTrue(arrival.KaijuHere);
            Assert.IsTrue(arrival.HostileWorld, "its lair starts under the other colour");
            Assert.Greater(arrival.PopulationScale, c.World.LairPopulation, "and it is thicker with its brood");
            var r = VoyageRules.Resolve(s, c, JobOutcome.Succeeded);
            Assert.IsFalse(s.Kaiju.Alive);
            Assert.AreEqual(1, s.Kaiju.Tier);
            Assert.AreEqual(1, s.Log.KaijuFelled);
            Assert.IsTrue(r.StoryLines.Any(l => l.Contains(c.Kaiju.Name)), "the fall is told");
            Assert.IsFalse(s.Ledgers.Any(l => l.News.Any(n => n.Tag == NewsTag.Kaiju)), "the beacon is out");

            VoyageWorld.TickDays(s, c, c.Kaiju.RespawnDays + 1);
            Assert.IsTrue(s.Kaiju.Alive, "sighted again");
            s.At = s.Home;
            var again = Candidate(s, c, hunter, c.Kaiju.HuntJob);
            Assert.IsNotNull(again);
            Assert.AreEqual(c.Kaiju.CullGoal + c.Kaiju.CullStep, again.Goal, "the ratchet: never easier than the hardest felled");
        }

        [Test]
        public void ASagaRunsAcrossThreePilotsAndEndsInARelicOnce()
        {
            var c = Shipped();
            var saga = c.SagaOf("saga.lantern");
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            Assert.IsTrue(s.Ledger(s.Home).News.Any(n => n.Tag == NewsTag.Chapter && n.Chapter == "ch.lantern.1"), "the first chapter is posted at home");

            var hunter = PilotOf(c, FactionType.Hunter);
            var ch1 = Offered(s, c, hunter, "job.hunt.sighting", "loc.lantern");
            Assert.IsNotNull(ch1);
            Assert.AreEqual("ch.lantern.1", ch1.Chapter, "a hunter is offered the chapter first");
            Sail(s, c, ch1);
            Assert.Contains("ch.lantern.1", s.ChaptersDone);
            Assert.IsTrue(s.Ledgers.Any(l => l.News.Any(n => n.Chapter == "ch.lantern.2")), "the next chapter is posted");

            var explorer = PilotOf(c, FactionType.Explorer);
            Assert.IsFalse(VoyageRules.IsVisible(s, c, "loc.keepers.rest"), "the keeper's rest is off the chart");
            var ch2 = VoyageRules.Board(s, c, new[] { explorer }).FirstOrDefault(j => j.Chapter == "ch.lantern.2");
            Assert.IsNotNull(ch2, "an explorer finds the second chapter from the news the crew knows");
            Sail(s, c, ch2);
            Assert.IsTrue(VoyageRules.IsVisible(s, c, "loc.keepers.rest"), "arriving charts it");

            var back = VoyageRules.Board(s, c, new[] { explorer }).First(j => j.IsReturn);
            Assert.IsTrue(VoyageRules.Take(s, c, back));
            VoyageRules.Arrive(s, c);
            VoyageRules.Resolve(s, c, JobOutcome.Succeeded);
            VoyageRules.Extract(s, c);
            var priest = PilotOf(c, FactionType.Priest);
            Assert.IsNull(VoyageRules.Board(s, c, new[] { priest }).FirstOrDefault(j => j.Chapter == "ch.lantern.3"),
                "a stranger priest is not trusted with the finale");
            s.AddStanding(FactionType.Priest, 2);
            var ch3 = VoyageRules.Board(s, c, new[] { priest }).FirstOrDefault(j => j.Chapter == "ch.lantern.3");
            Assert.IsNotNull(ch3, "a Known priest is");
            var r = Sail(s, c, ch3);
            string relicId = VoyageRules.RelicId(saga);
            Assert.AreEqual(relicId, r.RelicId);
            Assert.AreEqual(saga.Relic.Name, r.RelicName);
            Assert.Contains(relicId, s.Aboard, "the relic is aboard, at stake");
            Assert.IsTrue(UniqueId.IsUnique(relicId) && UniqueId.TemplateOf(relicId) == saga.Relic.Kind, "a unique artifact id of its kind");
            Assert.Contains(saga.Id, s.RelicsFound);

            var home = VoyageRules.Board(s, c, new[] { priest }).First(j => j.IsReturn);
            Assert.IsTrue(VoyageRules.Take(s, c, home));
            VoyageRules.Arrive(s, c);
            VoyageRules.Resolve(s, c, JobOutcome.Succeeded);
            var x = VoyageRules.Extract(s, c);
            Assert.AreEqual(1, x.Relics.Count);
            Assert.AreEqual(saga.Relic.Name, x.Relics[0].Name);
            Assert.AreEqual(1, s.Log.Relics);

            VoyageWorld.TickDays(s, c, 10);
            Assert.IsFalse(s.Ledgers.Any(l => l.News.Any(n => n.Tag == NewsTag.Chapter && n.Chapter.StartsWith("ch.lantern"))),
                "a finished saga is never posted again");
        }

        [Test]
        public void StandingRisesWithWorkAndRaisesPay()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var merchant = PilotOf(c, FactionType.Merchant);
            var first = Offered(s, c, merchant, "job.courier.dispatch");
            Assert.AreEqual(0, VoyageRules.Tier(s, c, FactionType.Merchant));
            Sail(s, c, first);
            Assert.AreEqual(1, s.StandingOf(FactionType.Merchant));
            s.AddStanding(FactionType.Merchant, 4);
            Assert.AreEqual(2, VoyageRules.Tier(s, c, FactionType.Merchant), "Trusted");
            var t = c.Job("job.courier.dispatch");
            int stranger = VoyageRules.Pay(s, c, t, 1, 1, 0, null);
            int trusted = VoyageRules.Pay(s, c, t, 1, 1, 2, null);
            int rough = VoyageRules.Pay(s, c, t, 4, 3, 0, null);
            Assert.Greater(trusted, stranger, "trust pays");
            Assert.Greater(rough, stranger, "the far and the rough pay more");
        }

        [Test]
        public void NotorietyPutsAPriceOnTheArkAndItFades()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Ruby);
            s.Notoriety = c.World.BountyAt;
            VoyageWorld.TickDays(s, c, 1);
            foreach (var port in c.Locations.Where(l => l.Experience is VoyageExperience.Port or VoyageExperience.Harbour))
                Assert.IsTrue(s.Ledger(port.Id).News.Any(n => n.Tag == NewsTag.Bounty && n.Rival == VoyageRules.SelfRival), $"{port.Name} posts the price");
            var hunter = PilotOf(c, FactionType.BountyHunter);
            Assert.IsNull(Offered(s, c, hunter, "job.bounty.crew"), "the crew's own bounty is never its work");
            var job = VoyageRules.Board(s, c, new[] { hunter }).First(j => !j.IsReturn);
            Assert.IsTrue(job.Warnings.Any(w => w == c.Words.WarningBounty), "and every card warns of it");
            VoyageWorld.TickDays(s, c, c.World.NotorietyDecayDays * c.World.BountyAt + 1);
            Assert.AreEqual(0, s.Notoriety, "notoriety fades");
            Assert.IsFalse(s.Ledgers.Any(l => l.News.Any(n => n.Tag == NewsTag.Bounty && n.Rival == VoyageRules.SelfRival)), "and the price is withdrawn");
        }

        [Test]
        public void ALadenArkIsDockTalkAndDrawsPirates()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            // The Ark sits at Lantern Reach with two finds aboard; the nearest port hears of it.
            s.At = "loc.lantern";
            s.Aboard.Add("artifact.hydra");
            s.Aboard.Add("artifact.aftershock");
            VoyageWorld.TickDays(s, c, 1);
            var port = VoyageRules.NearestPort(c, s.At);
            Assert.IsTrue(s.Ledger(port.Id).News.Any(n => n.Tag == NewsTag.Rumour && n.Rival == VoyageRules.SelfRival), "the rumour is posted");
            var any = VoyageRules.Board(s, c, new[] { PilotOf(c, FactionType.Hunter) }).First();
            Assert.IsTrue(any.Warnings.Any(w => w.Contains("2")), "the card says what is at stake");

            // A pirate crew standing at that port hears it and sails for the Ark.
            var pirate = c.Rivals.First(r => r.Faction == FactionType.Pirate);
            var rs = s.Rival(pirate.Id);
            rs.At = port.Id; rs.To = string.Empty; rs.JobTemplate = string.Empty; rs.WreckedDay = -1;
            VoyageWorld.TickDays(s, c, 1);
            Assert.AreEqual(s.At, rs.To, "the pirates are bound for the laden Ark");
            VoyageWorld.TickDays(s, c, rs.ArriveDay - s.Day + 1);
            var arrival = VoyageRules.Describe(s, c);
            var meeting = arrival.Rivals.FirstOrDefault(m => m.Crew.Id == pirate.Id);
            Assert.IsNotNull(meeting, "they stand where the Ark is");
            Assert.AreEqual(RivalRelation.Hostile, meeting.Relation);
            Assert.IsNotEmpty(meeting.Line);

            // The Ark falls to them: they are credited, promoted and remembered.
            int strength = rs.Strength;
            var aboard = new List<string>(s.Aboard);
            var r = VoyageRules.ArkLost(s, c, s.At, taken: true, spilled: aboard);
            CollectionAssert.IsSubsetOf(aboard, rs.Hold, "everything aboard is theirs");
            Assert.AreEqual(System.Math.Min(3, strength + 1), rs.Strength, "a crew that takes an Ark is promoted");
            Assert.AreEqual(pirate.Id, s.Log.Nemesis);
            Assert.IsTrue(r.StoryLines.Any(l => l.Contains(pirate.Name)));
            Assert.IsNull(s.WreckAt("loc.lantern"), "nothing spills when raiders take it");
        }

        [Test]
        public void ARaceIsARaceAndTheLoserRemembers()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var lodge = c.Rivals.First(r => r.Faction == FactionType.Hunter);
            var rs = s.Rival(lodge.Id);
            // The lodge is bound for Lantern Reach and arrives the day the squad does.
            rs.At = "loc.tollmoor"; rs.To = "loc.lantern"; rs.ArriveDay = s.Day + 1; rs.JobTemplate = string.Empty;
            var hunt = Candidate(s, c, PilotOf(c, FactionType.Hunter), "job.hunt.sighting", "loc.lantern");
            Assert.IsNotNull(hunt);
            Assert.IsTrue(VoyageRules.Take(s, c, hunt));
            var arrival = VoyageRules.Arrive(s, c);
            var meeting = arrival.Rivals.First(m => m.Crew.Id == lodge.Id);
            Assert.AreEqual(RivalRelation.Race, meeting.Relation);
            int shells = arrival.VaultFinds.Count;
            Assert.IsTrue(VoyageRivals.RivalOpens(s, c, lodge.Id, null), "the rival opens a shell");
            Assert.AreEqual(1, rs.Hold.Count);
            Assert.AreEqual(shells - 1, s.Place("loc.lantern").Sealed);
            VoyageRules.Stowed(s, c, arrival.VaultFinds[0]);
            Assert.AreEqual(1, rs.Grudge, "beaten to a find, the rival remembers");
            Assert.AreEqual(shells - 2, s.Place("loc.lantern").Sealed);
        }

        [Test]
        public void APickedFieldRestocksAndTheRestockIsNews()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Gold);
            var hunt = Candidate(s, c, PilotOf(c, FactionType.Hunter), "job.hunt.sighting", "loc.hollow");
            Assert.IsNotNull(hunt, "the seeded sighting at the Hollow is a hunter's candidate");
            Assert.IsTrue(VoyageRules.Take(s, c, hunt));
            var arrival = VoyageRules.Arrive(s, c);
            foreach (var id in arrival.VaultFinds) VoyageRules.Stowed(s, c, id);
            VoyageRules.Resolve(s, c, JobOutcome.Succeeded);
            var place = s.Place("loc.hollow");
            Assert.AreEqual(0, place.Sealed, "picked clean");
            Assert.Greater(place.RestockDay, s.Day);
            Assert.IsNull(VoyageRules.Board(s, c, new[] { PilotOf(c, FactionType.Hunter) }).FirstOrDefault(j => j.To == "loc.hollow" && j.Verb == WorkVerb.Stow),
                "no hunt is offered to a picked field");
            VoyageWorld.TickDays(s, c, c.Location("loc.hollow").RestockDays + 1);
            Assert.AreEqual(-1, place.Sealed, "lit again");
            Assert.IsTrue(VoyageRules.NearestPort(c, "loc.hollow") is { } port && s.Ledger(port.Id).Knows(NewsTag.Sighting, "loc.hollow"),
                "and the restock is a sighting at the nearest port");
        }

        [Test]
        public void UnchartedPlacesAreHiddenUntilFound()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            Assert.IsFalse(VoyageRules.IsVisible(s, c, "loc.pale.anchorage"));
            Assert.IsTrue(VoyageRules.IsVisible(s, c, "loc.ninewells"));
            Assert.IsNull(VoyageRules.Board(s, c, new[] { PilotOf(c, FactionType.FirstResponder) }).FirstOrDefault(j => j.To == "loc.pale.anchorage"),
                "no work sends a crew to a place it has not found");
            var explorer = PilotOf(c, FactionType.Explorer);
            var survey = VoyageRules.Board(s, c, new[] { explorer }).First(j => c.Job(j.TemplateId).GoesUncharted || j.Source?.Tag == NewsTag.Chart);
            Assert.IsTrue(c.Location(survey.To).Uncharted);
            var r = Sail(s, c, survey);
            Assert.IsTrue(VoyageRules.IsVisible(s, c, survey.To), "charted by sailing there");
            Assert.AreEqual(1, s.Log.Charted);
            Assert.IsTrue(s.Story.Any(e => e.Line == VoyageRules.Fill(c.Words.Charted, null, c.Location(survey.To), null, null)));
        }

        [Test]
        public void WorkIsOneSwitchOverTheVerbs()
        {
            var job = new VoyageJob { Verb = WorkVerb.Gather, Goal = 12 };
            Assert.IsFalse(VoyageRules.WorkDone(job, new VoyageRules.VoyageTally { Gathered = 11 }));
            Assert.IsTrue(VoyageRules.WorkDone(job, new VoyageRules.VoyageTally { Gathered = 12 }));
            job = new VoyageJob { Verb = WorkVerb.Hold, Goal = 45, NoKill = true };
            Assert.IsTrue(VoyageRules.WorkDone(job, new VoyageRules.VoyageTally { HeldSeconds = 50 }));
            Assert.IsFalse(VoyageRules.WorkDone(job, new VoyageRules.VoyageTally { HeldSeconds = 50, Kills = 1 }), "a kill undoes a tend");
            job = new VoyageJob { Verb = WorkVerb.Arrive, HullFloor = 0.6f };
            Assert.IsFalse(VoyageRules.DoneOnArrival(job), "a convoy is not done by arriving alone");
            Assert.IsFalse(VoyageRules.WorkDone(job, new VoyageRules.VoyageTally { HullFraction = 0.5f }));
            Assert.IsTrue(VoyageRules.WorkDone(job, new VoyageRules.VoyageTally { HullFraction = 0.7f }));
            job = new VoyageJob { Verb = WorkVerb.Meet };
            Assert.IsTrue(VoyageRules.WorkDone(job, new VoyageRules.VoyageTally { MetRival = true }));
            job = new VoyageJob { Verb = WorkVerb.Raze, Goal = 1500 };
            Assert.AreEqual(0.5f, VoyageRules.Progress(job, new VoyageRules.VoyageTally { Razed = 750 }), 1e-4f);
            // Legacy fixtures that name only a kind still ask what the kind always asked.
            Assert.AreEqual(WorkVerb.Stow, VoyageRules.EffectiveVerb(new JobTemplate { Kind = JobKind.Hunt }));
            Assert.AreEqual(WorkVerb.Hold, VoyageRules.EffectiveVerb(new JobTemplate { Kind = JobKind.Defence }));
            Assert.AreEqual(WorkVerb.Arrive, VoyageRules.EffectiveVerb(new JobTemplate { Kind = JobKind.Courier }));
        }

        [Test]
        public void AThiefLiftsNewsAndACourierDeliversIt()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var thief = PilotOf(c, FactionType.Thief);
            var lift = Offered(s, c, thief, "job.lift.dispatch");
            Assert.IsNotNull(lift);
            var target = c.Location(lift.To);
            Assert.AreEqual(VoyageExperience.Harbour, target.Experience);
            Assert.AreNotEqual(Domains.Jade, target.Home, "never the crew's own harbour");
            Assume.That(s.Ledger(target.Id).News.Count, Is.GreaterThan(0), "the rival harbour knows something");
            Sail(s, c, lift);
            Assert.AreEqual(1, s.Carrying.Count, "the freshest news rides aboard");
            var lifted = s.Carrying[0];
            Assert.IsFalse(s.Ledger(target.Id).Knows(lifted.Tag, lifted.About), "and left the harbour's ledger");
            Assert.AreEqual(1, s.Notoriety, "which is notorious work");
            var home = VoyageRules.Board(s, c, new[] { thief }).First(j => j.IsReturn);
            Assert.IsTrue(VoyageRules.Take(s, c, home));
            var arrival = VoyageRules.Arrive(s, c);
            Assert.AreEqual(1, arrival.Delivered.Count, "delivered at the next port");
            Assert.AreEqual(0, s.Carrying.Count);
        }

        [Test]
        public void WarningsComeFromTheWorldAsItIs()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Ruby);
            s.Place("loc.verdigris").Pressure = 3f;
            var job = Offered(s, c, PilotOf(c, FactionType.Aristocrat), "job.defence.aid", "loc.verdigris");
            Assert.IsNotNull(job);
            Assert.IsTrue(job.Warnings.Contains(VoyageRules.Fill(c.Words.WarningPressure, null, c.Location("loc.verdigris"), null, null)));
            Assert.GreaterOrEqual(job.Intensity, 3, "pressure is weather: the course reads rougher");
            var lane = VoyageRules.CorridorProfile(s, c, job.From, job.To);
            Assert.AreEqual(2, lane.Count, "one open cell and the destination's own world");
            Assert.Greater(lane[^1], 1f, "a core at pressure is thicker with waves");
        }

        [Test]
        public void TheLegProfileMakesTheMiddleCellsHardest()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var profile = VoyageRules.CorridorProfile(s, c, "loc.tollmoor", "loc.gullbone");
            Assert.AreEqual(5, profile.Count, "four open cells and the port");
            Assert.Greater(profile[1], profile[0]);
            Assert.Greater(profile[3], profile[2]);
            Assert.AreEqual(1.4f, profile[3], 1e-4f, "cell 4 is the hardest (the 2026-10-30 meeting)");
        }

        [Test]
        public void AnOldRecordIsRepairedAndFilledIn()
        {
            var c = Shipped();
            var s = new VoyageState { Day = 9, Home = "loc.harbour.jade", At = "loc.tollmoor", LastPort = "loc.tollmoor",
                Places = null, Rivals = null, Standing = null, Carrying = null, Kaiju = null, Log = null,
                Current = new VoyageJob { TemplateId = "job.hunt.sighting", Kind = JobKind.Hunt, From = "loc.tollmoor", To = "loc.lantern" } };
            s.Normalize();
            Assert.AreEqual(WorkVerb.Stow, s.Current.Verb, "a saved hunt asks a stow");
            Assert.AreEqual(1f, s.Current.Goal);
            VoyageRules.Resume(s, c, Domains.Jade);
            Assert.AreEqual(c.Locations.Count, s.Places.Count);
            Assert.AreEqual(c.Rivals.Count, s.Rivals.Count);
            Assert.AreEqual(9, s.TickedDay, "the past is not ticked twice");
            Assert.IsNotNull(s.Kaiju);
            Assert.IsNotNull(s.Log);
        }

        [Test]
        public void DaysAwayTickTheGalaxyUpToTheMost()
        {
            var c = Shipped();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            s.SavedAtMs = 1_000_000_000_000L;
            int day = s.Day;
            VoyageRules.Resume(s, c, Domains.Jade, s.SavedAtMs + 3L * 86_400_000L);
            Assert.AreEqual(day + 3, s.Day, "three days away are three days on the sea");
            VoyageRules.Resume(s, c, Domains.Jade, s.SavedAtMs + 40L * 86_400_000L);
            Assert.AreEqual(day + 3 + c.World.MaxTicksAway, s.Day, "and never more than the world's most");
        }

        [Test]
        public void TheDepthScorecardHoldsItsBars()
        {
            var card = VoyageDepthHarness.Play(Shipped(), 60, 40);
            TestContext.WriteLine(card.Report());
            CollectionAssert.IsEmpty(card.Failures(), card.Report());
        }
    }
}
#endif
