#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Ark's model binding (<see cref="ArkModel"/>, <see cref="ArkRigTable"/>, <see cref="ArkModelDefinitionSO"/>):
    /// the committed rig table describes a hull the game can lay (every socket a known prism kind, a real pose and size,
    /// riding a bone the table puppets or the hull), the takes are unit rotations, the blend and the gimbal composition
    /// do what the model's takes did in Blender, and every Arkway voyage is given the model.
    /// </summary>
    public class ArkModelTests
    {
        const string RigTablePath = "Assets/_Models/Vessel Models/ArkReverse.rig.json";
        const string ModelPath = "Assets/_Models/Vessel Models/ArkReverse.fbx";
        const string DefinitionPath = "Assets/_SO_Assets/Toys/ArkReverseModel.asset";
        const string ArkwaySettingsPath = "Assets/_SO_Assets/Toys/Wander_WithArk.asset";
        const string DomainPlaceholderPath = "Assets/_Graphics/Materials/GreenAccentVesselMaterial.mat";

        static ArkRigTable LoadTable()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(RigTablePath);
            Assert.IsNotNull(text, $"{RigTablePath} is missing - the model's build writes it beside the FBX");
            var table = ArkRigTable.Parse(text.text, out string error);
            Assert.IsNotNull(table, error);
            return table;
        }

        [Test]
        public void RigTable_EverySocketIsALayableHullPrism()
        {
            var table = LoadTable();
            var puppeted = new HashSet<string>(table.bones.Select(b => b.bone));
            var names = new HashSet<string>();
            foreach (var s in table.sockets)
            {
                Assert.IsTrue(names.Add(s.name), $"socket {s.name} is listed twice");
                CollectionAssert.Contains(new[] { "Plain", "Danger", "Shielded", "SuperShielded" }, s.kind, $"{s.name}: unknown kind '{s.kind}'");
                Assert.AreEqual(1f, ArkRigTable.V3(s.forward).magnitude, 1e-3f, $"{s.name}: forward is not a unit vector");
                Assert.AreEqual(1f, ArkRigTable.V3(s.up).magnitude, 1e-3f, $"{s.name}: up is not a unit vector");
                Assert.AreEqual(0f, Vector3.Dot(ArkRigTable.V3(s.forward), ArkRigTable.V3(s.up)), 1e-3f, $"{s.name}: up is not square to forward");
                var size = s.Size;
                Assert.IsTrue(size.x > 0f && size.y > 0f && size.z > 0f, $"{s.name}: size {size}");
                Assert.IsTrue(s.RidesHull || puppeted.Contains(s.bone), $"{s.name} rides '{s.bone}', a bone the table does not puppet");
            }
            // Every tier the model was cut for is present: the hull's health is all four kinds.
            foreach (var kind in new[] { PrismKind.Plain, PrismKind.Danger, PrismKind.Shielded, PrismKind.SuperShielded })
                Assert.IsTrue(table.sockets.Any(s => s.Kind == kind), $"no {kind} socket");
        }

        [Test]
        public void RigTable_TakesAreUnitRotations_AndEveryInputMovesSomething()
        {
            var table = LoadTable();
            var fields = new (string name, System.Func<ArkRigTable.Bone, float[]> get)[]
            {
                ("pitchUp", b => b.pitchUp), ("pitchDown", b => b.pitchDown), ("yawRight", b => b.yawRight), ("yawLeft", b => b.yawLeft),
                ("rollRight", b => b.rollRight), ("rollLeft", b => b.rollLeft), ("speedUp", b => b.speedUp), ("speedDown", b => b.speedDown),
            };
            foreach (var (name, get) in fields)
            {
                int moved = 0;
                foreach (var bone in table.bones)
                {
                    var raw = get(bone);
                    if (raw == null || raw.Length == 0) continue;
                    Assert.AreEqual(4, raw.Length, $"{bone.bone}.{name}");
                    float n = Mathf.Sqrt(raw[0] * raw[0] + raw[1] * raw[1] + raw[2] * raw[2] + raw[3] * raw[3]);
                    Assert.AreEqual(1f, n, 1e-3f, $"{bone.bone}.{name} is not a unit quaternion");
                    if (Quaternion.Angle(Quaternion.identity, ArkRigTable.Q(raw)) > 1f) moved++;
                }
                Assert.Greater(moved, 0, $"no bone moves for {name}");
            }
        }

        [Test]
        public void Blend_IsIdentityAtRest_TheTakeAtFull_AndNeverPastIt()
        {
            var up = Quaternion.AngleAxis(12f, Vector3.right);
            var down = Quaternion.AngleAxis(-12f, Vector3.right);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, ArkRigTable.Blend(up, down, 0f)), 1e-3f);
            Assert.AreEqual(0f, Quaternion.Angle(up, ArkRigTable.Blend(up, down, 1f)), 1e-3f);
            Assert.AreEqual(0f, Quaternion.Angle(down, ArkRigTable.Blend(up, down, -1f)), 1e-3f);
            Assert.AreEqual(6f, Quaternion.Angle(Quaternion.identity, ArkRigTable.Blend(up, down, 0.5f)), 1e-2f);
            Assert.AreEqual(0f, Quaternion.Angle(up, ArkRigTable.Blend(up, down, 3f)), 1e-3f, "a take must not play past full");
        }

        [Test]
        public void Budget_NoBoneTravelsFurtherThanOneFullTake()
        {
            // A jet has pitch and bank takes (both about its x axis); a full pitch with a full banked yaw shares one take.
            var jet = new Vector4(1f, 0f, 1f, 0f);
            var shared = ArkRigTable.Budget(new Vector4(1f, -1f, -0.8f, 1f), jet);
            Assert.AreEqual(1f, Mathf.Abs(shared.x) + Mathf.Abs(shared.z), 1e-5f);
            Assert.AreEqual(1f / 1.8f, shared.x, 1e-5f);
            Assert.AreEqual(-0.8f / 1.8f, shared.z, 1e-5f, "the sign (which take) is kept");
            // Inputs the bone has no take for do not eat its budget, and a blend inside one take is left alone.
            Assert.AreEqual(new Vector4(0.6f, 1f, 0.3f, -1f), ArkRigTable.Budget(new Vector4(0.6f, 1f, 0.3f, -1f), jet));
            Assert.AreEqual(new Vector4(0.5f, 0f, 0.5f, 0f), ArkRigTable.Budget(new Vector4(0.5f, 0f, 0.5f, 0f), jet));
        }

        [Test]
        public void PoseLocal_ComposesLikeAGimbal()
        {
            // A ring yawed by the yaw take carries its jet; the jet's pitch take is about the model's x axis at rest, so
            // under the yawed ring it pitches about the ring's YAWED x axis - the jet's model-space rotation is
            // yaw * pitch * rest.
            var ringRest = Quaternion.Euler(0f, 30f, 0f);
            var jetRestLocal = Quaternion.Euler(5f, -10f, 3f);
            var jetRestInModel = ringRest * jetRestLocal;
            var yaw = Quaternion.AngleAxis(14f, Vector3.up);
            var pitch = Quaternion.AngleAxis(-12f, Vector3.right);

            var ringNow = yaw * ringRest;                              // the ring's own pose (its parent is the hull)
            Assert.AreEqual(0f, Quaternion.Angle(ringNow, ArkRigTable.PoseLocal(ringRest, ringRest, yaw)), 1e-3f);
            var jetNow = ringNow * ArkRigTable.PoseLocal(jetRestLocal, jetRestInModel, pitch);
            Assert.AreEqual(0f, Quaternion.Angle(jetNow, yaw * pitch * jetRestInModel), 1e-2f);
        }

        [Test]
        public void ParseKind_AndParse_RejectWhatIsNotATable()
        {
            Assert.AreEqual(PrismKind.SuperShielded, ArkRigTable.ParseKind("SuperShielded"));
            Assert.AreEqual(PrismKind.Plain, ArkRigTable.ParseKind("nonsense"));
            Assert.IsNull(ArkRigTable.Parse("", out var e1));
            Assert.IsNotNull(e1);
            Assert.IsNull(ArkRigTable.Parse("{\"model\":\"x\"}", out var e2), "a table with no sockets is not a hull");
            Assert.IsNotNull(e2);
        }

        [Test]
        public void EveryArkwayVoyage_IsGivenTheArkModel()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ArkModelDefinitionSO>(DefinitionPath);
            Assert.IsNotNull(definition, DefinitionPath);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), definition.Model, "the definition draws ArkReverse.fbx");
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<TextAsset>(RigTablePath), definition.RigTable, "the definition binds the model's own rig table");
            Assert.Contains(AssetDatabase.LoadAssetAtPath<Material>(DomainPlaceholderPath), definition.DomainReplacesMaterials.ToList(),
                "the domain must replace the Domain slot's placeholder, or the Ark's trim is green in every domain");

            foreach (var guid in AssetDatabase.FindAssets("t:ArkwaySettingsSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<ArkwaySettingsSO>(path);
                Assert.AreEqual(definition, settings.BuildConfig("test").ArkModel, $"{path} does not draw its Ark as the model");
            }
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<ArkwaySettingsSO>(ArkwaySettingsPath), ArkwaySettingsPath);
        }

        [Test]
        public void Model_HasEveryBoneTheTablePuppetsAndEverySocketRides()
        {
            var table = LoadTable();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Assert.IsNotNull(model, ModelPath);
            var names = new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t => t.name));
            foreach (var bone in table.bones)
                Assert.IsTrue(names.Contains(bone.bone), $"the model has no bone '{bone.bone}' (optimizeBones strips a bone that skins nothing)");
            foreach (var socket in table.sockets.Where(s => !s.RidesHull))
                Assert.IsTrue(names.Contains(socket.bone), $"socket {socket.name} rides '{socket.bone}', which the model lacks");
        }
    }
}
#endif
