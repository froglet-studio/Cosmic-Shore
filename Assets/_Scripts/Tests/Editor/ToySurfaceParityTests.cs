#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>A toy declares its choices ONCE, and only the Toy Box offers them.</b> Toys have no world
    /// body (2026-11-01): nothing in freestyle is flown into to pick a toy. <c>MatrixToy</c> answers
    /// <c>IToyShellSurface.BuildShellOptions</c> with its subclass's <c>BuildOptions</c> and nothing
    /// else. This fixture guards the ways a subclass could climb back out of that, because each
    /// compiles perfectly well.
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

        /// <summary>The classes declared `: MatrixToy` or `: Toy` (directly - the family is shallow).</summary>
        static List<(string name, string text)> ToySubclasses(string baseName)
        {
            var found = ToySources()
                .Where(s => Regex.IsMatch(s.text, $@"class\s+\w+\s*:\s*{baseName}\b(?!\w)"))
                .ToList();

            Assert.IsNotEmpty(found,
                $"Found no {baseName} subclasses at all. Either the family was renamed or this " +
                "fixture's pattern rotted - repair it rather than deleting it, because a gate that " +
                "matches nothing passes forever.");
            return found;
        }

        [Test]
        public void NoMatrixToySubclassDeclaresTheShellInterface()
        {
            foreach (var (name, text) in ToySubclasses("MatrixToy"))
            {
                Assert.IsFalse(Regex.IsMatch(text, @"class\s+\w+\s*:\s*MatrixToy\s*,\s*IToyShellSurface"),
                    $"{name} declares IToyShellSurface on top of MatrixToy. MatrixToy already " +
                    "implements it by delegating to this toy's own BuildOptions; re-declaring the " +
                    "interface lets the class provide a SECOND explicit implementation. Remove the " +
                    "interface from the declaration and override BuildOptions (and ShellAvailable " +
                    "if the answer is not always settled).");
            }
        }

        /// <summary>A toy has no trigger, ring or body of its own - it is chosen in the Toy Box.</summary>
        [Test]
        public void NoToyIsFlownInto()
        {
            var toys = ToySubclasses("MatrixToy").Concat(ToySubclasses("Toy"));
            string[] diegetic = { "OnTriggerEnter", "AddSwitchRing", "CreateBareRoot", "ToyFactory.CreateRoot(" };
            foreach (var (name, text) in toys)
                foreach (var word in diegetic)
                    Assert.IsFalse(text.Contains(word),
                        $"{name} uses {word}: a toy is selected only through the app shell's Toy Box " +
                        "and stands nothing to fly into. Fly-through pieces belong to a toy's PLAY " +
                        "(a ToyStation such as a painting's stroke gate), not to choosing it.");
        }

        /// <summary>
        /// The base's own end of the contract. If these stop being true the tests above guard a
        /// rule that is no longer implemented, and would still pass.
        /// </summary>
        [Test]
        public void MatrixToyItselfAnswersTheWindowWithTheOneDeclaration()
        {
            string text = File.ReadAllText(Path.Combine(ToyDir, "MatrixToy.cs"));

            Assert.IsTrue(
                Regex.IsMatch(text, @"void\s+IToyShellSurface\.BuildShellOptions\s*\([^)]*\)\s*=>\s*BuildOptions\("),
                "MatrixToy no longer answers IToyShellSurface.BuildShellOptions with BuildOptions.");

            Assert.IsTrue(Regex.IsMatch(text, @"abstract\s+void\s+BuildOptions\s*\("),
                "MatrixToy.BuildOptions is no longer abstract - a subclass that does not declare " +
                "its choices there has nowhere else the base can read them from.");

            string toy = File.ReadAllText(Path.Combine(ToyDir, "Toy.cs"));
            Assert.IsFalse(toy.Contains("Collider"),
                "Toy carries a collider again: toys are not flown into (ToyStation is what is).");
        }
    }
}
#endif
