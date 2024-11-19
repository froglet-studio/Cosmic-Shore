using System.Collections;
using System.Collections.Generic;
using CosmicShore.Game.IO;
using CosmicShore.Environment.FlowField;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class Projectile : MonoBehaviour
    {
        public Vector3 Velocity;
        public Teams Team;
        public Ship Ship;
        public bool ImpactOnEnd;
        public float Inertia = 1;
        
        [HideInInspector] public Vector3 InitialScale;
        [SerializeField] List<TrailBlockImpactEffects> trailBlockImpactEffects;
        [SerializeField] List<ShipImpactEffects> shipImpactEffects;
        [SerializeField] List<CrystalImpactEffects> crystalImpactEffects;
        [SerializeField] List<TrailBlockImpactEffects> endEffects;

        public float ProjectileTime;

        [SerializeField] bool spike = false;
        [SerializeField] float growthRate = 1.0f;
        [SerializeField] bool friendlyFire = false;

        MeshRenderer meshRenderer;

        private void Awake()
        {
            InitialScale = transform.localScale;
        }

        void Start()
        {
            if (spike) 
            {
                meshRenderer = gameObject.GetComponent<MeshRenderer>();
                meshRenderer.material = ThemeManager.Instance.GetTeamSpikeMaterial(Team);
                meshRenderer.material.SetFloat("_Opacity", .5f);
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
           HandleCollision(other);
        }

        void HandleCollision(Collider other)
        {
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if (!friendlyFire && trailBlock.Team == Team)
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
            if (other.TryGetComponent<Crystal>(out var crystal))
            {
                Debug.Log($"projectile hit crystal {crystal.Team}");
                if (crystal.Team == Team)
                    return;
                PerformCrystalImpactEffects(crystal.crystalProperties);
            }
        }

        protected virtual void PerformTrailImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (TrailBlockImpactEffects effect in trailBlockImpactEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.DeactivateTrailBlock:
                        trailBlockProperties.trailBlock.Damage(Velocity * Inertia, Ship.Team, Ship.Player.PlayerName);
                        break;
                    case TrailBlockImpactEffects.Steal:
                        trailBlockProperties.trailBlock.Steal(Ship.Player, Team);
                        break;
                    case TrailBlockImpactEffects.Shield:
                        trailBlockProperties.trailBlock.ActivateShield(.5f);
                        break;
                    case TrailBlockImpactEffects.Stop:

                        if (!trailBlockProperties.trailBlock.GetComponent<Boid>()) Stop();
                        else GetComponentInParent<PoolManager>().ReturnToPool(gameObject, gameObject.tag);
                        break;
                    case TrailBlockImpactEffects.Fire:
                        GetComponent<LoadedGun>().FireGun();
                        break;
                    case TrailBlockImpactEffects.Explode:
                        Debug.Log("TrailExplode");
                        ((ExplodableProjectile)this).Detonate();
                        break;

                }
            }
        }

        protected virtual void PerformEndEffects()
        {
            foreach (TrailBlockImpactEffects effect in endEffects)
            {
                switch (effect)
                {
                    case TrailBlockImpactEffects.Stop:
                        Stop();
                        GetComponentInParent<PoolManager>().ReturnToPool(gameObject, gameObject.tag);
                        break;
                    case TrailBlockImpactEffects.Fire:
                        GetComponent<LoadedGun>().FireGun();
                        break;
                    case TrailBlockImpactEffects.Explode:
                        Debug.Log("EndExplode");
                        ((ExplodableProjectile)this).Detonate();
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
                        if (!shipGeometry.Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.ShipCollision);//.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.SpinAround:
                        shipGeometry.Ship.transform.localRotation = Quaternion.LookRotation(Velocity);
                        break;
                    case ShipImpactEffects.Knockback:
                        //shipGeometry.Ship.transform.localPosition += Velocity/2f;
                        shipGeometry.Ship.ShipTransformer.ModifyVelocity(Velocity * 100,2);
                        break;
                    case ShipImpactEffects.Stun:
                        shipGeometry.Ship.ShipTransformer.ModifyThrottle(.1f, 10);
                        break;
                    case ShipImpactEffects.Charm:
                        shipGeometry.Ship.TrailSpawner.Charm(Ship, 7);
                        break;
                }
            }
        }

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            if (StatsManager.Instance != null)
                StatsManager.Instance.CrystalCollected(this.Ship, crystalProperties);

            foreach (CrystalImpactEffects effect in crystalImpactEffects)
            {
                switch (effect)
                {
                    case CrystalImpactEffects.PlayHaptics:
                        if (!Ship.ShipStatus.AutoPilotEnabled) HapticController.PlayHaptic(HapticType.CrystalCollision);//.PlayCrystalImpactHaptics();
                        break;
                    //case CrystalImpactEffects.AreaOfEffectExplosion:
                    //    var AOEExplosion = Instantiate(AOEPrefab).GetComponent<AOEExplosion>();
                    //    AOEExplosion.Ship = this;
                    //    AOEExplosion.SetPositionAndRotation(transform.position, transform.rotation);
                    //    AOEExplosion.MaxScale = Mathf.Max(minExplosionScale, ResourceSystem.CurrentAmmo * maxExplosionScale);
                    //    break;
                    case CrystalImpactEffects.StealCrystal:
                        crystalProperties.crystal.Steal(Team, 7f);
                        break;
                    case CrystalImpactEffects.GainFullAmmo:
                        Ship.ResourceSystem.ChangeResourceAmount(0, Ship.ResourceSystem.Resources[0].MaxAmount); // Move to single system
                        break;
                }
            }
        }

        public void LaunchProjectile(float projectileTime)
        {
            if (spike)
            {
                transform.localScale = new Vector3(.4f, .4f, 2);
                GetComponent<MeshRenderer>().material.SetFloat("_Opacity", .5f);
            }
            moveCoroutine = StartCoroutine(MoveProjectileCoroutine(projectileTime));
        }

        Coroutine moveCoroutine;

        public IEnumerator MoveProjectileCoroutine(float projectileTime)
        {
            var elapsedTime = 0f;
            while (elapsedTime < projectileTime)
            {
                // Calculate movement for this frame
                Vector3 moveDistance = Velocity * Time.deltaTime * Mathf.Cos(elapsedTime * Mathf.PI / (2 * projectileTime));
                Vector3 nextPosition = transform.position + moveDistance;

                // Only check for raycasting collisions if spike is true
                if (spike)
                {
                    if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, moveDistance.magnitude))
                    {
                        transform.position = hit.point;
                        HandleCollision(hit.collider);
                    }

                    var stretchedPoint = new Vector3(.5f, .5f, elapsedTime * growthRate);
                    var percentRemaining = elapsedTime / projectileTime;
                    if (percentRemaining > .9) meshRenderer.material.SetFloat("_Opacity", 1 - Mathf.Pow(percentRemaining, 4));
                    //transform.localScale = stretchedPoint;
                }

                // Move the projectile to the next position
                transform.position = nextPosition;

                elapsedTime += Time.deltaTime;
                yield return null;
            }
            if (ImpactOnEnd) PerformEndEffects();
            GetComponentInParent<PoolManager>().ReturnToPool(gameObject, gameObject.tag);
        }

        public void Stop() 
        {
            if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        }

    }
}