using System;
using UnityEngine;

namespace StarWriter.Core
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public ShipControlOverrides ControlOverride { get; private set; }
        public ShipActions Action { get; private set; }
        public ShipLevelEffects LevelEffect { get; private set; }
        public CrystalImpactEffects CrystalImpactEffect { get; private set; }

        public ShowIfAttribute(ShipControlOverrides controlOverride) { ControlOverride = controlOverride; }
        public ShowIfAttribute(ShipActions action) { Action = action; }
        public ShowIfAttribute(ShipLevelEffects levelEffect) { LevelEffect = levelEffect; }
        public ShowIfAttribute(CrystalImpactEffects crystalImpactEffect) { CrystalImpactEffect = crystalImpactEffect; }
    }
}