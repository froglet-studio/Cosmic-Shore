using CosmicShore.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.UIElements;
using static Cinemachine.CinemachineFreeLook;

namespace CosmicShore
{
    public class SpawnableComet : SpawnableAbstractBase
    {
        [SerializeField] TrailBlock trailBlock;
        static int CometsSpawned = 0;

        #region Attributes for Explosion Parameters
        [Header("Block Parameters")]
        [SerializeField] int blockCount = 8;
        [SerializeField] int ringCount = 3;
        [SerializeField] float radius = 30f;
        [SerializeField] Vector3 blockScale = Vector3.one;
        [SerializeField] Vector3 Orgin;
        #endregion

        public override GameObject Spawn()
        {
            GameObject container = new GameObject();
            container.name = "COMET" + CometsSpawned++;

            var trail = new Trail();
            var position = new Vector3(Orgin.x, Orgin.y, Orgin.z);

            for (int ring = 0; ring < ringCount; ring++)
            {
                trails.Add(new Trail());

                // Creates increasing hemisphere
                for (int block = 0; block < blockCount; block++)
                {
                    CreateRingBlock(block, ring % 2 * 0.5f, ring / 2f + 1f, ring, -ring / 2f, trails[ring], container);
                }
                // Creates decreasing hemisphere  //TODO
                for (int block = blockCount; block > blockCount; block--)
                {
                    CreateRingBlock(block, ring % 2 * 0.5f, ring / 2f + 1f, ring, -ring / 2f, trails[ring], container);
                }
                // Creates decreasing cone and  trail of the comet //TODO
                CreateCone();
  
            }

            return container;
        }

        private void CreateCone()
        {
            throw new NotImplementedException();
        }

        private void CreateRingBlock(int block, float phase, float scale, float tilt, float sweep, Trail trail, GameObject container)
        {
            var forwardDirection = transform.forward;
            var offset = scale * radius * Mathf.Cos(((block + phase) / blockCount) * 2 * Mathf.PI) * transform.right +
                         scale * radius * Mathf.Sin(((block + phase) / blockCount) * 2 * Mathf.PI) * transform.up +
                         sweep * radius * forwardDirection;
            CreateBlock(transform.position + offset, offset + tilt * radius * forwardDirection, forwardDirection, container.name + "::BLOCK::" + block, trail, blockScale, trailBlock, container);
        }
        void CreateBlock(Vector3 position, Vector3 lookPosition, Vector3 up, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, GameObject container, Teams team = Teams.Blue)
        {
            var Block = Instantiate(trailBlock);
            Block.Team = team;
            Block.ownerId = "public";
            Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition, up));
            Block.transform.SetParent(container.transform, false);
            Block.ID = blockId;
            Block.TargetScale = scale;
            Block.Trail = trail;
            trail.Add(Block);
        }

    }
}
