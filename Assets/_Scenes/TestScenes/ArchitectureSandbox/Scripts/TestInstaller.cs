
using UnityEngine;
using Zenject;

namespace CosmicShore.TestScenes.ArchitectureSandbox.Scripts
{
    public class TestInstaller: MonoInstaller
    {
        private const string BindingData = "Testing123";
        public override void InstallBindings()
        {
            Container.BindInstance(BindingData);
            Container.Bind<TestRunner>().NonLazy();
        }
    }
}

public class TestRunner
{
    public TestRunner(string message)
    {
        Debug.Log($"Test runner: {message}");
    }
}