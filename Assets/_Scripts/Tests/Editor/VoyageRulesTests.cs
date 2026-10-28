#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The voyage slice's rules (Docs/Voyages/SLICE.md §1, <see cref="VoyageRules"/>): the seated pilots decide the
    /// board, a job's outcome writes the news the next board is made of, couriers carry news between ports, a fallen
    /// Ark leaves a wreck someone can salvage, and docking at home extracts. Built on a small map of its own so each
    /// rule is visible; the shipped content is checked by <c>author_voyage_assets.py --check</c> and the walk below.
    /// </summary>
    [TestFixture]
    public class VoyageRulesTests
    {
        const string Hunter = "pilot.callsign.citadel";
        const string Merchant = "pilot.callsign.volt";
        const string Pirate = "pilot.callsign.galleon";
        const string Aristocrat = "pilot.callsign.ensign";
        const string Stranger = "pilot.callsign.nobody";

        static VoyageContent Map()
        {
            var c = new VoyageContent();
            void Loc(string id, VoyageExperience e, Domains home = Domains.Blue, int cell = 0)
            {
                var l = new VoyageLocation { Id = id, Name = id.ToUpperInvariant(), Experience = e, Home = home, Cell = cell };
                if (e == VoyageExperience.VaultField) { l.Vaults = 2; l.Finds = new List<string> { "artifact.a", "artifact.b" }; }
                if (e == VoyageExperience.Core) l.HoldSeconds = 30f;
                c.Locations.Add(l);
            }
            Loc("jade", VoyageExperience.Harbour, Domains.Jade, 0);
            Loc("ruby", VoyageExperience.Harbour, Domains.Ruby, 1);
            Loc("gold", VoyageExperience.Harbour, Domains.Gold, 2);
            Loc("port", VoyageExperience.Port, cell: 3);
            Loc("vaults", VoyageExperience.VaultField, cell: 4);
            Loc("vaults2", VoyageExperience.VaultField, cell: 5);
            Loc("core", VoyageExperience.Core, cell: 6);
            void Lane(string a, string b, int cells) => c.Lanes.Add(new VoyageLane { From = a, To = b, Cells = cells });
            Lane("jade", "vaults", 1);
            Lane("jade", "port", 2);
            Lane("ruby", "port", 1);
            Lane("gold", "port", 1);
            Lane("port", "core", 1);
            Lane("core", "vaults2", 1);

            c.Crew.Add(new CrewPerson { PilotId = Hunter, Name = "Hunter", Faction = FactionType.Hunter });
            c.Crew.Add(new CrewPerson { PilotId = Merchant, Name = "Merchant", Faction = FactionType.Merchant });
            c.Crew.Add(new CrewPerson { PilotId = Pirate, Name = "Pirate", Faction = FactionType.Pirate });
            c.Crew.Add(new CrewPerson { PilotId = Aristocrat, Name = "Aristocrat", Faction = FactionType.Aristocrat });

            c.Jobs.Add(new JobTemplate { Id = VoyageRules.ReturnJobId, Kind = JobKind.Courier, Faction = FactionType.Merchant, Title = "home to {to}" });
            c.Jobs.Add(new JobTemplate { Id = "courier", Kind = JobKind.Courier, Faction = FactionType.Merchant, Goes = VoyageExperience.Port,
                Title = "{pilot} carries to {to}", SuccessLine = "delivered to {to}", Reward = 3, RewardElement = Element.Time, OnSuccess = "ev.delivered" });
            c.Jobs.Add(new JobTemplate { Id = "hunt", Kind = JobKind.Hunt, Faction = FactionType.Hunter, Needs = NewsTag.Sighting,
                Title = "open {to}", SuccessLine = "opened {to}", FailLine = "missed {to}", Reward = 5, RewardElement = Element.Space, OnSuccess = "ev.opened" });
            c.Jobs.Add(new JobTemplate { Id = "rumour", Kind = JobKind.Hunt, Faction = FactionType.Hunter, Goes = VoyageExperience.VaultField,
                Title = "rumour at {to}", Reward = 1 });
            c.Jobs.Add(new JobTemplate { Id = "salvage", Kind = JobKind.Salvage, Faction = FactionType.Pirate, Needs = NewsTag.Wreck,
                Title = "strip {to}", SuccessLine = "stripped {to}", OnSuccess = "ev.stripped" });
            c.Jobs.Add(new JobTemplate { Id = "defend", Kind = JobKind.Defence, Faction = FactionType.Aristocrat, Needs = NewsTag.Aid,
                Title = "hold {to}", SuccessLine = "held {to}", OnSuccess = "ev.held" });

            c.Events.Add(new VoyageEvent { Id = "ev.delivered", Writes = { new NewsWrite { Tag = NewsTag.Aid, At = NewsPlace.Destination, About = NewsAbout.AnotherCore, Line = "{about} needs help" } } });
            c.Events.Add(new VoyageEvent { Id = "ev.opened", SpendsNews = true, Writes = { new NewsWrite { Tag = NewsTag.Sighting, At = NewsPlace.Destination, About = NewsAbout.AnotherVaultField, Line = "chart to {about}" } } });
            c.Events.Add(new VoyageEvent { Id = "ev.stripped", SpendsNews = true });
            c.Events.Add(new VoyageEvent { Id = "ev.held", SpendsNews = true });

            c.Seeds.Add(new SeedNews { Port = "jade", Tag = NewsTag.Sighting, About = "vaults", Line = "vaults are lit" });
            c.SeedWrecks.Add(new SeedWreck { At = "core", Artifacts = new List<string> { "artifact.w" } });
            c.WreckLine = "wreck at {about}";
            c.WreckNewsLine = "an Ark fell at {about}";
            return c;
        }

        static VoyageJob Find(List<VoyageJob> board, string templateId) => board.FirstOrDefault(j => j.TemplateId == templateId);

        [Test]
        public void TheFixtureValidates() => CollectionAssert.IsEmpty(Map().Validate());

        [Test]
        public void TheSeatedPilotsDecideTheBoard()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);

            var hunters = VoyageRules.Board(s, c, new[] { Hunter, Stranger, Pirate });
            Assert.AreEqual(1, hunters.Count, "a hunter finds the sighting; a stranger and a pirate with no wreck news bring nothing, and at home there is no way home to offer");
            Assert.AreEqual("hunt", hunters[0].TemplateId);
            Assert.AreEqual("vaults", hunters[0].To, "the news says where");
            Assert.AreEqual("open VAULTS", hunters[0].Title);

            var merchants = VoyageRules.Board(s, c, new[] { Merchant });
            Assert.AreEqual("courier", merchants.Single().TemplateId);
            Assert.AreEqual("port", merchants[0].To);
            Assert.IsNotNull(merchants[0].Carried, "a courier carries the freshest news the destination lacks");
            Assert.AreEqual("vaults", merchants[0].Carried.About);
        }

        [Test]
        public void TheBoardIsTheSameWhenReadAgain()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var a = VoyageRules.Board(s, c, new[] { Hunter, Merchant });
            var b = VoyageRules.Board(s, c, new[] { Hunter, Merchant });
            CollectionAssert.AreEqual(a.Select(j => j.TemplateId + j.To), b.Select(j => j.TemplateId + j.To));
        }

        [Test]
        public void TwoPilotsOfOneTypeNeverBringTheSameNews()
        {
            var c = Map();
            c.Crew.Add(new CrewPerson { PilotId = "pilot.callsign.talon", Name = "Second", Faction = FactionType.Hunter });
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var board = VoyageRules.Board(s, c, new[] { Hunter, "pilot.callsign.talon" });
            Assert.AreEqual(2, board.Count);
            Assert.AreEqual("hunt", board[0].TemplateId);
            Assert.AreEqual("rumour", board[1].TemplateId, "the second hunter falls back on standing work");
            Assert.AreNotEqual(board[0].To, board[1].To, "and goes somewhere else");
        }

        [Test]
        public void AHuntChainsIntoTheNextHunt()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var hunt = Find(VoyageRules.Board(s, c, new[] { Hunter }), "hunt");
            Assert.IsTrue(VoyageRules.Take(s, c, hunt));
            Assert.IsTrue(s.AtSea);

            var arrival = VoyageRules.Arrive(s, c);
            Assert.AreEqual("vaults", arrival.Location.Id);
            Assert.AreEqual(2, arrival.VaultFinds.Count, "one find per vault shell");
            Assert.IsFalse(VoyageRules.WorkDone(s.Current, stowedHere: 0, held: false));

            VoyageRules.Stowed(s, arrival.VaultFinds[0]);
            Assert.IsTrue(VoyageRules.WorkDone(s.Current, stowedHere: 1, held: false));
            var r = VoyageRules.Resolve(s, c, JobOutcome.Succeeded);

            Assert.AreEqual("opened VAULTS", r.StoryLine);
            Assert.IsFalse(s.Ledger("jade").Knows(NewsTag.Sighting, "vaults"), "the sighting is spent everywhere");
            Assert.IsTrue(s.Ledger("jade").Knows(NewsTag.Sighting, "vaults2"),
                "the opened vault's chart is news at the nearest port: the next hunt");
            Assert.AreEqual(5, s.OwedOf(Element.Space));

            // Out at the vault field the crew reads the last port's news: the next hunt is already on the board.
            var next = VoyageRules.Board(s, c, new[] { Hunter });
            Assert.AreEqual("hunt", next[0].TemplateId);
            Assert.AreEqual("vaults2", next[0].To);
            Assert.IsTrue(next.Last().IsReturn, "away from home, the way home is always on the board");
        }

        [Test]
        public void CouriersCarryNewsThatMakesWorkElsewhere()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            Assert.IsFalse(s.Ledger("port").Knows(NewsTag.Sighting, "vaults"));

            var courier = Find(VoyageRules.Board(s, c, new[] { Merchant }), "courier");
            VoyageRules.Take(s, c, courier);
            VoyageRules.Arrive(s, c);
            Assert.IsTrue(VoyageRules.DoneOnArrival(s.Current));
            VoyageRules.Resolve(s, c, JobOutcome.Succeeded);

            Assert.IsTrue(s.Ledger("port").Knows(NewsTag.Sighting, "vaults"), "the dispatch reached the port's news");
            Assert.IsTrue(s.Ledger("port").Knows(NewsTag.Aid, "core"), "and the delivery's event wrote a call for aid");
            Assert.AreEqual("port", s.LastPort);

            var board = VoyageRules.Board(s, c, new[] { Hunter, Aristocrat });
            Assert.AreEqual("hunt", board[0].TemplateId, "a hunter at the port now finds the sighting the courier brought");
            Assert.AreEqual("defend", board[1].TemplateId, "and an aristocrat the call for aid");
        }

        [Test]
        public void AFallenArkLeavesAWreckAPirateCanStrip()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            s.Wrecks.Clear();
            var hunt = Find(VoyageRules.Board(s, c, new[] { Hunter }), "hunt");
            VoyageRules.Take(s, c, hunt);
            VoyageRules.Arrive(s, c);
            VoyageRules.Stowed(s, "artifact.a");

            var r = VoyageRules.ArkLost(s, c, "vaults", taken: false, spilled: s.Aboard.ToList());
            Assert.AreEqual(JobOutcome.ArkLost, r.Outcome);
            CollectionAssert.IsEmpty(s.Aboard, "everything aboard is lost");
            Assert.AreEqual(0, s.OwedOf(Element.Space), "and nothing is paid");
            Assert.AreEqual("jade", s.At, "the squad limps home");
            Assert.IsNotNull(s.WreckAt("vaults"));
            Assert.IsTrue(s.Ledger("jade").Knows(NewsTag.Wreck, "vaults"), "the wreck is news at the nearest port");

            var salvage = Find(VoyageRules.Board(s, c, new[] { Pirate }), "salvage");
            Assert.IsNotNull(salvage, "a pirate seated now finds the squad's own wreck");
            VoyageRules.Take(s, c, salvage);
            var arrival = VoyageRules.Arrive(s, c);
            CollectionAssert.Contains(arrival.WreckFinds, "artifact.a");
            VoyageRules.Stowed(s, "artifact.a");
            Assert.IsNull(s.WreckAt("vaults"), "what is taken leaves the wreck");
        }

        [Test]
        public void ATakenArkLeavesNoWreck()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            s.Wrecks.Clear();
            VoyageRules.Take(s, c, Find(VoyageRules.Board(s, c, new[] { Hunter }), "hunt"));
            VoyageRules.Arrive(s, c);
            VoyageRules.Stowed(s, "artifact.a");
            VoyageRules.ArkLost(s, c, "vaults", taken: true, spilled: s.Aboard.ToList());
            Assert.IsNull(s.WreckAt("vaults"), "raiders took it all");
        }

        [Test]
        public void DockingAtHomeExtractsOnce()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            VoyageRules.Take(s, c, Find(VoyageRules.Board(s, c, new[] { Hunter }), "hunt"));
            VoyageRules.Arrive(s, c);
            VoyageRules.Stowed(s, "artifact.b");
            VoyageRules.Resolve(s, c, JobOutcome.Succeeded);
            Assert.IsNull(VoyageRules.Extract(s), "away from home nothing is extracted");

            var home = VoyageRules.Board(s, c, new[] { Hunter }).Last();
            Assert.IsTrue(home.IsReturn);
            VoyageRules.Take(s, c, home);
            VoyageRules.Arrive(s, c);
            Assert.IsTrue(s.IsHome());
            VoyageRules.Resolve(s, c, JobOutcome.Succeeded);

            var x = VoyageRules.Extract(s);
            CollectionAssert.AreEqual(new[] { "artifact.b" }, x.Artifacts);
            Assert.AreEqual(5, x.Crystals[(int)Element.Space]);
            var again = VoyageRules.Extract(s);
            CollectionAssert.IsEmpty(again.Artifacts, "a second extraction hands over nothing");
        }

        [Test]
        public void TheCorridorEndsInTheDestinationsOwnWorld()
        {
            var c = Map();
            var cells = VoyageRules.Corridor(c, "jade", "core", day: 3, poolSize: 8);
            // jade -> port (2 open cells + port's world) -> core (1 open cell + core's world)
            Assert.AreEqual(5, cells.Count);
            Assert.AreEqual(3, cells[2], "the port passed on the way");
            Assert.AreEqual(6, cells[4], "the destination's own world last");
            Assert.AreNotEqual(6, cells[3], "an open-sea cell is never the destination's world");
            CollectionAssert.AreEqual(cells, VoyageRules.Corridor(c, "jade", "core", day: 3, poolSize: 8), "seeded");
            CollectionAssert.IsEmpty(VoyageRules.Corridor(c, "jade", "nowhere", 3, 8));
        }

        [Test]
        public void ATakeFromTheWrongPlaceIsRefused()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var hunt = Find(VoyageRules.Board(s, c, new[] { Hunter }), "hunt");
            hunt.From = "port";
            Assert.IsFalse(VoyageRules.Take(s, c, hunt));
            Assert.IsFalse(s.AtSea);
        }

        [Test]
        public void OldNewsIsForgotten()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            s.Day = VoyageRules.NewsLifeDays + 1;
            CollectionAssert.IsEmpty(VoyageRules.Board(s, c, new[] { Hunter }).Where(j => j.TemplateId == "hunt"));
        }

        [Test]
        public void GivingUpAwayFromHomeLosesWhatWasFound_ButHomeHasNothingToGiveUp()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            Assert.IsNull(VoyageRules.Abandon(s, c), "home between jobs: nothing to give up");

            VoyageRules.Take(s, c, Find(VoyageRules.Board(s, c, new[] { Hunter }), "hunt"));
            VoyageRules.Arrive(s, c);
            VoyageRules.Stowed(s, "artifact.a");
            var r = VoyageRules.Abandon(s, c);
            Assert.AreEqual(JobOutcome.Failed, r.Outcome);
            CollectionAssert.IsEmpty(s.Aboard, "only docking at home extracts");
            Assert.IsTrue(s.IsHome());
            Assert.IsNull(VoyageRules.Extract(s).Artifacts.FirstOrDefault());
        }

        [Test]
        public void AFinishedJobAnchorsTheSquadThere_AndTheNextVoyageSetsOutFromIt()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            var job = Find(VoyageRules.Board(s, c, new[] { Hunter }), "hunt");
            VoyageRules.Take(s, c, job);
            VoyageRules.Arrive(s, c);
            VoyageRules.Stowed(s, "artifact.a");
            var r = VoyageRules.Anchor(s, c, workDone: true);
            Assert.AreEqual(JobOutcome.Succeeded, r.Outcome);
            Assert.AreEqual(job.To, s.At, "anchored at the job's destination");
            CollectionAssert.Contains(s.Aboard, "artifact.a", "still aboard, still at stake");

            VoyageRules.Resume(s, c, Domains.Jade);
            Assert.AreEqual(job.To, s.At, "the next voyage sets out from the anchorage");
            CollectionAssert.Contains(s.Aboard, "artifact.a");
            Assert.IsTrue(VoyageRules.Board(s, c, new[] { Hunter }).Any(j => j.IsReturn), "the way home is on offer");
        }

        [Test]
        public void ALegCutOffAtSeaIsAVoyageGivenUp()
        {
            var c = Map();
            var s = VoyageRules.NewGalaxy(c, Domains.Jade);
            VoyageRules.Take(s, c, Find(VoyageRules.Board(s, c, new[] { Hunter }), "hunt"));
            s.Aboard.Add("artifact.a");
            VoyageRules.Resume(s, c, Domains.Jade);
            Assert.IsTrue(s.IsHome());
            CollectionAssert.IsEmpty(s.Aboard);
            Assert.IsNull(s.Current);
        }

        [Test]
        public void ARecordFromAPartialSaveIsRepaired()
        {
            var s = new VoyageState { Aboard = null, Ledgers = null, Wrecks = new List<WreckRecord> { null }, Current = new VoyageJob(), AtSea = true };
            s.Normalize();
            Assert.IsNotNull(s.Aboard);
            Assert.IsNotNull(s.Ledgers);
            CollectionAssert.IsEmpty(s.Wrecks);
            Assert.IsNull(s.Current);
            Assert.IsFalse(s.AtSea);
        }
    }
}
#endif
