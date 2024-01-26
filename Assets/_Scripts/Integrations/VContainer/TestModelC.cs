namespace CosmicShore.Integrations.VContainer
{
    public class TestModelC : IModelC
    {
        private int _uuid;
        public int Uuid => _uuid;
        public TestModelC(int prefix, IServiceC testServiceC)
        {
            _uuid = testServiceC.SumGenerator(prefix);
        }
    }
}