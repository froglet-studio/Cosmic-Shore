using CosmicShore.Core;
using CosmicShore.Game.Environment;
using System;
using System.Reflection;
using UnityEngine;
using CosmicShore.Game.Environment.CellModifiers;
using CosmicShore.Game.Environment.Cytoplasm;
using CosmicShore.Game.Environment.FlowField;
using CosmicShore.Game.Environment.MiniGameObjects;
using CosmicShore.Game.IO;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.ImpactEffects.Containers;
using CosmicShore.Game.ImpactEffects.EffectsSO;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.EffectsSO.ProjectileCrystalEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.ProjectileEndEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.ProjectileMineEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.ProjectilePrismEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.SkimmerPrismEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselCrystalEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselExplosionEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselPrismEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselProjectileEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselSkimmerEffects;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Multiplayer;
using CosmicShore.Game.Player;
using CosmicShore.Game.Prisms;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship.R_ShipActions.DataContainers;
using CosmicShore.Game.Ship.R_ShipActions.Executors;
using CosmicShore.Game.Ship.ShipActions;
using CosmicShore.Game.UI;
using CosmicShore.Models.Enums;
using CosmicShore.Models.ScriptableObjects;
using CosmicShore.UI.Modals;
using CosmicShore.Utility.DataContainers;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.SOAP.ScriptableClassType;
using CosmicShore.VesselHUD.Controller;
using CosmicShore.VesselHUD.Interfaces;
using CosmicShore.VesselHUD.View;
namespace CosmicShore.Game.Ship
{
    public class ElementalShipComponent : MonoBehaviour
    {
        readonly BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        public void BindElementalFloats(IVessel vessel)
        {
            Type thisType = GetType();
            FieldInfo[] fields = thisType.GetFields(bindingFlags);

            // Find all ElementalFloat Fields
            foreach (FieldInfo fieldInfo in fields)
            {
                if (fieldInfo.FieldType == typeof(ElementalFloat))
                {
                    // Assign the ElementalFloat fields name and vessel properties
                    var elementalFloatInstance = thisType.GetField(fieldInfo.Name, bindingFlags).GetValue(this);
                    typeof(ElementalFloat).GetProperty("Name").SetValue(elementalFloatInstance, GetType().Name + "." + fieldInfo.Name);
                    typeof(ElementalFloat).GetProperty("Vessel").SetValue(elementalFloatInstance, vessel);
                }
            }
        }
    }
}
