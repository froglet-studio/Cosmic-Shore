using System.Collections;
using System.Collections.Generic;
using StarWriter.Core;
using UnityEngine;

public class SpawnableDartBoard : SpawnableAbstractBase
{
    [SerializeField] TrailBlock greenTrailBlock;
    [SerializeField] TrailBlock redTrailBlock;

    GameObject container;


    int blockCount = 6;
    int ringCount = 24;
    float ringThickness = 5f;
    float gap = 6;

    public Player PlayerOne;
    public Player PlayerTwo;

    //dartboard position
    public float dartBoardRadius = 100;

    public float difficultyAngle = 40;

    void Start()
    {
        // TODO these should be injected by the game
        PlayerOne.Team = Teams.Green;
        PlayerTwo.Team = Teams.Red;

    }


    void CreateRings()
    {
        TrailBlock trailBlock;
        Player player;
        container = new GameObject();
        container.name = "SpawnedDartBoard";

        for (int ring = 1; ring <= ringCount; ring++)
        {
            trails.Add(new Trail());
            for (int block = 0; block < blockCount * ring; block++)
            {
                if ((block / ring + ring/3) % 2 == 0)// || (block / ring) % 6 == 1 || (block / ring) % 6 == 2) 
                { trailBlock = greenTrailBlock; player = PlayerOne; }
                else { trailBlock = redTrailBlock; player = PlayerTwo; }
                CreateRingBlock(block, 0, 0, 0, trails[ring-1], ring, trailBlock, player); // old value for phase = ring % 2 * .5f
            }
        }
    }

    void CreateRingBlock(int i, float phase, float tilt, float sweep, Trail trail, int ring, TrailBlock trailBlock, Player player)
    {
        var position = transform.position +
                             ring * ringThickness * Mathf.Cos(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.right +
                             ring * ringThickness * Mathf.Sin(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.up +
                             sweep * ringThickness * transform.forward;
        CreateBlock(position, position + tilt * ringThickness * transform.forward, "::SpawnableDartBoard::" + Time.time + "::" + i, trail,
            new Vector3(((Mathf.PI / 3f) * ringThickness) - (gap / (6 * ring)), // blockwidth 
                        (ringCount - ring) * ringThickness/3f, // dartboard thickness
                         ringThickness - (gap/5f)), trailBlock, player); //annulus thickness
    }

    void CreateBlock(Vector3 position, Vector3 lookPosition, string blockId, Trail trail, Vector3 scale, TrailBlock trailBlock, Player player)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = player.Team;
        Block.ownerId = player.PlayerUUID;
        Block.PlayerName = player.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        Block.transform.SetParent(container.transform, false);
        Block.ID = blockId;
        Block.InnerDimensions = scale;
        Block.Trail = trail;
        trail.Add(Block);
    }

    public override GameObject Spawn()
    {
        CreateRings();
        return container;
    }
}