#if UNITY_EDITOR
using System.IO;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// ONE SIZE, ONE PETAL (Garrett, 2026-10-26): every elemental crystal - a lifeform's heart, a
    /// petal knocked out of a hull, a mode's arena scatter, a toy's pickup - is
    /// <see cref="ElementalCrystalSetSO.CrystalWorldScale"/> and grants exactly one petal.
    ///
    /// <para>The size is taken from the Elemental Converter (the omni combine on the Ark screen),
    /// whose crystals are the right size for an elemental crystal. These tests measure the
    /// converter's data and the game's own meshes, so the constant cannot drift from the picture
    /// it was taken from: if the omni prefab, the Charge prefab or the combine data changes, the
    /// constant has to move with it.</para>
    /// </summary>
    public class ElementalCrystalSizeTests
    {
        const string CombineDataPath = "Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json";
        const string OmniCrystalPath = "Assets/_Prefabs/Environment/Crystal.prefab";
        const string PickupAssetPath =
            "Assets/_SO_Assets/Effects/Skimmer Crystal Effects/SkimmerAdjustElementLevelByCrystalEffect.asset";

        static readonly string[] ElementalPrefabPaths =
        {
            "Assets/_Prefabs/Environment/CrystalCharge.prefab",
            "Assets/_Prefabs/Environment/CrystalMass.prefab",
            "Assets/_Prefabs/Environment/CrystalSpace.prefab",
            "Assets/_Prefabs/Environment/CrystalTime.prefab",
        };

        static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"{path} did not load");
            return prefab;
        }

        /// <summary>
        /// Half-extent of a crystal's BODY - its first model - per unit of the crystal's ROOT
        /// scale. The same measure <c>SkimRaceRingCrystalTests</c> uses.
        /// </summary>
        static float BodyHalfExtentPerRootScale(Crystal crystal)
        {
            Assert.IsTrue(crystal.CrystalModels is { Count: > 0 } && crystal.CrystalModels[0]?.model,
                $"'{crystal.name}' has no body model to measure");
            var body = crystal.CrystalModels[0].model;
            Mesh mesh = body.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh
                      : body.TryGetComponent(out SkinnedMeshRenderer smr) ? smr.sharedMesh : null;
            Assert.IsNotNull(mesh, $"'{crystal.name}' body '{body.name}' draws no mesh");

            var e = mesh.bounds.extents;
            var s = body.transform.lossyScale;
            float extent = Mathf.Max(Mathf.Abs(e.x * s.x), Mathf.Abs(e.y * s.y), Mathf.Abs(e.z * s.z));
            return extent / Mathf.Abs(crystal.transform.lossyScale.x);
        }

        /// <summary>The largest axis half-extent of an xyz-packed vertex array.</summary>
        static float HalfExtent(float[] xyz)
        {
            Assert.IsTrue(xyz is { Length: >= 3 }, "no vertices to measure");
            float best = 0f;
            for (int axis = 0; axis < 3; axis++)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = axis; i < xyz.Length; i += 3)
                {
                    lo = Mathf.Min(lo, xyz[i]);
                    hi = Mathf.Max(hi, xyz[i]);
                }
                best = Mathf.Max(best, 0.5f * (hi - lo));
            }
            return best;
        }

        [Test]
        public void CrystalWorldScale_IsTheConvertersElementalToOmniRatio()
        {
            Assert.IsTrue(File.Exists(CombineDataPath), $"{CombineDataPath} is missing");
            var data = JsonUtility.FromJson<OmniCombineData>(File.ReadAllText(CombineDataPath));
            Assert.IsNotNull(data?.omni?.verts, "the combine data has no omni");

            OmniCombineCrystalData charge = null;
            foreach (var c in data.crystals)
                if (c != null && c.element == "Charge") charge = c;
            Assert.IsNotNull(charge, "the combine data has no Charge crystal");

            // In the converter: the Charge crystal (the reference the game's four models are
            // equalized to) against the omni it combines into.
            float converterRatio = HalfExtent(charge.basis) / HalfExtent(data.omni.verts);

            // In the game: the fixed omni (Crystal.prefab at its own root scale) against a Charge
            // crystal at the one elemental size.
            var omni = LoadPrefab(OmniCrystalPath);
            float omniHalfExtent = BodyHalfExtentPerRootScale(omni.GetComponent<Crystal>()) *
                                   Mathf.Abs(omni.transform.localScale.x);
            float chargeHalfExtent = BodyHalfExtentPerRootScale(LoadPrefab(ElementalPrefabPaths[0]).GetComponent<Crystal>()) *
                                     ElementalCrystalSetSO.CrystalWorldScale;
            float gameRatio = chargeHalfExtent / omniHalfExtent;

            Assert.AreEqual(converterRatio, gameRatio, converterRatio * 0.03f,
                $"In the Elemental Converter an elemental crystal is {converterRatio:F3} of the omni's size; " +
                $"in the game it is {gameRatio:F3} ({chargeHalfExtent * 2f:F2} u across against the omni's " +
                $"{omniHalfExtent * 2f:F2} u). Move ElementalCrystalSetSO.CrystalWorldScale to " +
                $"{ElementalCrystalSetSO.CrystalWorldScale * converterRatio / gameRatio:F2}.");
        }

        [Test]
        public void EveryElementalPrefab_IsAuthoredAtTheOneSize()
        {
            // So a crystal instantiated and left alone is already the right size.
            foreach (var path in ElementalPrefabPaths)
                Assert.AreEqual(ElementalCrystalSetSO.CrystalWorldScale, LoadPrefab(path).transform.localScale.x, 1e-4f,
                    $"{path}'s root scale should be ElementalCrystalSetSO.CrystalWorldScale");
        }

        [Test]
        public void TheElementalSet_PointsAtThoseFourPrefabs()
        {
            var set = ElementalCrystalSetSO.Load();
            Assert.IsNotNull(set, $"Resources/{ElementalCrystalSetSO.ResourcePath} is missing");
            foreach (var element in new[] { Element.Charge, Element.Mass, Element.Space, Element.Time })
            {
                var prefab = set.GetPrefab(element);
                Assert.IsNotNull(prefab, $"the set has no {element} crystal");
                Assert.AreEqual(ElementalCrystalSetSO.CrystalWorldScale, prefab.transform.localScale.x, 1e-4f,
                    $"the set's {element} crystal is not authored at the one size");
            }
        }

        [Test]
        public void EveryElementalCrystal_GrantsExactlyOnePetal()
        {
            Assert.AreEqual(ResourceSystem.PetalNormalized,
                SkimmerAdjustElementLevelByCrystalEffectSO.LevelGainPerCrystal, 1e-7f,
                "an elemental crystal grants one petal, whatever its size");
        }

        [Test]
        public void ThePickupAsset_CarriesNoRetiredSizeToRewardKeys()
        {
            Assert.IsTrue(File.Exists(PickupAssetPath), $"{PickupAssetPath} is missing");
            string text = File.ReadAllText(PickupAssetPath);
            StringAssert.DoesNotContain("levelPerUnitScale", text,
                "the reward no longer reads the crystal's size; delete the retired key");
            StringAssert.DoesNotContain("maxLevelGainPerCrystal", text,
                "the reward no longer reads the crystal's size; delete the retired key");
        }
    }
}
#endif
