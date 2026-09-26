#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>A vessel the game can SPAWN is a vessel the freestyle toybox must OFFER.</b>
    ///
    /// <para><c>ToyVesselRoster.Default</c> is the list the Vessel Changer and the Lifeform
    /// Matrix's hangar both draw from, and it is the one registration a new vessel needs that
    /// lives in CODE rather than in an asset. Every other place a hull has to be named — the
    /// <c>Vessel Prefab Container</c>, <c>DefaultNetworkPrefabs</c>, the class lists, the camera
    /// settings — is written by that vessel's own editor setup tool, so nobody has to remember
    /// them. This array is not, which makes it exactly the one that gets missed.</para>
    ///
    /// <para>And it gets missed SILENTLY: a hull absent from the roster simply has no station in
    /// the matrix. There is no error, no warning and no empty slot — the matrix is one ship
    /// smaller than the fleet, which is indistinguishable from a matrix that is correct. The
    /// player-visible symptom is that the new vessel cannot be flown in freestyle at all.</para>
    ///
    /// <para>The gate asks the question in the direction that cannot fire early: it reads the
    /// prefab CONTAINER (the asset both spawn paths resolve through, so a hull in it is a hull
    /// the game will build) and requires each entry to be in the roster. A vessel designed but
    /// not yet authored in the editor is therefore free to sit in the roster ahead of its prefab
    /// — which is what <c>ToyVesselRoster.ResolveOffered</c> exists to make safe — while a
    /// vessel that becomes spawnable without becoming offerable fails the build.</para>
    ///
    /// <para>Read from the container's TEXT and resolve guids through <c>.meta</c> files rather
    /// than through the asset database: the question is which prefabs the asset names, and the
    /// prefab's FILE NAME is the registration key the whole fleet keys off
    /// (<c>references/CONTRACT.md</c> §1.2).</para>
    /// </summary>
    [TestFixture]
    public class ToyVesselRosterCoverageTests
    {
        const string ContainerPath = "Assets/_SO_Assets/Vessel Prefab Container.asset";
        const string VesselDir = "Assets/_Prefabs/Spacevessels";

        /// <summary>Prefab name → guid, for every vessel prefab in the fleet folder.</summary>
        static Dictionary<string, string> VesselGuids()
        {
            var map = new Dictionary<string, string>();
            foreach (var meta in Directory.GetFiles(VesselDir, "*.prefab.meta"))
            {
                var m = Regex.Match(File.ReadAllText(meta), @"^guid: (\w+)", RegexOptions.Multiline);
                if (!m.Success) continue;
                // "Butterfly.prefab.meta" → "Butterfly"
                map[Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(meta))] =
                    m.Groups[1].Value;
            }
            return map;
        }

        /// <summary>The vessel classes the prefab container registers, by prefab name.</summary>
        static List<VesselClassType> RegisteredVessels()
        {
            Assert.IsTrue(File.Exists(ContainerPath), $"{ContainerPath} is missing.");
            string text = File.ReadAllText(ContainerPath);

            var byGuid = VesselGuids().ToDictionary(kv => kv.Value, kv => kv.Key);
            var registered = new List<VesselClassType>();

            foreach (Match m in Regex.Matches(text, @"guid: (\w+), type: 3"))
            {
                if (!byGuid.TryGetValue(m.Groups[1].Value, out string name)) continue;
                Assert.IsTrue(System.Enum.TryParse(name, out VesselClassType vessel),
                    $"{name}.prefab is registered in the Vessel Prefab Container but is not a " +
                    "VesselClassType member. A vessel prefab's file name IS its registration key " +
                    "(CONTRACT.md §1.2) — rename the prefab to match the enum.");
                if (!registered.Contains(vessel)) registered.Add(vessel);
            }

            Assert.IsNotEmpty(registered,
                "Resolved no vessels from the prefab container. Either the container was " +
                "re-authored in a shape this test cannot read, or the vessel prefabs moved — " +
                "either way this gate is now vacuous and must be repaired, not deleted.");
            return registered;
        }

        [Test]
        public void EverySpawnableVesselIsOfferedByTheToybox()
        {
            var missing = RegisteredVessels()
                          .Where(v => !ToyVesselRoster.Default.Contains(v))
                          .ToList();

            Assert.IsEmpty(missing,
                $"{string.Join(", ", missing)} " +
                (missing.Count == 1 ? "is" : "are") +
                " registered in the Vessel Prefab Container but missing from " +
                "ToyVesselRoster.Default, so the Vessel Changer and the Spawn Matrix's " +
                "hangar will never offer " + (missing.Count == 1 ? "it" : "them") +
                " and the hull cannot be flown in freestyle. Add the class to " +
                "ToyVesselRoster.Default (Assets/_Scripts/Controller/Toys/ToyVesselRoster.cs). " +
                "It is the one vessel registration that is code rather than an asset, so no " +
                "setup tool writes it for you.");
        }

        [Test]
        public void TheRosterNamesRealHullsOnlyOnce()
        {
            var seen = new HashSet<VesselClassType>();
            foreach (var vessel in ToyVesselRoster.Default)
            {
                Assert.IsFalse(vessel is VesselClassType.Any or VesselClassType.Random,
                    $"ToyVesselRoster.Default names the meta value {vessel}, which is not a hull. " +
                    "Resolve() drops it silently, so the roster reads as one longer than it is.");
                Assert.IsTrue(seen.Add(vessel),
                    $"ToyVesselRoster.Default names {vessel} twice. Resolve() de-duplicates, so " +
                    "the duplicate is invisible at runtime and survives until somebody edits the " +
                    "list believing it is a set.");
            }
        }
    }
}
#endif
