using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A lifeform's death runs once. <c>LifeForm.Die</c> re-enters through its own destruction (every prism it damages and
    /// every spindle it withers reports back to <c>CheckIfDead</c>), so every path into it must latch <c>dying</c> first:
    /// <c>CheckIfDead</c> and <c>Jousted</c> do it inline, and every other caller goes through <c>DieOnce</c>. A bare
    /// <c>Die()</c> from <c>Flora.KillReplicated</c> (a client's mirror of a replicated plant's death) ran the death
    /// twice, the second time attributed to the destruction's "Guy Fawkes". Structural, because the death path needs a
    /// running scene (spindles, prisms, a cell) that an edit-mode test cannot stand up.
    /// </summary>
    public class LifeFormDieOnceTests
    {
        const string Dir = "Assets/_Scripts/Controller/Environment/FloraAndFauna/";

        static string Read(string file) => File.ReadAllText(Dir + file).Replace("\r\n", "\n");

        [Test]
        public void DieOnce_latches_dying_before_it_dies()
        {
            var m = Regex.Match(Read("LifeForm.cs"),
                @"protected void DieOnce\(string killerName = """"\)\s*\{\s*if \(dying \|\| isCleaningUp\) return;\s*dying = true;\s*Die\(killerName\);\s*\}");
            Assert.IsTrue(m.Success, "LifeForm.DieOnce must return when already dying, set dying, then call Die - in that order.");
        }

        [TestCase("Flora.cs", @"public void KillReplicated\(\) => DieOnce\(\);")]
        [TestCase("BranchingFlora.cs", @"Auto-dying\.""\);\s*DieOnce\(\);")]
        public void Deaths_outside_CheckIfDead_and_Jousted_go_through_DieOnce(string file, string pattern)
        {
            Assert.IsTrue(Regex.IsMatch(Read(file), pattern),
                $"{file}: this death does not come through CheckIfDead or Jousted, so it must call DieOnce, not Die " +
                "(a bare Die() runs the death twice).");
        }
    }
}
