using UnityEngine;
using VContainer.Unity;

namespace CosmicShore.Integrations.VContainer
{
    public class ModuleA : IStartable
    {
        private readonly IServiceA _testServiceA;
        private readonly IServiceB _testServiceB;
        private readonly TestComponentA _testComponentA;

        public ModuleA(IServiceA serviceA, IServiceB serviceB, TestComponentA componentA)
        {
            _testServiceA = serviceA;
            _testServiceB = serviceB;
            _testComponentA = componentA;
        }

        public void Start()
        {
            Debug.Log("Module A is calling test service A.");
            _testServiceA.Call();
            Debug.Log("Module B is calling test service B.");
            _testServiceB.Call();
            Debug.Log($"{_testComponentA.name} started from Module A.");
        }
    }
}