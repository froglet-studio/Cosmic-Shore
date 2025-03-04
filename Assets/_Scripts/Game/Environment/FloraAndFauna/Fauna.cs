using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public abstract class Fauna : LifeForm
    {
        [SerializeField] protected List<FaunaBehaviorOption> behaviorOptions;
        public int aggression;
        protected Vector3 TargetPosition;
        public Population Population;

        protected abstract void Spawn();
    }
}