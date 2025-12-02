using System.Collections.Generic;
using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Utility;

namespace CosmicShore.Game.Projectiles
{
    public class SpawnableFlower : SpawnableAbstractBase
    {
        [SerializeField] List<Prism> lastTwoBlocks;
        [FormerlySerializedAs("trailBlock")] [SerializeField] protected Prism prism;
        [SerializeField] Vector3 blockScale = new Vector3(20f, 10f, 5f);
        [SerializeField] int depth = 6;

        static int ObjectsSpawned = 0;

        Material material;
        public Material Material { get { return material; } set { material = new Material(value); } }
        public Domains Domain { get => domain; set => domain = value; }

        public override GameObject Spawn()
        {
            return Spawn(lastTwoBlocks);
        }

        public GameObject Spawn(List<Prism> lastTwoBlocks) 
        {
            GameObject container = new GameObject();
            container.name = "Flower" + ObjectsSpawned++;
            SeedBlocks(lastTwoBlocks, container);
            return container;
        }

        void SeedBlocks(List<Prism> lastTwoBlocks, GameObject container)
        {
            trails.Add(new Trail());
            var origin = (lastTwoBlocks[0].transform.position + lastTwoBlocks[1].transform.position) / 2;
            var maxGap = 2;//Mathf.Abs(lastTwoBlocks[0].transform.localScale.x - (blockScale.x / 2f));

            var angle = 30;
            for (int i = angle; i <= 180; i += angle)
            {
                Prism block1 = CreateBlock(lastTwoBlocks[0].transform.position, lastTwoBlocks[0].transform.forward, lastTwoBlocks[0].transform.up, prism.ownerID + 1 + i, trails[trails.Count - 1], container);
                Prism block2 = CreateBlock(lastTwoBlocks[1].transform.position, lastTwoBlocks[1].transform.forward, -lastTwoBlocks[1].transform.up, prism.ownerID + 2 + i, trails[trails.Count - 1], container);
                block1.transform.RotateAround(origin, block1.transform.forward, i);
                block2.transform.RotateAround(origin, block2.transform.forward, i);
                CreateBranches(block1, maxGap, angle / 2f, container, 1, depth);
                CreateBranches(block2, maxGap, angle / 2f, container, 1, depth);
            }
        }

        enum Branch
        {
            both = 0,
            first = -1,
            second = 1,
        }

        void CreateBranches(Prism prism, float gap, float angle, GameObject container, int handedness = 1, int depth = 0, Branch branch = Branch.both)
        {
            //var angle = 30;
            --depth;
            if (branch == Branch.both)
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    Prism block = CreateBlock(prism.transform.position, prism.transform.forward, prism.transform.up, $"Depth:{depth},Branch:{branch},I:{i}", trails[trails.Count - 1], container);
                    Vector3 origin = block.transform.position + (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
                    block.transform.RotateAround(origin, block.transform.forward, 180);
                    origin = block.transform.position - (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
                    block.transform.RotateAround(origin, block.transform.forward, i * angle);
                    block.transform.RotateAround(origin, (block.transform.position - origin).normalized, 90f * i);
                    if (depth > 0)
                    {
                        if (depth == 1) CreateBranches(block, gap, angle, container, - handedness, depth, (Branch)i);
                        else CreateBranches(block, gap, angle, container, -handedness, depth);
                    }
                }
            }
            else if (branch == Branch.first)
            {
                Prism block = CreateBlock(prism.transform.position, prism.transform.forward, prism.transform.up, $"Depth:{depth},Branch:{branch},Angle:{angle}", trails[trails.Count - 1], container);
                Vector3 origin = block.transform.position + (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
                block.transform.RotateAround(origin, block.transform.forward, 180 - angle * 4);
            }
            else
            {
                Prism block = CreateBlock(prism.transform.position, prism.transform.forward, prism.transform.up, $"Depth:{depth},Branch:{branch},Angle:{angle}", trails[trails.Count - 1], container);
                Vector3 origin = block.transform.position + (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
                block.transform.RotateAround(origin, block.transform.forward, 180 + angle * 4);
            }
        }

        protected Prism CreateBlock(Vector3 position, Vector3 lookPosition, Vector3 up, string ownerId, Trail trail, GameObject container)
        {
            var Block = Instantiate(prism, container.transform, false);
            Block.ChangeTeam(Domain);
            //Block.ownerId = Vessel.Player.PlayerUUID;
            //Block.PlayerName = Vessel.Player.PlayerName;
            Block.ownerID = "public";
            if (SafeLookRotation.TryGet(lookPosition, up, out var rotation, Block))
                Block.transform.SetPositionAndRotation(position, rotation);
            else
                Block.transform.position = position;
            //Block.GetComponent<MeshRenderer>().material = material;
            Block.ownerID = /*Block.ownerId +*/ ownerId + position;
            Block.TargetScale = blockScale;
            Block.Trail = trail;
            Block.Initialize();
            trail.Add(Block);
            return Block;
        }
    }
}