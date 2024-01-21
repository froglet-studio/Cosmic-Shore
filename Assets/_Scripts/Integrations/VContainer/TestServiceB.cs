using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class TestServiceB : IServiceB
    {
        private readonly IServiceA _testServiceA;

        public TestServiceB(IServiceA testServiceA)
        {
            _testServiceA = testServiceA ;
        }
        public void Call()
        {
            _testServiceA.Call();
            Debug.Log("TestServiceB.Call() is called.");
        }
    }
}