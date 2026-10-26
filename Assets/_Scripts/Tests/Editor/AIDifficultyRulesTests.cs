#if UNITY_EDITOR
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="AIDifficultyRules"/> - the shared reading of the host's AI difficulty pick. The
    /// value crosses two boundaries as a bare number (the replicated lobby carries it as an int,
    /// the launch preference file as JSON), so the rules that turn a number back into a difficulty
    /// are what keep a 0, an old save or a future member from reaching the AI as nonsense.
    /// </summary>
    public class AIDifficultyRulesTests
    {
        [Test]
        public void Members_KeepTheirNumbers()
        {
            // Saved to disk and replicated as ints - a renumbering would silently swap a saved
            // Hard for an Easy on every machine that remembered one.
            Assert.AreEqual(1, (int)AIDifficulty.Easy);
            Assert.AreEqual(2, (int)AIDifficulty.Medium);
            Assert.AreEqual(3, (int)AIDifficulty.Hard);
            Assert.AreEqual(4, (int)AIDifficulty.Legendary);
        }

        [Test]
        public void Default_IsMedium()
        {
            Assert.AreEqual(AIDifficulty.Medium, AIDifficultyRules.Default);
        }

        [Test]
        public void Resolve_KeepsEveryRealDifficulty()
        {
            Assert.AreEqual(AIDifficulty.Easy, AIDifficultyRules.Resolve(AIDifficulty.Easy));
            Assert.AreEqual(AIDifficulty.Medium, AIDifficultyRules.Resolve(AIDifficulty.Medium));
            Assert.AreEqual(AIDifficulty.Hard, AIDifficultyRules.Resolve(AIDifficulty.Hard));
            Assert.AreEqual(AIDifficulty.Legendary, AIDifficultyRules.Resolve(AIDifficulty.Legendary));
        }

        [Test]
        public void Resolve_TurnsAnUnwrittenOrUnknownValueIntoTheDefault()
        {
            Assert.AreEqual(AIDifficulty.Medium, AIDifficultyRules.Resolve(0), "unwritten");
            Assert.AreEqual(AIDifficulty.Medium, AIDifficultyRules.Resolve(5), "a member a later build added");
            Assert.AreEqual(AIDifficulty.Medium, AIDifficultyRules.Resolve(-1), "garbage");
            Assert.AreEqual(AIDifficulty.Medium, AIDifficultyRules.Resolve(default(AIDifficulty)));
        }

        [Test]
        public void Resolve_RoundTripsTheReplicatedInt()
        {
            foreach (var d in new[] { AIDifficulty.Easy, AIDifficulty.Medium, AIDifficulty.Hard, AIDifficulty.Legendary })
                Assert.AreEqual(d, AIDifficultyRules.Resolve((int)d));
        }

        [Test]
        public void IsOffered_OnlyWhereTheAIReadsIt()
        {
            Assert.IsTrue(AIDifficultyRules.IsOfferedFor(GameModes.SkimRace));
            Assert.IsTrue(AIDifficultyRules.IsOfferedFor(GameModes.Slingshot), "the Stoat's circuit race (GateRaceHandicap)");
            Assert.IsTrue(AIDifficultyRules.IsOfferedFor(GameModes.Warpline), "the Stoat's time race (GateRaceHandicap)");
            Assert.IsTrue(AIDifficultyRules.IsOfferedFor(GameModes.Regatta), "Squirrels: SkimRaceHandicap; every other hull: GateRaceHandicap");
            Assert.IsTrue(AIDifficultyRules.IsOfferedFor(GameModes.AstroLeague), "Arena, platform autopilot (AIPilotBelief)");
            Assert.IsTrue(AIDifficultyRules.IsOfferedFor(GameModes.BroodRush), "Arena, platform autopilot (AIPilotBelief)");
            Assert.IsTrue(AIDifficultyRules.IsOfferedFor(GameModes.Broadside), "Arena, platform autopilot (AIPilotBelief)");
            Assert.IsFalse(AIDifficultyRules.IsOfferedFor(GameModes.Rampage));
            Assert.IsFalse(AIDifficultyRules.IsOfferedFor(GameModes.Joust));
            Assert.IsFalse(AIDifficultyRules.IsOfferedFor(GameModes.Random));
        }

        [Test]
        public void PilotBelief_NeverDoublesAModesOwnHandicap()
        {
            // The Skim Race pilot and the gate races carry their own handicap; the platform belief
            // on top would make every mistake twice.
            foreach (var mode in new[] { GameModes.SkimRace, GameModes.Slingshot, GameModes.Warpline, GameModes.Regatta })
                Assert.IsFalse(AIDifficultyRules.UsesPilotBelief(mode), mode.ToString());
            foreach (var mode in new[] { GameModes.AstroLeague, GameModes.BroodRush, GameModes.Broadside })
                Assert.IsTrue(AIDifficultyRules.UsesPilotBelief(mode), mode.ToString());
            Assert.IsFalse(AIDifficultyRules.UsesPilotBelief(GameModes.Joust));
        }

        [Test]
        public void EveryArenaCard_OffersTheDifficulty()
        {
            // The Arena roster (ArenaGames.asset): Astro League, Brood Rush, Regatta, Broadside.
            foreach (var mode in new[] { GameModes.AstroLeague, GameModes.BroodRush, GameModes.Regatta, GameModes.Broadside })
                Assert.IsTrue(AIDifficultyRules.IsOfferedFor(mode), mode.ToString());
        }

        [Test]
        public void Step_MovesOneAndStopsAtTheEnds()
        {
            Assert.AreEqual(AIDifficulty.Medium, AIDifficultyRules.Step(AIDifficulty.Easy, +1));
            Assert.AreEqual(AIDifficulty.Hard, AIDifficultyRules.Step(AIDifficulty.Medium, +1));
            Assert.AreEqual(AIDifficulty.Legendary, AIDifficultyRules.Step(AIDifficulty.Hard, +1));
            Assert.AreEqual(AIDifficulty.Legendary, AIDifficultyRules.Step(AIDifficulty.Legendary, +1));
            Assert.AreEqual(AIDifficulty.Hard, AIDifficultyRules.Step(AIDifficulty.Legendary, -1));
            Assert.AreEqual(AIDifficulty.Easy, AIDifficultyRules.Step(AIDifficulty.Medium, -1));
            Assert.AreEqual(AIDifficulty.Easy, AIDifficultyRules.Step(AIDifficulty.Easy, -1));
            Assert.AreEqual(AIDifficulty.Medium, AIDifficultyRules.Step(AIDifficulty.Medium, 0));
        }

        [Test]
        public void Step_FromAnUnwrittenValue_StartsAtTheDefault()
        {
            Assert.AreEqual(AIDifficulty.Hard, AIDifficultyRules.Step(default, +1));
            Assert.AreEqual(AIDifficulty.Easy, AIDifficultyRules.Step(default, -1));
        }
    }
}
#endif
