using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;


namespace CosmicShore.Game.Projectiles
{
    public class AOEFlowerCreation : AOEBlockCreation
    {
        [SerializeField] int TunnelAmount = 3;
        enum Branch
        {
            first = -1,
            both = 0,
            second = 1,
        }

        protected override IEnumerator ExplodeCoroutine()
        {
            yield return new WaitForSeconds(ExplosionDelay);

            var count = 0f;
            int currentPosition = Ship.TrailSpawner.TrailLength - 1;
            while (count < TunnelAmount)
            {
                if (currentPosition < Ship.TrailSpawner.TrailLength)
                {
                    count++;
                    currentPosition++;
                    SetBlockDimensions(Ship.TrailSpawner.TargetScale);
                    var lastTwoBlocks = Ship.TrailSpawner.GetLastTwoBlocks();
                    if(lastTwoBlocks != null)
                        SeedBlocks(lastTwoBlocks);
                }
                yield return null;
            }
        }

        public void SetBlockDimensions(Vector3 InnerDimensions)
        {
            blockScale = InnerDimensions;
        }

        public void SeedBlocks(List<TrailBlock> lastTwoBlocks)
        {
            trails.Add(new Trail());
            var origin = (lastTwoBlocks[0].transform.position + lastTwoBlocks[1].transform.position) / 2;
            var maxGap = Mathf.Abs(lastTwoBlocks[0].transform.localScale.x - (blockScale.x/2f));
   
            var angle = 30;
            for (int i = angle; i < 180; i += angle) 
            {
                TrailBlock block1 = CreateBlock(lastTwoBlocks[0].transform.position, lastTwoBlocks[0].transform.forward, lastTwoBlocks[0].transform.up, trailBlock.ID + 1 + i, trails[trails.Count - 1]);
                TrailBlock block2 = CreateBlock(lastTwoBlocks[1].transform.position, lastTwoBlocks[1].transform.forward, -lastTwoBlocks[1].transform.up, trailBlock.ID + 2 + i, trails[trails.Count - 1]);
                block1.transform.RotateAround(origin, block1.transform.forward, i);
                block2.transform.RotateAround(origin, block2.transform.forward, i);
                CreateBranches(block1, maxGap, angle/2f);
                CreateBranches(block2, maxGap, angle/2f);
            }
            CreateBranches(lastTwoBlocks[0], maxGap, angle/2f);
            lastTwoBlocks[1].transform.Rotate(0, 0, 180);
            CreateBranches(lastTwoBlocks[1], maxGap, angle/2f);
        }

  
        void CreateBranches(TrailBlock trailBlock, float gap, float angle, int handedness = 1, int depth = 0, Branch branch = Branch.both)
        {
            --depth;
            if (branch == Branch.both) 
            {
                for (int i = -1; i <= 1; i += 2)
                {
                    TrailBlock block = CreateBlock(trailBlock.transform.position, trailBlock.transform.forward, trailBlock.transform.up, trailBlock.ID + i, trails[trails.Count - 1]);
                    Vector3 origin = block.transform.position + (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
                    block.transform.RotateAround(origin, block.transform.forward, 180);
                    origin = block.transform.position - (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
                    block.transform.RotateAround(origin, block.transform.forward, i * angle);
                    block.transform.RotateAround(origin, (block.transform.position - origin).normalized, 90f*i);
                    if (depth > 0)
                    {
                        if (depth == 1) CreateBranches(block, gap, angle, -handedness, depth, (Branch)i);
                        else CreateBranches(block, gap, angle, -handedness, depth);
                    }
                }
            }
            else
            {
                if (branch == Branch.first)
                    angle = -angle;

                TrailBlock block = CreateBlock(trailBlock.transform.position, trailBlock.transform.forward, trailBlock.transform.up, trailBlock.ID + angle, trails[trails.Count - 1]);
                Vector3 origin = block.transform.position + (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
                block.transform.RotateAround(origin, block.transform.forward, 180 + angle * 4);
            }
        }
    }
}