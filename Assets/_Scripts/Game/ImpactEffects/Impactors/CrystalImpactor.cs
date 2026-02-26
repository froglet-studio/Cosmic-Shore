using System;
using UnityEngine;
using CosmicShore.Models.Enums;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.ImpactEffects
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