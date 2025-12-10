using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class SlowShipViewer : MonoBehaviour
    {
        [Header("Visuals")]
        [SerializeField] private Material trailViewerMaterial;
        [SerializeField] private float fallbackDuration = 3f;

        [Header("Events")]
        [Tooltip("Explosion debuff events to listen to (e.g., ExplosionEffect, Rhino Prism debuff).")]
        [SerializeField] private ScriptableEventExplosionDebuffApplied[] explosionDebuffEvents;

        LineRenderer _lineRenderer;

        struct ActiveLink
        {
            public Transform target;
            public float expireTime;
        }

        readonly List<ActiveLink> _links = new();

        void Awake()
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
            _lineRenderer.material      = trailViewerMaterial;
            _lineRenderer.startWidth    = _lineRenderer.endWidth = 0.1f;
            _lineRenderer.positionCount = 2;
            _lineRenderer.enabled       = false;
        }

        void OnEnable()
        {
            if (explosionDebuffEvents == null) return;

            foreach (var evt in explosionDebuffEvents)
            {
                if (evt == null) continue;
                evt.OnRaised += OnExplosionDebuffApplied;
            }
        }

        void OnDisable()
        {
            if (explosionDebuffEvents == null) return;

            foreach (var evt in explosionDebuffEvents)
            {
                if (evt == null) continue;
                evt.OnRaised -= OnExplosionDebuffApplied;
            }
        }

        void OnExplosionDebuffApplied(ExplosionDebuffPayload payload)
        {
            var victimVessel = payload.Vessel;
            var duration     = payload.Duration;

            if (victimVessel == null)
                return;

            if (victimVessel.Transform == transform)
                return;

            _links.Add(new ActiveLink
            {
                target     = victimVessel.Transform,
                expireTime = Time.time + (duration > 0 ? duration : fallbackDuration)
            });
        }

        void Update()
        {
            // prune expired / null links
            for (int i = _links.Count - 1; i >= 0; i--)
            {
                if (!_links[i].target || Time.time > _links[i].expireTime)
                    _links.RemoveAt(i);
            }

            if (_links.Count == 0)
            {
                _lineRenderer.enabled = false;
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
                    bestTarget  = link.target;
                }
            }

            if (!bestTarget)
            {
                _lineRenderer.enabled = false;
                return;
            }

            _lineRenderer.enabled = true;
            _lineRenderer.SetPosition(0, transform.position);
            _lineRenderer.SetPosition(1, bestTarget.position);
        }
    }
}
