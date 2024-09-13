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
        [SerializeField] Core.TrailBlock trailBlock;
        [SerializeField] Teams team = Teams.Blue;
        static int CometsSpawned = 0;

        #region Attributes for Explosion Parameters
        [Header("Block Parameters")]
        [SerializeField] int blockCount = 8;

        [SerializeField] int ringCountHead = 4;
        [SerializeField] int ringCountTail = 9;
        [SerializeField] float headRadius = 30; //y scaler
        [SerializeField] float tailLength = 60f; //x scaler
        [SerializeField] Vector3 blockScale = Vector3.one;
        [SerializeField] Vector3 Orgin;
        #endregion

        public override GameObject Spawn()
        {
            GameObject container = new GameObject();
            container.name = "COMET" + CometsSpawned++;

            

            var trail = new Trail();

            // Head //
            for (int ring = 0; ring < ringCountHead; ring++) //ring = X value
            {
                trails.Add(new Trail());

                // Creates increasing hemisphere
                for (int block = 0; block < blockCount; block++)
                {
                    float scale = Mathf.Sqrt(Mathf.Pow(headRadius, 2) - Mathf.Pow(((ring / (float)ringCountHead) - 1) * headRadius, 2));   //y = sqrt(R^2 -X^2) scales the ring radius
                    CreateRingBlock(block, ring % 2 * 0.5f, scale, -headRadius, ring * headRadius/ringCountHead , trails[ring], container);
                }
            }
            // Tail //
            for (int ring = ringCountHead; ring < ringCountTail + ringCountHead; ring++) //ring = X value
            {
                trails.Add(new Trail());

                // Creates increasing hemisphere
                for (int block = 0; block < blockCount; block++)
                {
                    float scale = (.5f*Mathf.Cos((ring - ringCountHead) / ((float)ringCountTail)*Mathf.PI) + .5f) * headRadius;

                    CreateRingBlock(block, ring % 2 * 0.5f, scale,-headRadius - ((ring - ringCountHead) / (float)ringCountTail * tailLength),
                        ((ring - ringCountHead) * tailLength / ringCountTail) + headRadius, trails[ring], container);
                }
            }
            return container;
        }

        //Phase (0-.5) offsets everyother ring
        private void CreateRingBlock(int block, float phase, float scale, float tilt, float distanceTowardTail, Trail trail, GameObject container)
        {
            var offset = scale * Mathf.Cos(((block + phase) / blockCount) * 2 * Mathf.PI) * transform.right +
                         scale * Mathf.Sin(((block + phase) / blockCount) * 2 * Mathf.PI) * transform.up +
                         distanceTowardTail * -transform.forward;
            var tempBlockscale = new Vector3(blockScale.x * scale, blockScale.y, blockScale.z * scale);

            CreateBlock(transform.position + offset, tilt * transform.forward - (offset + transform.position), transform.forward, container.name + "::BLOCK::" + block, trail, tempBlockscale, trailBlock, container, team);
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
