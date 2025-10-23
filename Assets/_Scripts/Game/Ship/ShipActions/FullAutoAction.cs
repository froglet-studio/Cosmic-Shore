using System;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Game.Projectiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace CosmicShore
{
    public class FullAutoAction : ShipAction
    {
        public event Action OnFullAutoStarted;
        public event Action OnFullAutoStopped;
        
        // TODO: WIP gun firing needs to be reworked
        [SerializeField] Gun gunContainer;

        [SerializeField]
        Transform[] gunTransforms;

        // [SerializeField] PoolManager projectileContainer;

        [SerializeField] int ammoIndex = 0;
        [SerializeField] float ammoCost = .03f;
 
        [SerializeField] bool inherit = false;

        [SerializeField] float ProjectileScale = 1f;

        public FiringPatterns FiringPattern = FiringPatterns.Default;
        public int Energy = 0;
        public ElementalFloat speed = new(2000);
        public float projectileTime = 3;

        bool firing = false;
        public float firingRate = 1f;

        Coroutine fireGunsCoroutine = null;

        void CopyValues<T>(T from, T to)
        {
            var json = JsonUtility.ToJson(from);
            JsonUtility.FromJsonOverwrite(json, to);
        }


        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            if (gunTransforms == null || gunTransforms.Length == 0)
            {
                gunTransforms = new Transform[1];
                gunTransforms[0] = gunContainer.transform;
            }
            gunContainer.Initialize(vessel.VesselStatus);
        }

        public override void StartAction()
        {
            firing = true;
            OnFullAutoStarted?.Invoke();   
            fireGunsCoroutine = StartCoroutine(FireGunsCoroutine());
        }

        public override void StopAction()
        {
            firing = false;
            StopAllCoroutines();
            OnFullAutoStopped?.Invoke();   
        }

        IEnumerator FireGunsCoroutine()
        {
            while (firing) 
            {
                if (ResourceSystem.Resources[ammoIndex].CurrentAmount >= ammoCost)
                {
                    Vector3 inheritedVelocity;
                    foreach (var transform in gunTransforms)
                    {
                        if (inherit)
                        {
                            if (VesselStatus.Attached) inheritedVelocity = transform.transform.forward;
                            else inheritedVelocity = VesselStatus.Course;
                        }
                        else inheritedVelocity = Vector3.zero;
                        gunContainer.FireGun(transform, speed.Value, inheritedVelocity * VesselStatus.Speed, ProjectileScale, true, projectileTime, 0, FiringPattern, Energy);
                    }
                    ResourceSystem.ChangeResourceAmount(ammoIndex, -ammoCost);
                }
                yield return new WaitForSeconds(1/firingRate);
            }
        }
    }
}