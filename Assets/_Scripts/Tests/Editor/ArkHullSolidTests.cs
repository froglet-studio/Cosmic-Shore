#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Ark as a solid ship (<see cref="ArkHullSolid"/>): the body, the jets and the spine are inside the volume, the
    /// hangar and the ring tunnel are the one open passage, no straight line through the hull's middle gets through, and
    /// the bulkhead that closes the stern gape is a real mesh in the gape.
    /// </summary>
    public class ArkHullSolidTests
    {
        const string RigTablePath = "Assets/_Models/Vessel Models/ArkReverse.rig.json";
        const string DefinitionPath = "Assets/_SO_Assets/Toys/ArkReverseModel.asset";

        static ArkRigTable LoadTable()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(RigTablePath);
            Assert.IsNotNull(text, RigTablePath);
            var table = ArkRigTable.Parse(text.text, out string error);
            Assert.IsNotNull(table, error);
            return table;
        }

        [Test]
        public void TheBodyAndTheSpineAreSolid()
        {
            Assert.Less(ArkHullSolid.Distance(new Vector3(0f, 0f, 0f)), 0f, "the mantle cavity under the shell");
            Assert.Less(ArkHullSolid.Distance(new Vector3(0f, 10f, 30f)), 0f, "the umbo");
            Assert.Less(ArkHullSolid.Distance(new Vector3(50f, 0f, -40f)), 0f, "the fan, off the centreline");
            Assert.Less(ArkHullSolid.Distance(new Vector3(10f, -15f, -60f)), 0f, "the spine beside the tunnel's exit run");
            Assert.Less(ArkHullSolid.Distance(new Vector3(27f, -17.5f, 80f)), 0f, "a jaw");
            Assert.Greater(ArkHullSolid.Distance(new Vector3(0f, 0f, 400f)), 0f, "open water ahead");
            Assert.Greater(ArkHullSolid.Distance(new Vector3(0f, 80f, 0f)), 0f, "open water above");
            Assert.Greater(ArkHullSolid.Distance(new Vector3(200f, 0f, 0f)), 0f, "open water abeam");
        }

        [Test]
        public void TheEnginesAreSolid()
        {
            var table = LoadTable();
            var jets = table.bones.Where(b => b != null && b.bone.StartsWith("engine.") && !b.bone.EndsWith(".ring")).ToList();
            Assert.AreEqual(8, jets.Count, "eight jets in the rig table");
            foreach (var jet in jets)
            {
                Assert.Less(ArkHullSolid.Distance(jet.Head), 0f, $"{jet.bone}'s pivot is inside the hull");
                // and down the jet's barrel, aft of the pivot: no flying in through a nozzle
                var aft = jet.Head + new Vector3(jet.Head.x, 0f, jet.Head.z - 44f).normalized * 12f;
                Assert.Less(ArkHullSolid.Distance(aft), 0f, $"{jet.bone}'s barrel is solid");
            }
        }

        [Test]
        public void TheTunnelIsTheOnePassage()
        {
            for (int i = 0; i < ArkHullSolid.TunnelPointCount; i++)
            {
                var p = ArkHullSolid.TunnelPointLocal(i);
                Assert.Greater(ArkHullSolid.Distance(p), ArkHullSolid.VesselMargin, $"tunnel point {i} at {p} is open");
                Assert.IsTrue(ArkHullSolid.InPassage(p), $"tunnel point {i} is the passage");
            }
            Assert.Greater(ArkHullSolid.Distance(new Vector3(0f, -17.5f, 95f)), 0f, "the hangar mouth is open");
            Assert.Greater(ArkHullSolid.Distance(new Vector3(0f, -17.5f, 60f)), 0f, "the hangar bay is open");
        }

        [Test]
        public void NoStraightLineCrossesTheHullsMiddle()
        {
            // Bow to stern, side to side, top to bottom through the hull's middle: every line meets the solid.
            for (float x = -60f; x <= 60f; x += 10f)
                for (float y = -6f; y <= 8f; y += 2f)
                    Assert.IsTrue(Blocked(new Vector3(x, y, 160f), new Vector3(x, y, -160f)), $"bow to stern at x {x} y {y}");
            for (float z = -60f; z <= 40f; z += 10f)
                for (float y = -6f; y <= 8f; y += 2f)
                    Assert.IsTrue(Blocked(new Vector3(-200f, y, z), new Vector3(200f, y, z)), $"beam at z {z} y {y}");
            for (float x = -40f; x <= 40f; x += 10f)
                for (float z = -60f; z <= 30f; z += 10f)
                    Assert.IsTrue(Blocked(new Vector3(x, 80f, z), new Vector3(x, -80f, z)), $"top to bottom at x {x} z {z}");
        }

        static bool Blocked(Vector3 a, Vector3 b)
        {
            const int steps = 800;
            for (int i = 0; i <= steps; i++)
                if (ArkHullSolid.Distance(Vector3.Lerp(a, b, i / (float)steps)) < 0f) return true;
            return false;
        }

        [Test]
        public void TheBulkheadClosesTheSternGape()
        {
            var mesh = ArkHullSolid.BulkheadMesh();
            Assert.AreEqual(2, mesh.subMeshCount);
            Assert.Greater(mesh.GetTriangles(0).Length, 0, "the wall");
            Assert.Greater(mesh.GetTriangles(1).Length, 0, "the ports");
            foreach (var v in mesh.vertices)
            {
                // Unity -> the hinge frame: rho from the hinge at z 44, every vertex on the wall's radius
                float rho = new Vector2(v.x, 44f - v.z).magnitude;
                Assert.AreEqual(ArkHullSolid.BulkheadRho, rho, 0.3f, $"vertex {v} sits on the bulkhead");
            }
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void TheLookIsToned()
        {
            var def = AssetDatabase.LoadAssetAtPath<ArkModelDefinitionSO>(DefinitionPath);
            Assert.IsNotNull(def, DefinitionPath);
            Assert.That(def.DomainBrightness, Is.InRange(0.1f, 0.99f), "the domain seams are toned down, not off");
            Assert.That(def.EngineBrightness, Is.InRange(0.1f, 0.99f));
            Assert.That(def.WindowBrightness, Is.InRange(0.1f, 0.99f));
        }
    }
}
#endif
