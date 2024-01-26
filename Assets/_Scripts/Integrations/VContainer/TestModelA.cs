using System;

namespace CosmicShore.Integrations.VContainer
{
    public interface IModel
    {
        int Id { get; set; }
        string Name { get; set; }
        DateTime StartDate { get; set; }
    }

    public class TestModelA : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
    }
}