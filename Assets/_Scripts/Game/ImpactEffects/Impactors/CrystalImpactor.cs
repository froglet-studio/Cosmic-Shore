using System;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent((typeof(Crystal)))]
    public abstract class CrystalImpactor : ImpactorBase
    {
        public Crystal Crystal;
        public override Domains OwnDomain => Crystal.ownDomain;

        protected virtual void Awake()
        {
            Crystal ??= GetComponent<Crystal>();
        }

        private void Reset()
        {
            Crystal ??= GetComponent<Crystal>();
        }
    }
}