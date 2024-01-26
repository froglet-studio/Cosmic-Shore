using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class Truck : IVehicle
    {
        private float _speed = 1.5f;
        private string _cargo = "banana";
        
        public void Run(float time)
        {
            Debug.Log($"Truck.Run() carries {_cargo} for distance: {_speed*time}m.");
        }
    }
}