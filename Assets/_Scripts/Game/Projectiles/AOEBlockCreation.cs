using System.Collections;
using System.Collections.Generic;
using CosmicShore.Core;

using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEBlockCreation : AOEExplosion
    {
        #region Attributes for Block Creation
        [Header("Block Creation")]
        [SerializeField] protected TrailBlock trailBlock;
        [SerializeField] protected Vector3 blockScale = new Vector3(20f, 10f, 5f);
        [SerializeField] bool shielded = true;
        private Material blockMaterial;
        #endregion

        #region Attributes for Explosion Parameters
        [Header("Block Parameters")]
        [SerializeField] float blockCount = 8; // TODO: make int
        [SerializeField] int ringCount = 3;
        [SerializeField] float radius = 30f;
        #endregion

        protected List<Trail> trails = new List<Trail>();

        protected override void Start()
        {
            blockMaterial = shielded ? ThemeManager.Instance.GetTeamShieldedBlockMaterial(Ship.Team) 
                : Ship.TrailSpawner.GetBlockMaterial();
            base.Start();
        }

        protected override IEnumerator ExplodeCoroutine()
        {
            yield return new WaitForSeconds(ExplosionDelay);
        
            for (int ring = 0; ring < ringCount; ring++)
            {
                trails.Add(new Trail());
                for (int block = 0; block < blockCount; block++)
                    CreateRingBlock(block, ring % 2 * 0.5f, ring / 2f + 1f, ring, -ring / 2f, trails[ring]);
            }
        }

        private void CreateRingBlock(int i, float phase, float scale, float tilt, float sweep, Trail trail)
        {
            var forwardDirection = transform.forward;
            var offset = scale * radius * Mathf.Cos(((i + phase) / blockCount) * 2 * Mathf.PI) * transform.right +
                         scale * radius * Mathf.Sin(((i + phase) / blockCount) * 2 * Mathf.PI) * transform.up +
                         sweep * radius * forwardDirection;
            CreateBlock(transform.position + offset, offset + tilt * radius * forwardDirection, forwardDirection, "::AOE::" + Time.time + "::" + i, trail);
        }

        protected TrailBlock CreateBlock(Vector3 position, Vector3 forward, Vector3 up, string ownerId, Trail trail)
        {
            var block = Instantiate(trailBlock);
            block.Team = Team;
            block.ownerId = Ship.Player.PlayerUUID;
            block.Player = Ship.Player;
            block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
            block.GetComponent<MeshRenderer>().material = blockMaterial;
            block.ID = block.ownerId + ownerId + position;  
            block.TargetScale = blockScale;
            block.transform.parent = TrailSpawner.TrailContainer.transform;
            block.Trail = trail;
            if (shielded) block.TrailBlockProperties.IsShielded = true;
            trail.Add(block);
            return block;
        }
    }
}
