using System;
using UnityEngine;

namespace CosmicShore.Core
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public ShipCameraOverrides ControlOverride { get; private set; }
        public ShipActions Action { get; private set; }
        public CrystalImpactEffects CrystalImpactEffect { get; private set; }

        public ShowIfAttribute(ShipCameraOverrides controlOverride) { ControlOverride = controlOverride; }
        public ShowIfAttribute(ShipActions action) { Action = action; }
        public ShowIfAttribute(CrystalImpactEffects crystalImpactEffect) { CrystalImpactEffect = crystalImpactEffect; }
    }
}