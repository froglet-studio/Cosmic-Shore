#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The gate for the AI's aim telegraph (<see cref="IAimTelegraphAction"/>).
    ///
    /// <see cref="AIPilot"/> holds a vessel's telegraph for the length of a drift onto its
    /// objective, and it finds that ability entirely through the vessel's own BINDINGS
    /// (<c>R_VesselActionHandler.TryGetInputForAction</c>). That indirection is what keeps one
    /// vessel's ability out of the shared AI — and it is also why the whole behaviour can be turned
    /// off by an asset edit that mentions neither the AI nor the interface: unbind the action from
    /// every control and the lookup simply returns false, forever, silently, on a code path that
    /// is written to treat "this vessel has no telegraph" as the normal case.
    ///
    /// So the invariant is asserted from the assets: an ability declared as a telegraph must be
    /// reachable on at least one vessel.
    /// </summary>
    public class AimTelegraphBindingTests
    {
        const string VesselPrefabFolder = "Assets/_Prefabs/Spacevessels";

        static List<ShipActionSO> TelegraphActions() =>
            AssetDatabase.FindAssets("t:ShipActionSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ShipActionSO>)
                .Where(a => a is IAimTelegraphAction)
                .ToList();

        /// <summary>
        /// Every action bound to any control on a vessel, across the shared map and both device
        /// override maps. Read through SerializedObject rather than the runtime dictionaries
        /// because those are built in <c>Initialize</c> and an asset-only test never runs it.
        ///
        /// Loads the prefab as an ASSET (not via PrefabUtility.LoadPrefabContents), which already
        /// carries the merged hierarchy and does not open a preview scene per vessel.
        /// </summary>
        static IEnumerable<Object> BoundActions(GameObject vesselPrefab)
        {
            foreach (var handler in vesselPrefab.GetComponentsInChildren<R_VesselActionHandler>(true))
            {
                var so = new SerializedObject(handler);
                foreach (var mapName in new[] { "_inputEventShipActions", "_touchActionOverrides", "_gamepadActionOverrides" })
                {
                    var map = so.FindProperty(mapName);
                    if (map == null || !map.isArray) continue;

                    for (int i = 0; i < map.arraySize; i++)
                    {
                        var actions = map.GetArrayElementAtIndex(i).FindPropertyRelative("ShipActions");
                        if (actions == null || !actions.isArray) continue;

                        for (int j = 0; j < actions.arraySize; j++)
                        {
                            var reference = actions.GetArrayElementAtIndex(j).objectReferenceValue;
                            if (reference != null) yield return reference;
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryAimTelegraphAction_IsBoundOnSomeVessel()
        {
            var telegraphs = TelegraphActions();
            Assert.IsNotEmpty(telegraphs,
                "No ShipActionSO in the project implements IAimTelegraphAction. The interface is the " +
                "only thing AIPilot looks for, so an AI can no longer announce its aim on any vessel. " +
                "If the last telegraph was deliberately retired, delete the interface and the AI's " +
                "EngageAimTelegraph/ReleaseAimTelegraph with it rather than leaving dead machinery.");

            var bound = new HashSet<Object>();
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { VesselPrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;
                foreach (var action in BoundActions(prefab)) bound.Add(action);
            }

            var orphans = telegraphs.Where(t => !bound.Contains(t)).Select(t => t.name).ToList();
            Assert.IsEmpty(orphans,
                $"Aim telegraph action(s) bound to no control on any vessel in {VesselPrefabFolder}: " +
                $"{string.Join(", ", orphans)}. AIPilot resolves the telegraph through the vessel's " +
                "own bindings, and an unbound one makes TryGetInputForAction return false - which the " +
                "AI correctly treats as 'this vessel has no telegraph' and never mentions again. " +
                "Bind it, or drop the IAimTelegraphAction interface from it.");
        }
    }
}
#endif
