using CosmicShore.Core.HangerBuilder;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;

namespace CosmicShore
{
    public class SlowShipViewer : MonoBehaviour
    {
        Transform target;
        private LineRenderer lineRenderer;
        [SerializeField] Material trailViewerMaterial;
        Ship ship;
        // Start is called before the first frame update
        void Start()
        {
            ship = GetComponent<Ship>();  
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = trailViewerMaterial;
            lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
            
        }

        // Update is called once per frame
        void Update()
        {
            target = null;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.enabled = false;
            if (Hangar.Instance.SlowedShipTransforms.Count > 0 && Player.ActivePlayer && Player.ActivePlayer.Ship == ship)
            {
                var distance = float.PositiveInfinity;
                foreach (var shipTransform in Hangar.Instance.SlowedShipTransforms)
                {
                    if (shipTransform == transform) continue;
                    float tempDistance;     
                    tempDistance = (shipTransform.position - transform.position).magnitude;
                    if (tempDistance < distance)
                    {
                        distance = tempDistance;
                        target = shipTransform;
                    }
                }
                if (target != null)
                {
                    lineRenderer.SetPosition(1, target.position);
                    lineRenderer.enabled = true;
                }
            }
        }
    }
}
