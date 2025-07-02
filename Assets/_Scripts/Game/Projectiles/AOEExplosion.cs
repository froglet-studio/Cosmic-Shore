﻿using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEExplosion : ElementalShipComponent
    {
        [HideInInspector] public float speed;

        protected const float PI_OVER_TWO = Mathf.PI / 2;
        protected Vector3 MaxScaleVector;
        protected float Inertia = 70;

        [HideInInspector] public float MaxScale = 200f;

        [Header("Explosion Settings")]
        [SerializeField] protected float ExplosionDuration = 2f;
        [SerializeField] protected float ExplosionDelay = 0.2f;

        [Header("Impact Effects")]
        [SerializeField] private List<ShipImpactEffects> shipImpactEffects;
        [SerializeField] private bool affectSelf = false;
        [SerializeField] private bool destructive = true;
        [SerializeField] private bool devastating = false;
        [SerializeField] bool shielding = false;

        protected GameObject container;

        // Material and Team
        [HideInInspector] public Material Material { get; set; }
        [HideInInspector] public Teams Team;
        public IShip Ship { get; private set; }
        [HideInInspector] public bool AnonymousExplosion;

        public virtual void Detonate(IShip ship)
        {
            Ship = ship;
            InitializeProperties();
            StartCoroutine(ExplodeCoroutine());
        }

        private void InitializeProperties()
        {
            speed = MaxScale / ExplosionDuration;
            if (container == null) container = new GameObject("AOEContainer");

            if (Team == Teams.Unassigned)
                Team = Ship.ShipStatus.Team;
            if (Material == null)
                Material = new Material(Ship.ShipStatus.AOEExplosionMaterial);

            // SetParent with false to take container's world position
            //transform.SetParent(container.transform, worldPositionStays: false);
            MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            var impactVector = (other.transform.position - transform.position).normalized * speed * Inertia ;

            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if ((trailBlock.Team != Team || affectSelf) && trailBlock.TrailBlockProperties.IsSuperShielded)
                {
                    trailBlock.DeactivateShields();
                    Destroy(gameObject);    // TODO: This seems wrong...
                } 
                if ((trailBlock.Team == Team && !affectSelf) || !destructive)
                {
                    if (shielding && trailBlock.Team == Team)
                        trailBlock.ActivateShield();
                    else 
                        trailBlock.ActivateShield(2f);
                    return;
                }

                if (AnonymousExplosion)
                    trailBlock.Damage(impactVector, Teams.None, "🔥GuyFawkes🔥", devastating);
                else
                    trailBlock.Damage(impactVector, Ship.ShipStatus.Team, Ship.ShipStatus.Player.PlayerName, devastating);
            }
            else if (other.TryGetComponent<IShipStatus>(out var shipStatus))
            {
                if (shipStatus.Team == Team && !affectSelf)
                    return;

                PerformShipImpactEffects(shipStatus, impactVector);
            }
            else
            {
                Debug.Log("AOEExplosion.OnTriggerEnter - not a ship or a trail block: " + other.name);
            }
        }

        protected virtual IEnumerator ExplodeCoroutine()
        {
            yield return new WaitForSeconds(ExplosionDelay);

            if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                meshRenderer.material = Material;

            var elapsedTime = 0f;
            while (elapsedTime < ExplosionDuration)
            {
                elapsedTime += Time.deltaTime;
                var easing = Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO);
                transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, easing);
                Material.SetFloat("_Opacity", 1 - easing);
                yield return null;
            }

            Destroy(gameObject);
        }

        protected virtual void PerformShipImpactEffects(IShipStatus shipStatus, Vector3 impactVector)
        {
            foreach (ShipImpactEffects effect in shipImpactEffects)
            {
                switch (effect)
                {
                    case ShipImpactEffects.TrailSpawnerCooldown:
                        shipStatus.TrailSpawner.PauseTrailSpawner();
                        shipStatus.TrailSpawner.RestartTrailSpawnerAfterDelay(10);
                        break;
                    case ShipImpactEffects.PlayHaptics:
                        if (!shipStatus.Ship.ShipStatus.AutoPilotEnabled)
                            HapticController.PlayHaptic(HapticType.ShipCollision);
                        break;
                    case ShipImpactEffects.SpinAround:
                        shipStatus.Ship.ShipStatus.ShipTransformer.SpinShip(impactVector);
                        break;
                    case ShipImpactEffects.Knockback:
                        if (shipStatus.Ship.ShipStatus.Team == Team)
                        {
                            shipStatus.Ship.ShipStatus.ShipTransformer.ModifyVelocity(impactVector * 100, 2);
                            shipStatus.Ship.ShipStatus.ShipTransformer.ModifyThrottle(10, 6); // TODO: the magic number here needs tuning after switch to additive
                        }
                        else shipStatus.Ship.ShipStatus.ShipTransformer.ModifyVelocity(impactVector * 100, 3);
                        //shipGeometry.Ship.transform.localPosition += impactVector / 2f;
                        break;
                    case ShipImpactEffects.Stun:
                        shipStatus.Ship.ShipStatus.ShipTransformer.ModifyThrottle(.6f, 5);
                        break;
                }
            }
        }

        public virtual void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
