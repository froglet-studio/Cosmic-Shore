using UnityEngine;

namespace CosmicShore.Integrations.VContainer
{
    public class Car : IVehicle
    {
        private float _speed = 2.0f;
        private string _brand = "Tesla";

        public void Run(float time)
        {
            Debug.Log($"Car.Run() brand: {_brand} is running for distance: {_speed * time}");
        }
    }
}