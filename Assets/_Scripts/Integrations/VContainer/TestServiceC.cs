namespace CosmicShore.Integrations.VContainer
{
    public class TestServiceC : IServiceC
    {
        private int _prefix = 12;
        public int SumGenerator(int suffix)
        {
            return _prefix + suffix;
        }
    }
}