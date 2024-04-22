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
    public class SpawnableCylinder : SpawnableAbstractBase
    {
        [SerializeField] Core.TrailBlock trailBlock;
        static int CylindersSpawned = 0;

        #region Attributes for Explosion Parameters
        [Header("Block Parameters")]
        [SerializeField] int blockCount = 12;

        [SerializeField] int ringCount = 15;
        [SerializeField] float radius = 30; //y scaler
        [SerializeField] float height = 60f; //x scaler
        [SerializeField] Vector3 blockScale = Vector3.one;
        [SerializeField] Vector3 Orgin;
        #endregion

        public override GameObject Spawn()
        {
            GameObject container = new GameObject();
            container.name = "COMET" + CylindersSpawned++;

            

            var trail = new Trail();
     

            
            // Cylinder //
            for (int ring = 0; ring < ringCount; ring++) //ring = X value
            {
                trails.Add(new Trail());

                // Creates ring
                for (int block = 0; block < blockCount; block++)
                {
                    float scale = (.5f*Mathf.Cos((ring) / - radius *((float)ringCount)*Mathf.PI) + .5f) * radius;

                    CreateRingBlock(block, ring % 2 * 0.5f, scale, (ring) / (float)ringCount,
                        ((ring) * height / ringCount) + radius, trails[ring], container);

                    /*CreateRingBlock(block, ring % 2 * 0.5f, scale,-radius - ((ring) / (float)ringCount * height),
                        ((ring) * height / ringCount) + radius, trails[ring], container);*/
                }
            }
            return container;
        }

        //Phase (0-.5) offsets everyother ring
        private void CreateRingBlock(int block, float phase, float scale, float tilt, float distanceTowardTail, Trail trail, GameObject container)
        {
            var offset = scale * radius *(((block + phase) / blockCount) * 2 * Mathf.PI) * transform.right +
                         scale * radius * (((block + phase) / blockCount) * 2 * Mathf.PI) * transform.up +
                         distanceTowardTail * -transform.forward;
            var tempBlockscale = new Vector3(blockScale.x * scale, blockScale.y, blockScale.z * scale);

            CreateBlock(transform.position + offset, tilt * transform.forward - (offset + transform.position), transform.forward, container.name + "::BLOCK::" + block, trail, tempBlockscale, trailBlock, container);
        }
        void CreateBlock(Vector3 position, Vector3 lookPosition, Vector3 up, string blockId, Trail trail, Vector3 scale, Core.TrailBlock trailBlock, GameObject container, Teams team = Teams.Blue)
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
