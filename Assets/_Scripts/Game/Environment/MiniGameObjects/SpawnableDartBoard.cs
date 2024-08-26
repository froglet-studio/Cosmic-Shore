
using CosmicShore.Core;
using UnityEngine;

public class SpawnableDartBoard : SpawnableAbstractBase
{
    [SerializeField] TrailBlock greenTrailBlock;
    [SerializeField] TrailBlock redTrailBlock;
    [SerializeField] int blockCount = 6;
    [SerializeField] int ringCount = 30;
    [SerializeField] float ringThickness = 5f;
    [SerializeField] float gap = 6;

    GameObject container;

    void CreateRings()
    {
        TrailBlock trailBlock;
        Teams team;
        container = new()
        {
            name = "SpawnedDartBoard"
        };

        for (int ring = 1; ring <= ringCount; ring++)
        {
            trails.Add(new Trail());
            for (int block = 0; block < blockCount * ring; block++)
            {
                if ( (block / ring + ring/3) % 2 == 0)
                { 
                    trailBlock = greenTrailBlock;
                    team = Teams.Jade; 
                }
                else 
                { 
                    trailBlock = redTrailBlock; 
                    team = Teams.Ruby;
                }
                CreateRingBlock(block, 0, 0, 0, trails[ring-1], ring, trailBlock, team); // old value for phase = ring % 2 * .5f
            }
        }
    }

    void CreateRingBlock(int i, float phase, float tilt, float sweep, Trail trail, int ring, TrailBlock trailBlock, Teams team)
    {
        var position = transform.position +
                             ring * ringThickness * Mathf.Cos(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.right +
                             ring * ringThickness * Mathf.Sin(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.up +
                             sweep * ringThickness * transform.forward;
        CreateBlock(position, position + tilt * ringThickness * transform.forward, "::SpawnableDartBoard::" + Time.time + "::" + i, trail,
            new Vector3(((Mathf.PI / 3f) * ringThickness) - (gap / (2 * ring)), // blockwidth 
                        (ringCount - ring) * ringThickness/3f, // dartboard thickness
                         ringThickness - (gap/5f)), trailBlock, team); //annulus thickness
    }

    void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, Teams team)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = team;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        Block.transform.SetParent(container.transform, false);
        Block.ID = blockId;
        Block.TargetScale = scale;
        Block.Trail = trail;
        trail.Add(Block);
    }

    public override GameObject Spawn()
    {
        CreateRings();
        return container;
    }
}