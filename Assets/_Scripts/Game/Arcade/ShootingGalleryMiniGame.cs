using CosmicShore.Core;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class ShootingGalleryMiniGame : MiniGame
    {
        [SerializeField] Crystal Crystal;
        [SerializeField] Vector3 CrystalStartPosition;
        [SerializeField] SegmentSpawner SegmentSpawner;

        public static new ShipClassType PlayerShipType = ShipClassType.Urchin;

        protected override void Start()
        {
            base.Start();

            SegmentSpawner.Seed = new System.Random().Next();
        }

        protected override void SetupTurn()
        {
            base.SetupTurn();

            TrailSpawner.NukeTheTrails();

            SegmentSpawner.NumberOfSegments = 60;
            SegmentSpawner.Initialize();

            Crystal.transform.position = CrystalStartPosition;
            ActivePlayer.Ship.ShipStatus.TrailSpawner.PauseTrailSpawner();

            FormRing();
        }

        [SerializeField] protected TrailBlock trailBlock;
        [SerializeField] float blockCount = 20;
        [SerializeField] float radius = 60f;
        [SerializeField] protected Vector3 blockScale = new Vector3(20f, 10f, 5f);
        [SerializeField] protected Material blockMaterial;
        protected List<Trail> trails = new List<Trail>();

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

        virtual protected TrailBlock CreateBlock(Vector3 position, Vector3 lookPosition, Trail trail)
        {
            var Block = Instantiate(trailBlock);
            Block.ChangeTeam(ActivePlayer.Team);
            Block.ownerID = ActivePlayer.PlayerUUID;
            Block.PlayerName = ActivePlayer.Name;
            Block.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
            Block.ownerID = Block.ownerID + position;
            Block.TargetScale = blockScale;
            Block.Trail = trail;
            trail.Add(Block);
            return Block;
        }
    }
}