
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOERadialBlocks : AOEConicExplosion
    {
        #region Attributes for Block Creation
        [Header("Block Creation")]
        [SerializeField] protected TrailBlock trailBlock;
        [SerializeField] protected Vector3 baseBlockScale = new Vector3(10f, 5f, 5f);
        [SerializeField] bool shielded = true;
        private Material blockMaterial;
        #endregion

        #region Attributes for Explosion Parameters
        [Header("Explosion Parameters")]
        [SerializeField] float SecondaryExplosionDelay = 0.2f;
        [SerializeField] int numberOfRays = 16;
        [SerializeField] int blocksPerRay = 5;
        [SerializeField] float maxRadius = 50f;
        [SerializeField] float minRadius = 10f;
        [SerializeField] float raySpread = 15f; // Spread angle in degrees
        [SerializeField] AnimationCurve scaleCurve = AnimationCurve.Linear(0, 1, 1, 0.5f);
        #endregion

        Vector3 rayDirection;

        protected List<Trail> trails = new List<Trail>();

        protected override void Start()
        {
            base.Start();
            blockMaterial = shielded ? ThemeManager.Instance.GetTeamShieldedBlockMaterial(Ship.Team)
                : Ship.TrailSpawner.GetBlockMaterial();
        }

        protected override IEnumerator ExplodeCoroutine()
        {
            StartCoroutine(base.ExplodeCoroutine());
            yield return new WaitForSeconds(ExplosionDelay + SecondaryExplosionDelay);

            for (int ray = 0; ray < numberOfRays; ray++)
            {
                trails.Add(new Trail());
                CreateRay(ray, trails[ray]);
            }
        }

        private void CreateRay(int rayIndex, Trail trail)
        {
            float angleStep = 360f / numberOfRays;
            float rayAngle = rayIndex * angleStep;

            //Quaternion.Euler(0, rayAngle, 0) * 
            //rayDirection = transform.forward;

            for (int block = 0; block < blocksPerRay; block++)
            {
                float t = (float)block / (blocksPerRay - 1);
                float radius = Random.Range(minRadius, maxRadius);

                // Add some randomness to the ray direction
                Vector3 spreadDirection = Quaternion.Euler(
                    Random.Range(-raySpread, raySpread),
                    Random.Range(-raySpread, raySpread),
                    Random.Range(-raySpread, raySpread)
                ) * rayDirection;

                Vector3 position = transform.position + spreadDirection * radius;

                // Scale blocks based on their position in the ray
                float scaleMultiplier = scaleCurve.Evaluate(radius/maxRadius);
                Vector3 blockScale = baseBlockScale * scaleMultiplier;

                CreateBlock(position, spreadDirection, transform.up, $"::Radial::{rayIndex}::{block}", trail, blockScale);
            }
        }

        protected TrailBlock CreateBlock(Vector3 position, Vector3 forward, Vector3 up, string blockId, Trail trail, Vector3 scale)
        {
            var block = Instantiate(trailBlock);
            block.Team = Team;
            block.ownerId = Ship.Player.PlayerUUID;
            block.Player = Ship.Player;
            block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
            block.GetComponent<MeshRenderer>().material = blockMaterial;
            block.ID = block.ownerId + blockId + position;
            block.TargetScale = scale;
            block.transform.parent = TrailSpawner.TrailContainer.transform;
            block.Trail = trail;
            if (shielded) block.TrailBlockProperties.IsShielded = true;
            trail.Add(block);
            return block;
        }


        public override void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            base.SetPositionAndRotation(position, rotation);
            rayDirection = rotation * Vector3.forward;
        }
    }
}


