#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using Reflex.Core;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// AppManager's bindings live in the root container only. The instance in the Bootstrap scene is
    /// also asked to install, and doing so gave every lazy singleton a second copy: the second
    /// ApplicationStateMachine put the app state back to None ("Invalid transition: None → MainMenu").
    /// </summary>
    [TestFixture]
    public class AppManagerInstallBindingsTests
    {
        const string PrefabPath = "Assets/_Prefabs/CORE/AppManager.prefab";

        [Test]
        public void SceneInstance_InstallsNothing()
        {
            var go = new GameObject("TestAppManagerInstance");
            try
            {
                var manager = go.AddComponent<AppManager>();
                var builder = new ContainerBuilder();

                manager.InstallBindings(builder);

                using var container = builder.Build();
                Assert.IsFalse(container.HasBinding<ApplicationStateMachine>(),
                    "A scene instance registered ApplicationStateMachine, so the scene's container " +
                    "holds a second copy of the root's singletons.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PrefabAsset_InstallsTheServices()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AppManager>(PrefabPath);
            Assert.IsNotNull(asset, $"No AppManager on {PrefabPath}.");

            // The manager sweep would add DontDestroyOnLoad to whatever scene is open.
            var resolved = typeof(AppManager).GetField("_resolved", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(resolved, "AppManager._resolved was renamed; update this test.");
            object saved = resolved.GetValue(asset);
            resolved.SetValue(asset, true);
            try
            {
                var builder = new ContainerBuilder();

                asset.InstallBindings(builder);

                using var container = builder.Build();
                Assert.IsTrue(container.HasBinding<ApplicationStateMachine>());
                Assert.IsTrue(container.HasBinding<AuthenticationServiceFacade>());
            }
            finally
            {
                resolved.SetValue(asset, saved);
            }
        }
    }
}
#endif
