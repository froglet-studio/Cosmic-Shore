
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOERadialBlocks : AOEConicExplosion
    {
        ElementalFloat depthScale = new(1f);  // Scale both ray radius and block size, in the z direction.
        [SerializeField] float growthRate = .05f;
        //float depthScale = 1f;

        #region Attributes for Block Creation
        [Header("Block Creation")]
        [SerializeField] protected TrailBlock trailBlock;
        [SerializeField] protected Vector3 baseBlockScale = new Vector3(10f, 5f, 5f);
        [SerializeField] bool shielded = true;
        private Material blockMaterial;
        #endregion

        #region Attributes for Explosion Parameters
        [Header("Explosion Parameters")]
        [SerializeField] float SecondaryExplosionDelay = 0.3f;
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
                : ThemeManager.Instance.GetTeamBlockMaterial(Ship.Team);

            baseBlockScale.z *= depthScale.Value;
            maxRadius *= depthScale.Value;
            BindElementalFloats(Ship);
//          "The name 'BindElementalFloats' does not exist in the current context" --> class type needs to be ElementalShipComponent?
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

            for (int block = 0; block < blocksPerRay; block++)
            {
                float t = (float)block / (blocksPerRay - 1);
                float radius = Random.Range(minRadius, maxRadius);

                // Create a rotation that evenly spreads around rayDirection
                float spread = raySpread;
                float rotationAroundRay = Random.Range(0f, 360f);

                // Create a rotation from rayDirection to the spread vector
                Quaternion spreadRotation = Quaternion.AngleAxis(spread, Vector3.Cross(rayDirection, Vector3.up).normalized);
                Quaternion rotationAround = Quaternion.AngleAxis(rotationAroundRay, rayDirection);

                // Combine the rotations
                Vector3 spreadDirection = rotationAround * spreadRotation * rayDirection;

                Vector3 position = transform.position + spreadDirection * radius;
                float scaleMultiplier = scaleCurve.Evaluate(radius / maxRadius);
                Vector3 blockScale = baseBlockScale * scaleMultiplier;

                CreateBlock(position, spreadDirection, transform.up, $"::Radial::{rayIndex}::{block}", trail, blockScale);
            }
        }

        protected TrailBlock CreateBlock(Vector3 position, Vector3 forward, Vector3 up, string blockId, Trail trail, Vector3 scale)
        {
            var block = Instantiate(trailBlock);
            block.ChangeTeam(Team);
            block.ownerID = Ship.Player.PlayerUUID;
            block.Player = Ship.Player;
            block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
            block.GetComponent<MeshRenderer>().material = blockMaterial;
            block.ownerID = block.ownerID + blockId + position;
            block.TargetScale = scale;
            block.transform.parent = TrailSpawner.TrailContainer.transform;
            block.Trail = trail;
            block.growthRate = growthRate;
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


