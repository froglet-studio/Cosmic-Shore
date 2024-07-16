using System;
using UnityEngine;

namespace CosmicShore.Models.Enums
{
    [Serializable]
    public struct ResourceCollection
    {
        [SerializeField] public float Mass;
        [SerializeField] public float Charge;
        [SerializeField] public float Space;
        [SerializeField] public float Time;

        public ResourceCollection(float mass, float charge, float space, float time)
        {
            Mass = mass;
            Charge = charge;
            Space = space;
            Time = time;
        }
    }
}