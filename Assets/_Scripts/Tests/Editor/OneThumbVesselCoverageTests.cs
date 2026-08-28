using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>Every ONE-THUMB hull gets the desktop mouse scheme, and gets it structurally.</b>
    ///
    /// <para>`SingleStickMouseInputStrategy` is selected off the live
    /// <c>IVesselStatus.IsSingleStickControls</c> flag, which is written by whichever
    /// <c>VesselTransformer</c> the vessel resolves — and a vessel resolves it with
    /// <c>GetOrAdd&lt;VesselTransformer&gt;()</c> → <c>TryGetComponent</c>, i.e. by COMPONENT
    /// ORDER. So a prefab carrying two transformers has its flight model, and therefore its
    /// whole control scheme, decided by a list nobody maintains deliberately.</para>
    ///
    /// <para>Falcon and Shrike carried exactly that: a <c>SingleStickVesselTransformer</c> at
    /// index 2 and a disabled base <c>VesselTransformer</c> at index 3. They were not broken —
    /// the single-stick one won, and the base was inert (<c>m_Enabled: 0</c>, and
    /// <c>Update</c> early-outs on <c>!isActive</c>) — but "correct because of the order two
    /// components happen to sit in" is not a guarantee, and a re-serialize or an inspector
    /// reorder would have silently handed those vessels the dual-stick model with
    /// <c>IsSingleStickControls</c> never set.</para>
    ///
    /// <para>Read from the prefab TEXT rather than through the asset database: the question is
    /// about the serialized component list itself, and a <c>GetComponents</c> sweep would
    /// report the resolved winner while saying nothing about the duplicate behind it.</para>
    /// </summary>
    [TestFixture]
    public class OneThumbVesselCoverageTests
    {
        const string VesselDir = "Assets/_Prefabs/Spacevessels";
        const string ScriptDir = "Assets/_Scripts/Controller/Vessel";

        /// <summary>The one-thumb hulls, by prefab name. A vessel is one-thumb because its
        /// transformer sets <c>IsSingleStickControls</c>; this list is the roster that must
        /// stay true, so adding a hull here without giving it such a transformer fails.</summary>
        static readonly string[] OneThumbVessels =
            { "Sparrow", "Serpent", "Grizzly", "Termite", "Falcon", "Shrike", "Scarab" };

        // Unity fileIDs are SIGNED - a negative anchor is ordinary, and a `&(\d+)` regex
        // silently skips those documents (which is how a first pass of this census lost the
        // Grizzly entirely).
        static readonly Regex DocHeader =
            new(@"^--- !u!(\d+) &(-?\d+)( stripped)?$", RegexOptions.Multiline);

        static string GuidOf(string scriptName)
        {
            string meta = Path.Combine(ScriptDir, scriptName + ".cs.meta");
            Assert.IsTrue(File.Exists(meta), $"{meta} is missing.");
            var m = Regex.Match(File.ReadAllText(meta), @"^guid: (\w+)", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, $"{meta} has no guid.");
            return m.Groups[1].Value;
        }

        /// <summary>Every non-stripped MonoBehaviour document's script guid, in file order.</summary>
        static List<string> ComponentScriptGuids(string prefabText)
        {
            var headers = DocHeader.Matches(prefabText).Cast<Match>().ToList();
            var guids = new List<string>();

            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].Groups[1].Value != "114") continue;
                if (headers[i].Groups[3].Success) continue;   // a stripped doc is a reference stub

                int start = headers[i].Index + headers[i].Length;
                int end = i + 1 < headers.Count ? headers[i + 1].Index : prefabText.Length;
                var m = Regex.Match(prefabText.Substring(start, end - start),
                                    @"m_Script: \{fileID: 11500000, guid: (\w+)");
                if (m.Success) guids.Add(m.Groups[1].Value);
            }
            return guids;
        }

        [Test]
        public void EveryVesselCarriesExactlyOneTransformer()
        {
            var transformers = new Dictionary<string, string>
            {
                [GuidOf("VesselTransformer")] = "VesselTransformer",
                [GuidOf("SingleStickVesselTransformer")] = "SingleStickVesselTransformer",
                [GuidOf("ScarabVesselTransformer")] = "ScarabVesselTransformer",
                [GuidOf("GunVesselTransformer")] = "GunVesselTransformer",
            };

            foreach (var path in Directory.GetFiles(VesselDir, "*.prefab"))
            {
                var found = ComponentScriptGuids(File.ReadAllText(path))
                            .Where(transformers.ContainsKey)
                            .Select(g => transformers[g])
                            .ToList();
                if (found.Count == 0) continue;

                Assert.AreEqual(1, found.Count,
                    $"{Path.GetFileNameWithoutExtension(path)} carries {found.Count} transformers " +
                    $"({string.Join(", ", found)}). VesselStatus resolves one by COMPONENT ORDER, so " +
                    "which flight model - and which control scheme - the vessel gets is decided by a " +
                    "list nobody maintains. Remove the one that is not the vessel's.");
            }
        }

        [Test]
        public void EveryOneThumbVesselResolvesToASingleStickTransformer()
        {
            string singleStick = GuidOf("SingleStickVesselTransformer");
            string scarab = GuidOf("ScarabVesselTransformer");

            foreach (var vessel in OneThumbVessels)
            {
                string path = Path.Combine(VesselDir, vessel + ".prefab");
                Assert.IsTrue(File.Exists(path), $"{vessel}.prefab is missing.");

                var guids = ComponentScriptGuids(File.ReadAllText(path));
                Assert.IsTrue(guids.Contains(singleStick) || guids.Contains(scarab),
                    $"{vessel} is listed as a one-thumb hull but carries neither " +
                    "SingleStickVesselTransformer nor ScarabVesselTransformer, so it never sets " +
                    "IsSingleStickControls and SingleStickMouseInputStrategy will refuse it " +
                    "(MouseFlightDiagnostics reports NotSingleStick).");
            }
        }

        [Test]
        public void NoTwoStickVesselClaimsToBeOneThumb()
        {
            // The other half of the roster: a hull that quietly gained a single-stick
            // transformer would start locking the cursor and flying on the mouse with nobody
            // having decided that, and its dual-WASD keys would stop steering.
            string singleStick = GuidOf("SingleStickVesselTransformer");
            string scarab = GuidOf("ScarabVesselTransformer");

            foreach (var path in Directory.GetFiles(VesselDir, "*.prefab"))
            {
                string vessel = Path.GetFileNameWithoutExtension(path);
                if (OneThumbVessels.Contains(vessel)) continue;

                var guids = ComponentScriptGuids(File.ReadAllText(path));
                Assert.IsFalse(guids.Contains(singleStick) || guids.Contains(scarab),
                    $"{vessel} carries a single-stick transformer but is not on the one-thumb " +
                    "roster. Either add it to OneThumbVessels (and to " +
                    "ONE_THUMB_MOUSE_CONTROLS.md's table) or give it the transformer it wants.");
            }
        }
    }
}
