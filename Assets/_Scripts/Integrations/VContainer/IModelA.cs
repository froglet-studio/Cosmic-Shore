using System;

namespace CosmicShore.Integrations.VContainer
{
    public interface IModelA
    {
        int Id { get; set; }
        string Name { get; set; }
        DateTime StartDate { get; set; }
    }
}