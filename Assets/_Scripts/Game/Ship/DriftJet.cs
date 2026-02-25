using CosmicShore.Game.Environment;
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
using CosmicShore.UI.Views;
namespace CosmicShore.Game.Ship
{
    public class DriftJet : MonoBehaviour
    {
        [SerializeField] bool flip = false;

        [SerializeField] VesselStatus vesselStatus;
        private void Update()
        {
            if (vesselStatus.IsDrifting)
            {
                transform.rotation = Quaternion.Lerp(transform.rotation,Quaternion.LookRotation(vesselStatus.Course, transform.parent.forward),.06f);
            }
            else
            {
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(flip ? -transform.parent.right : transform.parent.right, transform.parent.forward), .06f);
            }
        }
    }
}
