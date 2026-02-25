using System;
using CosmicShore.Game.Ship;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Models.Enums;
using CosmicShore.Utility.DataContainers;
using CosmicShore.Utility.SOAP.ScriptableAbilityStats;
using CosmicShore.Utility.SOAP.ScriptableAuthenticationData;
using CosmicShore.Utility.SOAP.ScriptableCrystalStats;
using CosmicShore.Utility.SOAP.ScriptableEventWithReturn;
using CosmicShore.Utility.SOAP.ScriptablePrismStats;
using CosmicShore.Utility.SOAP.ScriptableQuaternion;
using CosmicShore.Utility.SOAP.ScriptableShipHUDData;
using CosmicShore.Utility.SOAP.ScriptableSilhouetteData;
using CosmicShore.Utility.SOAP.ScriptableTransform;
using CosmicShore.Game.Environment.FloraAndFauna;
namespace CosmicShore.Game.Environment
{
    public class HealthPrism : Prism
    {
        public LifeForm LifeForm;

        [Header("Optional Components")]
        [SerializeField] Spindle spindle;

        public override void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            base.Initialize(playerName);
            if (LifeForm)
                LifeForm.AddHealthBlock(this);

            // Spindle logic disabled for now
            spindle ??= transform.parent.GetComponent<Spindle>(); // Every healthPrism requires a spindle parent
            if (spindle) spindle.AddHealthBlock(this);
        }

        public void Reparent(Transform newParent)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            if (spindle) spindle.RemoveHealthBlock(this);

            transform.parent = newParent;

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this);

            if (spindle) spindle.CheckForLife();
        }

        protected override void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            if (spindle) spindle.RemoveHealthBlock(this);

            base.Explode(impactVector, domain, playerName, devastate);

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this, playerName);

            if (spindle) spindle.CheckForLife();
        }
        
        protected override void Implode(Transform targetTransform, Domains domain, string playerName, bool devastate = false)
        {
            spindle ??= transform.parent.GetComponent<Spindle>();
            if (spindle) spindle.RemoveHealthBlock(this);

            base.Implode(targetTransform, domain, playerName, devastate);

            if (LifeForm)
                LifeForm.RemoveHealthBlock(this);

            if (spindle) spindle.CheckForLife();
        }
    }
}