using System;

namespace CosmicShore.Integrations.VContainer
{
    public class TestModelA : IModelA
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
    }
}