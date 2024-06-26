using UnityEngine;

namespace CosmicShore
{
    public abstract class Fauna : LifeForm
    {
        public float aggression; 

        protected abstract void Spawn();

        protected override void Start()
        {
            base.Start();
        }

    }
}
