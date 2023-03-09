using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class Gun : MonoBehaviour
    {
        [SerializeField] GameObject projectilePrefab;

        public float projectileTime = 2;
        public float firePeriod = .2f;
        public Teams Team;
        public Ship Ship;
        bool onCooldown = false;
        Trail trail = new();


        [SerializeField] TrailBlock trailBlock;
        Material blockMaterial;

        private void Start()
        {
            blockMaterial = Ship.GetComponent<TrailSpawner>().GetBlockMaterial();
        }

        public void FireGun(Transform containerTransform, float speed, Vector3 inheritedVelocity, 
            float projectileScale, Vector3 blockScale, bool ignoreCooldown = false, float projectileTime = 2)
        {
            if (onCooldown && !ignoreCooldown)
                return;

            onCooldown = true;

            var projectile = Instantiate(projectilePrefab).GetComponent<Projectile>();
            projectile.transform.rotation = Quaternion.LookRotation(transform.up);
            projectile.transform.position = transform.position + projectile.transform.forward * 2;
            projectile.transform.localScale = projectileScale * Vector3.one;
            projectile.transform.parent = containerTransform;
            projectile.Velocity = projectile.transform.forward * speed + inheritedVelocity;
            projectile.Team = Team;
            projectile.Ship = Ship;

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
            Vector3 distance = lookAtTarget.position - position;
            //Vector3 reflectedVector = Quaternion.AngleAxis(180, distance) * lookAtTarget.transform.forward;
            float angle1 = Vector3.Angle(lookAtTarget.transform.forward, distance);
            Vector3 outOfPlaneVector = Vector3.Cross(lookAtTarget.transform.forward, distance);
            float curveRadius = distance.magnitude / (2 * Mathf.Sin(angle1));
            Vector3 distanceFromPreviousBlockToCurveCenter = (Quaternion.AngleAxis(-90, outOfPlaneVector) * lookAtTarget.transform.forward * curveRadius);
            Vector3 centerOfCurvaturePosition = lookAtTarget.position + distanceFromPreviousBlockToCurveCenter;

            //Vector3 blockPosition = centerOfCurvaturePosition + Quaternion.AngleAxis(3, outOfPlaneVector) * (-distanceFromPreviousBlockToCurveCenter);

            for (int i = 1; i < 2*angle1; i++) 
            {
                var Block = Instantiate(trailBlock);
                Block.Team = Team;
                Block.ownerId = Ship.Player.PlayerUUID;
                Block.PlayerName = Ship.Player.PlayerName;
                Block.GetComponent<MeshRenderer>().material = blockMaterial;
                Block.ID = Block.ownerId + ownerId;
                Block.Trail = trail;
                trail.Add(Block);
                Block.transform.parent = TrailSpawner.TrailContainer.transform;

                //float span = (lookAtTarget.position - position).magnitude;
                //float SqrtDistance = Mathf.Sqrt(span);
                //Block.InnerDimensions = new Vector3(blockScale.x / SqrtDistance,
                //                                    blockScale.y / SqrtDistance,
                //                                    blockScale.z * span);
                Block.InnerDimensions = new Vector3(.5f, .5f, 1);



                Debug.Log($"block scale: {blockScale} --- position:{position} ");
                if (trail.TrailList.Count == 1) { Block.transform.position = position; return; }
                else Block.transform.position = centerOfCurvaturePosition + Quaternion.AngleAxis(i, outOfPlaneVector) * (-distanceFromPreviousBlockToCurveCenter);//(position + lookAtTarget.position) / 2f;
                Block.transform.LookAt(trail.TrailList[trail.TrailList.Count - 1].transform, transform.up);
            }
            
        }

        IEnumerator MoveProjectileCoroutine(Projectile projectile, Vector3 blockScale, float projectileTime)
        {
            var elapsedTime = 0f;
            var velocity = projectile.Velocity;
            Transform lookAtTarget;
            
            while (elapsedTime < projectileTime)
            {
                elapsedTime += Time.deltaTime;
                projectile.transform.position += velocity * Time.deltaTime;
                yield return null;
            }

            if (trail.TrailList.Count == 0) lookAtTarget = Ship.transform;
            else lookAtTarget = trail.TrailList[trail.TrailList.Count - 1].transform;

            CreateBlock(projectile.transform.position, lookAtTarget, "::projectile::" + Time.time, blockScale);
            Destroy(projectile.gameObject);
        }
    }
}