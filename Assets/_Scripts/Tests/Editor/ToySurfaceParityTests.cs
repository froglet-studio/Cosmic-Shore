#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>A toy declares its choices ONCE.</b> The fly-into matrix and the menu Toy Box window are
    /// two INPUTS to that one declaration - they may differ in how an option is DRAWN, never in
    /// which options exist or what pressing one does.
    ///
    /// <para><c>MatrixToy</c> makes that structural: it builds the stations from
    /// <c>BuildOptions</c>, answers <c>IToyShellSurface.BuildShellOptions</c> with the same call,
    /// and wires each station's action to that option's own <c>Apply</c>. This fixture guards the
    /// two ways a subclass could climb back out of it, because both compile perfectly well and
    /// neither is visible in play until somebody notices a row that is missing from one surface -
    /// which is exactly how the Lifeform bench's window lost a whole kingdom, and how the Vessel
    /// Changer's roster went a vessel stale.</para>
    ///
    /// <para>Read from the SOURCE rather than by reflection: the question is what a future author
    /// will write, and the cheapest place to answer it is the text they will write it in.</para>
    /// </summary>
    [TestFixture]
    public class ToySurfaceParityTests
    {
        const string ToyDir = "Assets/_Scripts/Controller/Toys";

        /// <summary>Every .cs in the toys folder, as (name, text).</summary>
        static IEnumerable<(string name, string text)> ToySources() =>
            Directory.GetFiles(ToyDir, "*.cs")
                     .Select(p => (Path.GetFileNameWithoutExtension(p), File.ReadAllText(p)));

        /// <summary>The classes declared `: MatrixToy` (directly - the family is one level deep).</summary>
        static List<(string name, string text)> MatrixToySubclasses()
        {
            var found = ToySources()
                .Where(s => Regex.IsMatch(s.text, @"class\s+\w+\s*:\s*MatrixToy\b"))
                .ToList();

            Assert.IsNotEmpty(found,
                "Found no MatrixToy subclasses at all. Either the family was renamed or this " +
                "fixture's pattern rotted - repair it rather than deleting it, because a parity " +
                "gate that matches nothing passes forever.");
            return found;
        }

        [Test]
        public void NoMatrixToySubclassDeclaresTheShellInterface()
        {
            foreach (var (name, text) in MatrixToySubclasses())
            {
                Assert.IsFalse(Regex.IsMatch(text, @"class\s+\w+\s*:\s*MatrixToy\s*,\s*IToyShellSurface"),
                    $"{name} declares IToyShellSurface on top of MatrixToy. MatrixToy already " +
                    "implements it by delegating to this toy's own BuildOptions; re-declaring the " +
                    "interface lets the class provide a SECOND explicit implementation, which wins " +
                    "over the base's and is once again a list the world does not read. Remove the " +
                    "interface from the declaration and override BuildOptions (and ShellAvailable " +
                    "if the answer is not always settled).");
            }
        }

        [Test]
        public void NoMatrixToySubclassWiresItsOwnStationAction()
        {
            foreach (var (name, text) in MatrixToySubclasses())
            {
                Assert.IsFalse(text.Contains("OnVesselPassed"),
                    $"{name} sets ToyMatrixStation.OnVesselPassed itself. The station's action is " +
                    "the option's own Apply and MatrixToy.CreateStation wires it - that is what " +
                    "makes 'the station and the window do the same thing' structural rather than a " +
                    "convention somebody has to keep. If this station cannot go through " +
                    "CreateStation (a full Toy with its own bloom, as the painting gallery needs), " +
                    "it must still invoke that same Apply and nothing else.");
            }
        }

        /// <summary>
        /// The base's own end of the contract. If these two lines ever stop being true the two
        /// tests above are guarding a rule that is no longer implemented, and would still pass.
        /// </summary>
        [Test]
        public void MatrixToyItselfAnswersTheWindowWithTheOneDeclaration()
        {
            string text = File.ReadAllText(Path.Combine(ToyDir, "MatrixToy.cs"));

            Assert.IsTrue(
                Regex.IsMatch(text, @"void\s+IToyShellSurface\.BuildShellOptions\s*\([^)]*\)\s*=>\s*BuildOptions\("),
                "MatrixToy no longer answers IToyShellSurface.BuildShellOptions with BuildOptions. " +
                "That one line is the whole guarantee that the window and the matrix read the same " +
                "declaration.");

            Assert.IsTrue(text.Contains("station.OnVesselPassed = option?.Apply;"),
                "MatrixToy.CreateStation no longer wires the station's action to the option's own " +
                "Apply, so a station can once again do something the window's row does not.");

            Assert.IsTrue(Regex.IsMatch(text, @"abstract\s+void\s+BuildOptions\s*\("),
                "MatrixToy.BuildOptions is no longer abstract - a subclass that does not declare " +
                "its choices there has nowhere else the base can read them from.");
        }
    }
}
#endif
