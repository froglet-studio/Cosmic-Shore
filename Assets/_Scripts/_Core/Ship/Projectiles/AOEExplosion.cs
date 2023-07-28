using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class AOEExplosion : MonoBehaviour
    {
        [HideInInspector] public float speed;
        protected const float PI_OVER_TWO = Mathf.PI / 2;
        protected Vector3 MaxScaleVector;

        [HideInInspector] public float MaxScale = 200f;
        [SerializeField] protected float ExplosionDuration = 2f;
        [SerializeField] protected float ExplosionDelay = .2f;
        [SerializeField] List<ShipImpactEffects> shipImpactEffects;
        [SerializeField] bool affectSelf = false;
        [SerializeField] bool destructive = true;
        [SerializeField] bool devastating = false;
        
        protected static GameObject container;

        protected Material material;
        Teams team;
        Ship ship;
        [HideInInspector] public Material Material { get { return material; } set { material = new Material(value); } }
        [HideInInspector] public Teams Team { get => team; set => team = value; }
        [HideInInspector] public Ship Ship { get => ship; set => ship = value; }

        protected virtual void Start()
        {
            speed = MaxScale / ExplosionDuration; // TODO: use the easing of the explosion to change this over time
            if (container == null) container = new GameObject("AOEContainer");

            if (team == Teams.Unassigned)
                team = Ship.Team;
            if (material == null) 
                material = new Material(Ship.AOEExplosionMaterial);
            transform.SetParent(container.transform, false); // SetParent with false to take container's world position
            MaxScaleVector = new Vector3(MaxScale, MaxScale, MaxScale);

            StartCoroutine(ExplodeCoroutine());
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            var impactVector = (other.transform.position - transform.position).normalized * speed;
            if (other.TryGetComponent<TrailBlock>(out var trailBlock))
            {
                if (trailBlock.Team == Team && !affectSelf || !destructive)
                {
                    trailBlock.ActivateShield(2f);
                    return;
                }
                if (devastating) trailBlock.Devastate(impactVector, Team, Ship.Player.PlayerName);
                else trailBlock.Explode(impactVector, Team, Ship.Player.PlayerName);
            }
            if (other.TryGetComponent<ShipGeometry>(out var shipGeometry))
            {
                if (shipGeometry.Ship.Team == Team && !affectSelf)
                    return;

                PerformShipImpactEffects(shipGeometry, impactVector);
            }
        }

        protected virtual IEnumerator ExplodeCoroutine()
        {
            yield return new WaitForSeconds(ExplosionDelay);

            if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                meshRenderer.material = material;

            var elapsedTime = 0f;
            while (elapsedTime < ExplosionDuration)
            {
                elapsedTime += Time.deltaTime;
                var easing = Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO);
                transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, easing);
                material.SetFloat("_Opacity", 1-easing);
                //material.SetFloat("_Opacity", Mathf.Clamp((MaxScaleVector - container.transform.localScale).magnitude / MaxScaleVector.magnitude, 0, 1));
                yield return null;
            }

            Destroy(gameObject);
        }

        protected virtual void PerformShipImpactEffects(ShipGeometry shipGeometry, Vector3 impactVector)
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
                        HapticController.PlayShipCollisionHaptics();
                        break;
                    case ShipImpactEffects.SpinAround:
                        shipGeometry.Ship.ShipController.SpinShip(impactVector);
                        break;
                    case ShipImpactEffects.Knockback:
                        shipGeometry.Ship.ShipController.ModifyVelocity(impactVector * 100, 2);
                        //shipGeometry.Ship.transform.localPosition += impactVector / 2f;
                        break;
                    case ShipImpactEffects.Stun:
                        shipGeometry.Ship.ShipController.ModifyThrottle(.1f, 10);
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