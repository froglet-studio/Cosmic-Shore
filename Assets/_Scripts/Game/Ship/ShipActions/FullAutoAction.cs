using CosmicShore.Core;
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
        ShipStatus shipStatus;
        [SerializeField] PoolManager projectileContainer;
        [SerializeField] float ammoCost = .03f;
        [SerializeField] bool inherit = false;

        [SerializeField] float ProjectileScale = 1f;

        public FiringPatterns FiringPattern = FiringPatterns.Default;
        public int Energy = 0;
        public float speed = 7;
        public float projectileTime = 3;

        bool firing = false;
        [SerializeField] float firingRate = 1f;

        void CopyValues<T>(T from, T to)
        {
            var json = JsonUtility.ToJson(from);
            JsonUtility.FromJsonOverwrite(json, to);
        }

        Coroutine fireGunsCoroutine = null;
        protected override void Start()
        {
            base.Start();
            var gunTemplate = gunContainer.GetComponent<Gun>();
            foreach (var child in gunContainer.GetComponentsInChildren<Transform>())
            {
                if (child == gunContainer.transform)
                    continue;
                var go = child.gameObject;
                CopyValues<Gun>(gunTemplate, go.AddComponent<Gun>());
                guns.Add(go.GetComponent<Gun>());
                //child.LookAt(gunContainer.transform);
                //child.Rotate(0, 180, 0);
            }
            //projectileContainer = new GameObject($"{ship.Player.PlayerName}_BarrageProjectiles");
            shipStatus = ship.GetComponent<ShipStatus>();
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
                if (resourceSystem.CurrentAmmo >= ammoCost)
                {
                    Vector3 inheritedVelocity;
                    // TODO: WIP magic numbers
                    foreach (var gun in guns)
                    {
                        if (inherit)
                        {
                            if (shipStatus.Attached) inheritedVelocity = gun.transform.forward;
                            else inheritedVelocity = shipStatus.Course;
                        }
                        else inheritedVelocity = Vector3.zero;
                        gun.FireGun(projectileContainer.transform, speed, inheritedVelocity * shipStatus.Speed, ProjectileScale, true, projectileTime, 0, FiringPattern, Energy);
                    }
                    resourceSystem.ChangeAmmoAmount(-ammoCost);
                }
                yield return new WaitForSeconds(1/firingRate); 
            }
        }
    }
}
