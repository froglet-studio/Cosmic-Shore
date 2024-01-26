using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class TestFactory
    {
        private readonly IServiceC _testServiceC;
        public TestFactory(IServiceC testServiceC)
        {
            _testServiceC = testServiceC;
        }

        public IModelC Creat(int b)
        {
            var modelC = new TestModelC(b, _testServiceC);
            Debug.Log($"TestFactory.Creat() get a new model c - uuid: {modelC.Uuid}");
            return modelC;
        }
    }
}