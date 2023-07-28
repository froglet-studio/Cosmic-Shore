using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class Gun : MonoBehaviour
    {
        [SerializeField] GameObject projectilePrefab;

        public bool Detonate = false;
        public float projectileTime = 2;
        public float firePeriod = .2f;
        public Teams Team;
        public Ship Ship;
        bool onCooldown = false;
        Trail trail = new();


        [SerializeField] TrailBlock trailBlock;
        Material blockMaterial;
        ShipStatus shipData;

        private void Start()
        {
            blockMaterial = Ship.GetComponent<TrailSpawner>().GetBlockMaterial();
            shipData = Ship.GetComponent<ShipStatus>();
        }

        public void FireGun(Transform containerTransform, float speed, Vector3 inheritedVelocity, 
            float projectileScale, Vector3 blockScale, bool ignoreCooldown = false, float projectileTime = 3, float charge = 0)
        {
            if (onCooldown && !ignoreCooldown)
                return;

            onCooldown = true;

            var projectile = Instantiate(projectilePrefab).GetComponent<Projectile>();
            projectile.transform.rotation = Quaternion.LookRotation(transform.forward);
            projectile.transform.position = transform.position + projectile.transform.forward * 5;
            projectile.transform.localScale = projectileScale * Vector3.one;
            projectile.transform.parent = containerTransform;
            projectile.Velocity = projectile.transform.forward * speed + inheritedVelocity;
            projectile.Team = Team;
            projectile.Ship = Ship;
            if (projectile is ExplodableProjectile) ((ExplodableProjectile)projectile).Charge = charge;

            StartCoroutine(MoveProjectileCoroutine(projectile, blockScale, projectileTime));
            StartCoroutine(CooldownCoroutine());
        }

        IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(firePeriod);
            onCooldown = false;
        }

        void CreateBlock(Vector3 position, Transform lookAtTarget, string ownerId, Vector3 blockScale)
        {
            Vector3 distance;
            Vector3 lookAtVector;
            
            
            var Block = Instantiate(trailBlock);
            if (trail.TrailList.Count == 0) 
            { 
                lookAtVector = Vector3.zero;
                distance = Vector3.one; 
            }
            else 
            {
                lookAtVector = lookAtTarget.position - ((Block.InnerDimensions.z / 2) * lookAtTarget.forward);
                distance = lookAtVector - position; 
            }

            Block.Team = Team;
            Block.ownerId = Ship.Player.PlayerUUID;
            Block.PlayerName = Ship.Player.PlayerName;
            Block.GetComponent<MeshRenderer>().material = blockMaterial;
            Block.ID = Block.ownerId + ownerId;
            Block.Trail = trail;
            trail.Add(Block);
            Block.transform.parent = TrailSpawner.TrailContainer.transform;

            float span = distance.magnitude;
            float SqrtDistance = Mathf.Sqrt(span);
            Block.InnerDimensions = new Vector3(blockScale.x / SqrtDistance,
                                                blockScale.y / SqrtDistance,
                                                blockScale.z * span);
            //Block.InnerDimensions = new Vector3(.5f, .5f, 1);



            Debug.Log($"block scale: {blockScale} --- position:{position} ");
            //if (trail.TrailList.Count == 1) { Block.transform.position = position; return; }
            Block.transform.position = (position + lookAtVector)/2;//centerOfCurvaturePosition + Quaternion.AngleAxis(i, outOfPlaneVector) * (-distanceFromPreviousBlockToCurveCenter);
            Block.transform.LookAt(lookAtVector, transform.up);
            Debug.Log($"bullet trail count: {trail.TrailList.Count}");
            //}
            
        }


        IEnumerator MoveProjectileCoroutine(Projectile projectile, Vector3 blockScale, float projectileTime)
        {
            var elapsedTime = 0f;
            var velocity = projectile.Velocity;
            
            while (elapsedTime < projectileTime)
            {
                if (projectile == null) yield break;
                if (Detonate)
                {
                    Destroy(projectile.gameObject);
                    yield break;
                }
                elapsedTime += Time.deltaTime;              
                projectile.transform.position += velocity * Time.deltaTime;
                yield return null;
            }

            //Transform lookAtTarget;
            //if (trail.TrailList.Count == 0) lookAtTarget = Ship.transform;
            //else lookAtTarget = trail.TrailList[trail.TrailList.Count - 1].transform;

            //if (shipData.LayingBulletTrail)
            //{
            //    CreateBlock(projectile.transform.position, lookAtTarget, "::projectile::" + Time.time, blockScale);
            //}
            //else if (trail.TrailList.Count < 7 && trail.TrailList.Count != 0)
            //{
            //    foreach (TrailBlock trailBlock in trail.TrailList)
            //    {
            //        Destroy(trailBlock.gameObject);
            //    }
            //    trail = new();
            //}
            //else if (trail.TrailList.Count > 0)
            //{
            //    trail = new();
            //}

            Destroy(projectile.gameObject);
        }
    }
}