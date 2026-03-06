using System;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
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