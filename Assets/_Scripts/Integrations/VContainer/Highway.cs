using System.Collections.Generic;
using VContainer.Unity;

namespace CosmicShore.Integrations.VContainer
{
    public class Highway : IStartable
    {
        // IEnumerable also works for collections
        private IReadOnlyList<IVehicle> _vehicles;
        
        public Highway(IReadOnlyList<IVehicle> vehicles)
        {
            _vehicles = vehicles;
        }

        public void Start()
        {
            foreach (var vehicle in _vehicles)
            {
                vehicle.Run(10);
            }
        }
    }
}