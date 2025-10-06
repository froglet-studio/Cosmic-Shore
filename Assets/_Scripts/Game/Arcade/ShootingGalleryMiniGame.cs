using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game.Arcade
{
    public class ShootingGalleryMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] SegmentSpawner SegmentSpawner;

        public static new VesselClassType PlayerVesselType = VesselClassType.Urchin;

        protected override void Start()
        {
            base.Start();

            SegmentSpawner.Seed = new System.Random().Next();
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();

            VesselPrismController.NukeTheTrails();

            SegmentSpawner.NumberOfSegments = 60;
            SegmentSpawner.Initialize();

            Crystal.transform.position = CrystalStartPosition;
            ActivePlayer.Vessel.VesselStatus.VesselPrismController.PauseTrailSpawner();

            FormRing();
        }

        [FormerlySerializedAs("trailBlock")] [SerializeField] protected Prism prism;
        [SerializeField] float blockCount = 20;
        [SerializeField] float radius = 60f;
        [SerializeField] protected Vector3 blockScale = new Vector3(20f, 10f, 5f);
        [SerializeField] protected Material blockMaterial;
        List<Trail> trails = new ();

        public void SetBlockMaterial(Material material)
        {
            blockMaterial = material;
        }

        void FormRing()
        {
            trails.Add(new Trail(true));
            for (int block = 0; block < blockCount; block++)
            {
                CreateRingBlock(block, trails[0]);
            }
        }

        virtual protected void CreateRingBlock(int i, Trail trail)
        {
            var position = transform.position +
                                 radius * Mathf.Cos((i / blockCount) * 2 * Mathf.PI) * transform.right +
                                 radius * Mathf.Sin((i / blockCount) * 2 * Mathf.PI) * transform.up +
                                 radius * transform.forward;
            CreateBlock(position, position, trail);
        }

        virtual protected Prism CreateBlock(Vector3 position, Vector3 lookPosition, Trail trail)
        {
            var Block = Instantiate(prism);
            Block.ChangeTeam(ActivePlayer.Domain);
            // Block.ownerID = ActivePlayer.PlayerUUID;
            Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
            // Block.ownerID = Block.ownerID + position;
            Block.TargetScale = blockScale;
            Block.Trail = trail;
            Block.Initialize(ActivePlayer.Name);
            trail.Add(Block);
            return Block;
        }
    }
}