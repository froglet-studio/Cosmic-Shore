#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Holds the 2026-11-16 Maelstrom finding: two players with the same display name ("ys1" twice) were one roster
    /// row, because the roster keys a pilot by name. <see cref="RosterNames"/> must give every pilot of a match a
    /// different name, and the same one on every machine whatever order the players arrived in.
    /// </summary>
    [TestFixture]
    public class RosterNamesTests
    {
        const string Template = "{name} ({number})";

        static string Resolve(ulong id, string name, List<(ulong, string)> field, string template = Template)
        {
            var others = new List<(ulong networkObjectId, string displayName)>();
            foreach (var p in field)
                if (p.Item1 != id) others.Add(p);
            return RosterNames.Compose(name, RosterNames.NamesakesBefore(id, name, others), template);
        }

        [Test]
        public void TwoPlayersWithTheSameDisplayName_GetDifferentRosterNames()
        {
            var field = new List<(ulong, string)> { (3, "ys1"), (7, "ys1"), (9, "AI 1"), (11, "AI 2") };

            var names = new HashSet<string>();
            foreach (var p in field) names.Add(Resolve(p.Item1, p.Item2, field));

            Assert.AreEqual(4, names.Count, "four pilots must be four roster rows");
            Assert.AreEqual("ys1", Resolve(3, "ys1", field), "the first pilot keeps its name unchanged");
            Assert.AreEqual("ys1 (2)", Resolve(7, "ys1", field));
        }

        [Test]
        public void TheAnswerDoesNotDependOnTheOrderPlayersArrivedIn()
        {
            var hostOrder = new List<(ulong, string)> { (3, "ys1"), (7, "ys1"), (12, "ys1") };
            var guestOrder = new List<(ulong, string)> { (12, "ys1"), (7, "ys1"), (3, "ys1") };

            foreach (var p in hostOrder)
                Assert.AreEqual(Resolve(p.Item1, p.Item2, hostOrder), Resolve(p.Item1, p.Item2, guestOrder));

            Assert.AreEqual("ys1 (3)", Resolve(12, "ys1", guestOrder));
        }

        [Test]
        public void AUniqueName_IsLeftAlone()
        {
            var field = new List<(ulong, string)> { (3, "ys1"), (7, "Kyoki") };

            Assert.AreEqual("ys1", Resolve(3, "ys1", field));
            Assert.AreEqual("Kyoki", Resolve(7, "Kyoki", field));
        }

        [Test]
        public void AnEmptyName_IsNeverNumbered()
        {
            var field = new List<(ulong, string)> { (3, ""), (7, "") };

            Assert.AreEqual("", Resolve(7, "", field), "a name that has not replicated yet is not a namesake");
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("{name}")]
        [TestCase("Pilot")]
        public void ATemplateThatCannotTellThemApart_StillGivesDifferentNames(string template)
        {
            var field = new List<(ulong, string)> { (3, "ys1"), (7, "ys1"), (9, "ys1") };

            var names = new HashSet<string>();
            foreach (var p in field) names.Add(Resolve(p.Item1, p.Item2, field, template));

            Assert.AreEqual(3, names.Count, "an emptied or wrong template must not bring the shared row back");
        }
    }
}
#endif
