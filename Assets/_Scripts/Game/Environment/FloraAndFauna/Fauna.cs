using UnityEngine;

namespace CosmicShore
{
    public abstract class Fauna : LifeForm
    {
        public int aggression;
        public Population Population;

        protected abstract void Spawn();

        protected override void Start()
        {
            base.Start();
        }

    }
}
