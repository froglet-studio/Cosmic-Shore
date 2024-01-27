using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class TestServiceB : IServiceB
    {
        private readonly IServiceA _testServiceA;
        private readonly IModelB _testModelB;

        public TestServiceB(IServiceA testServiceA, IModelB testModelB)
        {
            _testServiceA = testServiceA ;
            _testModelB = testModelB;
        }
        public void Call()
        {
            _testServiceA.Call();
            Debug.Log("TestServiceB.Call() is called.");
            Debug.Log($"TestServiceB has test model b: " +
                      $"message {_testModelB.Message} ");
        }
    }
}