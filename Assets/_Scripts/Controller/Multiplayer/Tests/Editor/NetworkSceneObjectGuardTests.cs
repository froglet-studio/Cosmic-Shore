#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicShore.Gameplay
{
    [TestFixture]
    public class NetworkSceneObjectGuardTests
    {
        [Test]
        public void SamePrefabStrippedTwice_WarnsOnce()
        {
            var reported = (HashSet<uint>)typeof(NetworkSceneObjectGuard)
                .GetField("_reported", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null);
            Assert.IsNotNull(reported, "NetworkSceneObjectGuard._reported was renamed; update this test.");
            reported.Clear();

            var first = new GameObject("Stray");
            var second = new GameObject("Stray");
            try
            {
                // Two instances of ONE prefab carry one GlobalObjectIdHash; the guard reports once per hash. Two
                // fresh GameObjects each get a hash of their own in the editor, so give them the same one here.
                var hashField = typeof(NetworkObject).GetField("GlobalObjectIdHash",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.IsNotNull(hashField, "NetworkObject.GlobalObjectIdHash was renamed; update this test.");
                const uint prefabHash = 0xC0FFEEu;
                hashField.SetValue(first.AddComponent<NetworkObject>(), prefabHash);
                hashField.SetValue(second.AddComponent<NetworkObject>(), prefabHash);

                LogAssert.Expect(LogType.Warning, new Regex(@"\[NetworkSceneObjectGuard\] Stripped"));

                Assert.IsTrue(NetworkSceneObjectGuard.NeutralizeStray(first, "test"));
                Assert.IsTrue(NetworkSceneObjectGuard.NeutralizeStray(second, "test"));

                LogAssert.NoUnexpectedReceived();
                Assert.IsFalse(first.TryGetComponent<NetworkObject>(out _));
                Assert.IsFalse(second.TryGetComponent<NetworkObject>(out _));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                reported.Clear();
            }
        }
    }
}
#endif
