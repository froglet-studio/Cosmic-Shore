using CosmicShore.Game.Environment;
using System;
using System.Reflection;
using UnityEngine;
using CosmicShore.Game.IO;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Multiplayer;
using CosmicShore.Game.Player;
using CosmicShore.Game.Prisms;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Game.UI;
using CosmicShore.Models.Enums;
using CosmicShore.Models.ScriptableObjects;
using CosmicShore.UI.Modals;
using CosmicShore.Utility.DataContainers;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.SOAP;
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
