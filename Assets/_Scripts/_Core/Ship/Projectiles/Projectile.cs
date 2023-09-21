using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;


namespace StarWriter.Core
{
    public class Projectile : MonoBehaviour
    {
        public Vector3 Velocity;
        public Teams Team;
        public Ship Ship;

        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] List<ShipImpactEffects> shipImpactEffects;

        [SerializeField] bool drawLine = false;
        [SerializeField] float startLength = 1f;
        [SerializeField] float growthRate = 1.0f;
        [SerializeField] Material spikeMaterial;
        LineRenderer lineRenderer;

        private void Start()
        {
            if (drawLine) 
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(spikeMaterial);
                lineRenderer.startColor = lineRenderer.endColor = Color.green;
                lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, new Vector3(0, 0, 0));
                lineRenderer.SetPosition(1, new Vector3(0, 0, startLength));
                lineRenderer.useWorldSpace = false;
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if (trailBlock.Team == Team)
                    return;

                PerformTrailImpactEffects(trailBlock.TrailBlockProperties);
            }
            if (other.TryGetComponent<ShipGeometry>(out var shipGeometry))
            {
                Debug.Log($"projectile hit ship {shipGeometry}");
                if (shipGeometry.Ship.Team == Team)
                    return;

                PerformShipImpactEffects(shipGeometry);
            }
        }

        protected virtual void PerformTrailImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Explode(Velocity, Ship.Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        trailBlockProperties.trailBlock.Steal(Ship.Player.PlayerName, Team);
                        break;
                    case TrailBlockImpactEffects.Shield:
                        trailBlockProperties.trailBlock.ActivateShield(.5f);
                        break;
                    case TrailBlockImpactEffects.Stop:
                        StopCoroutine(moveCoroutine);
                        break;
                }
            }
        }

        protected virtual void PerformShipImpactEffects(ShipGeometry shipGeometry)
        {
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipGeometry.Ship.TrailSpawner.PauseTrailSpawner();
                        shipGeometry.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        if (!shipGeometry.Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.SpinAround:
                        shipGeometry.Ship.transform.localRotation = Quaternion.LookRotation(Velocity);
                        break;
                    case ShipImpactEffects.Knockback:
                        //shipGeometry.Ship.transform.localPosition += Velocity/2f;
                        shipGeometry.Ship.ShipController.ModifyVelocity(Velocity * 100,2);
                        break;
                    case ShipImpactEffects.Stun:
                        shipGeometry.Ship.ShipController.ModifyThrottle(.1f, 10);
                        break;
                }
            }
        }

        public void LaunchProjectile(float projectileTime)
        {
            moveCoroutine = StartCoroutine(MoveProjectileCoroutine(projectileTime));
        }

        Coroutine moveCoroutine;

        public IEnumerator MoveProjectileCoroutine(float projectileTime)
        {
            var elapsedTime = 0f;
    
            if (drawLine) yield return new WaitUntil(() => lineRenderer != null);
            while (elapsedTime < projectileTime)
            {
                if (drawLine) lineRenderer.SetPosition(1, new Vector3(0,0, elapsedTime * growthRate));
                elapsedTime += Time.deltaTime;
                transform.position += Velocity * Time.deltaTime * Mathf.Cos(elapsedTime*Mathf.PI/(2*projectileTime));
                yield return null;
            }

            Destroy(gameObject);
        }

        public void Detonate()
        {
            StopCoroutine(moveCoroutine);
            Destroy(gameObject);
        }

    }
}