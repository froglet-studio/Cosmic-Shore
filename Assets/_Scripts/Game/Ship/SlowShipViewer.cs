using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class SlowShipViewer : MonoBehaviour
    {
        [SerializeField] private Material trailViewerMaterial;
        [SerializeField] private float fallbackDuration = 3f;

        private LineRenderer lineRenderer;

        private struct ActiveLink
        {
            public Transform target;
            public float expireTime;
        }

        private readonly List<ActiveLink> _links = new();

        void Awake()
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material   = trailViewerMaterial;
            lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
            lineRenderer.positionCount = 2;
            lineRenderer.enabled   = false;
        }

        void OnEnable()
        {
            VesselChangeSpeedByExplosionEffectSO.OnExplosionDebuffApplied += OnExplosionDebuffApplied;
        }

        void OnDisable()
        {
            VesselChangeSpeedByExplosionEffectSO.OnExplosionDebuffApplied -= OnExplosionDebuffApplied;
        }

        private void OnExplosionDebuffApplied(IVessel victimVessel, float duration)
        {
            if (victimVessel == null)
                return;

            if (victimVessel.Transform == transform)
                return;

            Debug.Log($"Explosion Debuffed");
            
            _links.Add(new ActiveLink
            {
                target     = victimVessel.Transform,
                expireTime = Time.time + (duration > 0 ? duration : fallbackDuration)
            });
        }

        void Update()
        {
            for (int i = _links.Count - 1; i >= 0; i--)
            {
                if (!_links[i].target || Time.time > _links[i].expireTime)
                    _links.RemoveAt(i);
            }

            if (_links.Count == 0)
            {
                lineRenderer.enabled = false;
                return;
            }

            Transform bestTarget = null;
            float bestDistSqr = float.PositiveInfinity;

            foreach (var link in _links)
            {
                if (!link.target) continue;
                float d2 = (link.target.position - transform.position).sqrMagnitude;
                if (d2 < bestDistSqr)
                {
                    bestDistSqr = d2;
                    bestTarget = link.target;
                }
            }

            if (!bestTarget)
            {
                lineRenderer.enabled = false;
                return;
            }

            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, bestTarget.position);
        }

    }
}