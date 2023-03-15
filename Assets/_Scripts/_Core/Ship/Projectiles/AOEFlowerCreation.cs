using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core;
using UnityEngine.UIElements;

public class AOEFlowerCreation : AOEBlockCreation
{

    protected override IEnumerator ExplodeCoroutine()
    {
        yield return new WaitForSeconds(ExplosionDelay);

        //for (int ring = 0; ring < ringCount; ring++)
        //{
        //    trails.Add(new Trail());
        //    for (int block = 0; block < blockCount; block++)
        //    {
        //        CreateRingBlock(block, ring % 2 * .5f, ring / 2f + 1f, ring, -ring / 2f, trails[ring]);
        //    }
        //}
        yield return new WaitForEndOfFrame();
    }


    public void SetBlockDimensions(Vector3 InnerDimensions)
    {
        blockScale = InnerDimensions;
    }

    

    public void SeedBlocks(List<TrailBlock> lastTwoBlocks, float gap)
    {
        trails.Add(new Trail());
        var origin = (lastTwoBlocks[0].transform.position + lastTwoBlocks[1].transform.position) / 2;
        var blockDistance = (lastTwoBlocks[0].transform.position - lastTwoBlocks[1].transform.position).magnitude / 2f;
        var maxGap = blockDistance - (blockScale.x/2f);
        //var up = block1.transform.up;
        //var forward = block1.transform.forward;
        //block1.transform.position = origin + blockDistance * up;
        //block2.transform.position = origin - blockDistance * up;
        var angle = 90;
        for (int i = angle; i < 180; i += angle) 
        {
            TrailBlock block1 = CreateBlock(lastTwoBlocks[0].transform.position, lastTwoBlocks[0].transform.forward, lastTwoBlocks[0].transform.up, trailBlock.ID + 1, trails[trails.Count - 1]);
            TrailBlock block2 = CreateBlock(lastTwoBlocks[1].transform.position, lastTwoBlocks[1].transform.forward, -lastTwoBlocks[1].transform.up, trailBlock.ID + 2, trails[trails.Count - 1]);
            block1.transform.RotateAround(origin, block1.transform.forward, i);
            block2.transform.RotateAround(origin, block2.transform.forward, i);
            CreateBranches(block1, maxGap * gap);
            CreateBranches(block2, maxGap * gap);
        }
        CreateBranches(lastTwoBlocks[0], maxGap * gap);
        lastTwoBlocks[1].transform.Rotate(0, 0, 180);
        CreateBranches(lastTwoBlocks[1], maxGap * gap);
    }

    enum Branch
    {
        both = 0,
        first = -1,
        second = 1,
    }
  
    void CreateBranches(TrailBlock trailBlock, float gap, int handedness = 1, int depth = 2, Branch branch = Branch.both)
    {
        var angle = 45;
        --depth;
        if (branch == Branch.both) 
        {
            for (int i = -1; i <= 1; i += 2)
            {
                TrailBlock block = CreateBlock(trailBlock.transform.position, trailBlock.transform.forward, trailBlock.transform.up, trailBlock.ID + i, trails[trails.Count - 1]);
                Vector3 origin = block.transform.position + (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
                block.transform.RotateAround(origin, block.transform.forward, 180 + i*angle);
                if (depth > 0)
                {
                    if (depth == 1) CreateBranches(block, gap, -handedness, depth, (Branch)i);
                    else CreateBranches(block, gap, -handedness, depth);
                }
            }
        }
        else if (branch == Branch.first)
        {
            TrailBlock block = CreateBlock(trailBlock.transform.position, trailBlock.transform.forward, trailBlock.transform.up, trailBlock.ID + -angle, trails[trails.Count - 1]);
            Vector3 origin = block.transform.position + (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
            block.transform.RotateAround(origin, block.transform.forward, 180 - angle);
        }
        else
        {
            TrailBlock block = CreateBlock(trailBlock.transform.position, trailBlock.transform.forward, trailBlock.transform.up, trailBlock.ID + angle, trails[trails.Count - 1]);
            Vector3 origin = block.transform.position + (block.transform.right * (blockScale.x / 2 + gap)) * handedness;
            block.transform.RotateAround(origin, block.transform.forward, 180 + angle);
        }
        
    }

    protected TrailBlock CreateBlock(Vector3 position, Vector3 lookPosition, Vector3 up, string ownerId, Trail trail)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = Team;
        Block.ownerId = Ship.Player.PlayerUUID;
        Block.PlayerName = Ship.Player.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition, up));
        Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.ID = Block.ownerId + ownerId + position;
        Block.InnerDimensions = blockScale;
        Block.transform.parent = TrailSpawner.TrailContainer.transform;
        Block.Trail = trail;
        trail.Add(Block);
        return Block;
    }

}
