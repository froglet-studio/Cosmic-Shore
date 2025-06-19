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
        // TODO: WIP gun firing needs to be reworked
        [SerializeField] Gun gunContainer;
        List<Gun> guns = new();

        [SerializeField] PoolManager projectileContainer;

        [SerializeField] int ammoIndex = 0;
        [SerializeField] float ammoCost = .03f;
 
        [SerializeField] bool inherit = false;

        [SerializeField] float ProjectileScale = 1f;

        public FiringPatterns FiringPattern = FiringPatterns.Default;
        public int Energy = 0;
        public ElementalFloat speed = new(2000);
        public float projectileTime = 3;

        bool firing = false;
        [SerializeField] float firingRate = 1f;

        Coroutine fireGunsCoroutine = null;

        void CopyValues<T>(T from, T to)
        {
            var json = JsonUtility.ToJson(from);
            JsonUtility.FromJsonOverwrite(json, to);
        }


        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);
            gunContainer.Initialize(ship.ShipStatus);

            foreach (var child in gunContainer.GetComponentsInChildren<Transform>())
            {
                if (child == gunContainer.transform)
                    continue;
                var go = child.gameObject;
                CopyValues<Gun>(gunContainer, go.AddComponent<Gun>());
                var childGun = go.GetComponent<Gun>();
                childGun.Initialize(ship.ShipStatus);

                guns.Add(childGun);
                //child.LookAt(gunContainer.transform);
                //child.Rotate(0, 180, 0);
            }

            //projectileContainer = new GameObject($"{ship.Player.PlayerName}_BarrageProjectiles");
        }

        public override void StartAction()
        {
            firing = true;
            fireGunsCoroutine = StartCoroutine(FireGunsCoroutine());
        }

        public override void StopAction()
        {
            firing = false;
            StopAllCoroutines();
        }

        IEnumerator FireGunsCoroutine()
        {
            while (firing) 
            {
                if (ResourceSystem.Resources[ammoIndex].CurrentAmount >= ammoCost)
                {
                    Vector3 inheritedVelocity;
                    // TODO: WIP magic numbers
                    foreach (var gun in guns)
                    {
                        if (inherit)
                        {
                            if (ShipStatus.Attached) inheritedVelocity = gun.transform.forward;
                            else inheritedVelocity = ShipStatus.Course;
                        }
                        else inheritedVelocity = Vector3.zero;
                        gun.FireGun(projectileContainer.transform, speed.Value, inheritedVelocity * ShipStatus.Speed, ProjectileScale, true, projectileTime, 0, FiringPattern, Energy);
                    }
                    ResourceSystem.ChangeResourceAmount(ammoIndex, -ammoCost);
                }
                yield return new WaitForSeconds(1/firingRate);
            }
        }
    }
}