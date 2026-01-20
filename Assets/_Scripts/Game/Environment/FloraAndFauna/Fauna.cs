using UnityEngine;

namespace CosmicShore
{
    public abstract class Fauna : LifeForm
    {
        public Population Population;
        //
        // [Header("Fauna Health Prism Size")]
        // [SerializeField] Vector3 healthPrismTargetScale = new Vector3(4f, 4f, 1f);

        // public override void AddHealthBlock(HealthPrism healthPrism)
        // {
        //     base.AddHealthBlock(healthPrism);
        //
        //     if (healthPrism)
        //         healthPrism.TargetScale = healthPrismTargetScale;
        // }

        protected abstract void Spawn();
    }
}