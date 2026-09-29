using System;
using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class FireGunAction : ShipAction
    {
        public event Action OnGunFired;

        /// <summary>Static event: each time a gun fires a single shot. Param = player name.</summary>
        public static event Action<string> OnShotFired;
        // TODO: WIP gun firing needs to be reworked
        [SerializeField] Gun gun;

        [SerializeField] GameObject projectileContainer;
        [SerializeField] float ammoCost = .03f;

        public float ProjectileScale = 1f;
        public int Energy = 0;
        public float Speed = 90;
        public ElementalFloat ProjectileTime = new ElementalFloat(3f);

        [SerializeField] int ammoIndex = 0;

        // An ability's temporary raise of this gun's output, held as a FLOOR rather than written
        // into the authored fields. EnergizeAction used to do the latter -- including
        // `ProjectileTime.Value = x`, which is the element's own parameter -- and it cost three
        // things: a level change mid-energize made ScaleValueWithLevel overwrite the raise and
        // silently drop it; the "default" it restored was captured from fireActions[0] and written
        // to EVERY gun, so two guns with different authored values collapsed onto the first one's;
        // and that captured default was the pre-scaling authored Value (5 on the Urchin), so a
        // restore replaced the element-scaled lifetime with a constant until the next level event.
        // A floor cannot do any of the three: the authored parameter is never written, so it is
        // always the element that decides the base and the ability only ever lifts it.
        float speedFloor;
        float projectileTimeFloor;
        int energyFloor;

        /// <summary>This shot's lifetime: the element's live value, or an ability's floor if higher.</summary>
        public float ResolvedProjectileTime => Mathf.Max(
            IsInitialized ? ProjectileTime.EvaluateLive(VesselStatus) : ProjectileTime.Value,
            projectileTimeFloor);

        public float ResolvedSpeed  => Mathf.Max(Speed, speedFloor);
        public int   ResolvedEnergy => Mathf.Max(Energy, energyFloor);

        /// <summary>Lift this gun's output for as long as an ability holds it. Never lowers.</summary>
        public void RaiseOutputFloors(float speed, float projectileTime, int energy)
        {
            speedFloor          = Mathf.Max(speedFloor, speed);
            projectileTimeFloor = Mathf.Max(projectileTimeFloor, projectileTime);
            energyFloor         = Mathf.Max(energyFloor, energy);
        }

        /// <summary>Drop every floor. The gun returns to its OWN authored values, not a neighbour's.</summary>
        public void ClearOutputFloors()
        {
            speedFloor = projectileTimeFloor = 0f;
            energyFloor = 0;
        }

    
        public float Ammo01
        {
            get
            {
                var r = ResourceSystem.Resources[ammoIndex];
                if (r == null || r.MaxAmount <= 0f) return 0f;
                return Mathf.Clamp01(r.CurrentAmount / r.MaxAmount);
            }
        }

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            gun.Initialize(vessel.VesselStatus);
            projectileContainer.transform.parent = Vessel.VesselStatus.Player.Transform;
        }
        public override void StartAction()
        {
            if (ResourceSystem.Resources[ammoIndex].CurrentAmount >= ammoCost) 
            {
                ResourceSystem.ChangeResourceAmount(ammoIndex, - ammoCost);

                Vector3 inheritedVelocity;
                if (VesselStatus.IsAttached) inheritedVelocity = gun.transform.forward;
                else inheritedVelocity = VesselStatus.Course;
                OnGunFired?.Invoke();
                OnShotFired?.Invoke(VesselStatus.PlayerName);
                gun.FireGun(projectileContainer.transform, ResolvedSpeed, inheritedVelocity * VesselStatus.Speed, ProjectileScale, true, ResolvedProjectileTime, 0, FiringPatterns.Default, ResolvedEnergy);
            }
        }

        public override void StopAction()
        {

        }
    }
}
