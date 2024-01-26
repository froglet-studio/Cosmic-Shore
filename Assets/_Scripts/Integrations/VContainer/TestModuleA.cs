using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CosmicShore.Integrations.VContainer
{
    public class TestModuleA : IStartable
    {
        private readonly IServiceA _testServiceA;
        private readonly IServiceB _testServiceB;
        private readonly TestComponentA _testComponentA;
        private readonly TestFactory _testFactory;

        public TestModuleA(IServiceA serviceA, IServiceB serviceB, TestComponentA componentA, IObjectResolver container)
        {
            _testServiceA = serviceA;
            _testServiceB = serviceB;
            _testComponentA = componentA;
            _testFactory = container.Resolve<TestFactory>();
        }

        public void Start()
        {
            Debug.Log("Module A is calling test service A.");
            _testServiceA.Call();
            Debug.Log("Module A is calling test service B.");
            _testServiceB.Call();
            Debug.Log("Module A is calling test factory.");
            var modelC1 = _testFactory.Creat(1);
            var modelC2 = _testFactory.Creat(2);
            var modelC3 = _testFactory.Creat(3);
            Debug.Log($"{_testComponentA.name} started from Module A.");
            
        }
    }
}