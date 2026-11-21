using CosmicShore.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
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

            SetCollidersEnabled(true);
        }

        IEnumerator TemporaryIntangibilityCoroutine()
        {
            SetCollidersEnabled(false);

            yield return new WaitForSeconds(maxDuration);

            SetCollidersEnabled(true);
        }

        // A ship geometry need not carry a collider: a rigged model's SkinnedMeshRenderer is
        // listed for its domain paint (the Urchin since its 2026-10-18 rig swap) beside the
        // collider-bearing parts.
        void SetCollidersEnabled(bool enabled)
        {
            foreach (var geometry in shipGeometries)
                if (geometry && geometry.TryGetComponent(out Collider collider))
                    collider.enabled = enabled;
        }
    }
}
