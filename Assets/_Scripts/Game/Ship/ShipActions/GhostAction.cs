using CosmicShore.Game.Ship;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Models.Enums;
namespace CosmicShore.Game.Ship.ShipActions
{
    public class GhostAction : ShipAction
    {
        [SerializeField] float maxDuration;
        List<GameObject> shipGeometries;

        Coroutine intangibilityCoroutine;

        public override void Initialize(IVessel vessel)
        {
            shipGeometries = Vessel.VesselStatus.ShipGeometries;
        }

        public override void StartAction()
        {
            intangibilityCoroutine = StartCoroutine(TemporaryIntangibilityCoroutine());
        }

        public override void StopAction()
        {
            if (intangibilityCoroutine != null)
            {
                StopCoroutine(intangibilityCoroutine);
                intangibilityCoroutine = null;
            }

            foreach (var geometry in shipGeometries)
                geometry.GetComponent<Collider>().enabled = true;
        }

        IEnumerator TemporaryIntangibilityCoroutine()
        {
            foreach (var geometry in shipGeometries)
                geometry.GetComponent<Collider>().enabled = false;

            yield return new WaitForSeconds(maxDuration);

            foreach (var geometry in shipGeometries)
                geometry.GetComponent<Collider>().enabled = true;
        }
    }
}
