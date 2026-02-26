using CosmicShore.Game.Environment;
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
