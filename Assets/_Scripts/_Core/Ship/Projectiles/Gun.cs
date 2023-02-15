using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class Gun : MonoBehaviour
    {
        [SerializeField] GameObject projectilePrefab;

        public float speed = 10;
        public float projectileTime = 2;
        public float firePeriod = .2f;
        public Teams Team;
        public Ship Ship;
        bool onCooldown = false;

        [SerializeField] TrailBlock trail;
        Material blockMaterial;
        [SerializeField] Vector3 blockScale = new Vector3(1.5f, 1.5f, 3f);

        private void Start()
        {
            blockMaterial = Ship.GetComponent<TrailSpawner>().GetBlockMaterial();
        }

        public void FireGun(Transform containerTransform, Vector3 inheritedVelocity, float projectileScale, Vector3 blockScale)
        {
            if (onCooldown)
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

            StartCoroutine(MoveProjectileCoroutine(projectile, blockScale));
            StartCoroutine(CooldownCoroutine());
        }

        IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(firePeriod);
            onCooldown = false;
        }

        void CreateBlock(Vector3 position, Quaternion rotation, string ownerId, Vector3 blockScale)
        {
            var Block = Instantiate(trail);
            Block.Team = Team;
            Block.ownerId = Ship.Player.PlayerUUID;
            Block.PlayerName = Ship.Player.PlayerName;
            Block.transform.SetPositionAndRotation(position, rotation);
            Block.GetComponent<MeshRenderer>().material = blockMaterial;
            Block.ID = Block.ownerId + ownerId;
            Block.InnerDimensions = blockScale;
            Block.transform.parent = TrailSpawner.TrailContainer.transform;
        }

        IEnumerator MoveProjectileCoroutine(Projectile projectile, Vector3 blockScale)
        {
            var elapsedTime = 0f;
            var velocity = projectile.Velocity;
            while (elapsedTime < projectileTime)
            {
                elapsedTime += Time.deltaTime;
                projectile.transform.position += velocity * Time.deltaTime;
                yield return null;
            }
            CreateBlock(projectile.transform.position, projectile.transform.rotation, "::projectile::" + Time.time, blockScale);
            Destroy(projectile.gameObject);
        }
    }
}