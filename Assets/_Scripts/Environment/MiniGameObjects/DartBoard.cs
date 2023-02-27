using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using StarWriter.Core;
using UnityEngine;
using UnityEngine.Serialization;

public class DartBoard : MonoBehaviour
{
    [SerializeField] TrailBlock greenTrailBlock;
    [SerializeField] TrailBlock redTrailBlock;
    
    float blockCount = 6; // TODO: make int
    int ringCount = 25;
    float ringThickness = 5f;
    float gap = 6;
    //Vector3 blockScale = new Vector3(3f, 2f, 1f)

    Player greenPlayer;
    Player redPlayer;

    //dartboard position
    float dartBoardRadius = 300;

    public float difficultyAngle = 40;
    public int numberOfDartBoards = 4;
    //Vector3 dartBoardPosition;

    //[SerializeField] Material blockMaterial;
    List<Trail> trails = new List<Trail>();

    private void Start()
    {
        greenPlayer = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
        redPlayer = GameObject.FindGameObjectWithTag("red").GetComponent<Player>();
        for (int i = 0; i< numberOfDartBoards; i++)
        {
            transform.position = Quaternion.Euler(0,0, Random.Range(i*90, i*90 + 20)) *
                (Quaternion.Euler(0, Random.Range(Mathf.Max(difficultyAngle - 20, 40), difficultyAngle), 0) *
                (dartBoardRadius * Vector3.forward));

            transform.LookAt(Vector3.zero);
            CreateRings();
        }
        
    }

    void CreateRings()
    {
        TrailBlock trailBlock;
        Player player;
        for (int ring = 1; ring <= ringCount; ring++)
        {
            trails.Add(new Trail());
            for (int block = 0; block < blockCount * ring; block++)
            {
                if ((block / ring + ring/3) % 2 == 0)// || (block / ring) % 6 == 1 || (block / ring) % 6 == 2) 
                { trailBlock = greenTrailBlock; player = greenPlayer; }
                else { trailBlock = redTrailBlock; player = redPlayer; }
                CreateRingBlock(block, ring % 2 * .5f, 0, 0, trails[ring-1], ring, trailBlock, player);
            }
        }
    }

    void CreateRingBlock(int i, float phase, float tilt, float sweep, Trail trail, int ring, TrailBlock trailBlock, Player player)
    {
        var position = transform.position +
                             ring * ringThickness * Mathf.Cos(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.right +
                             ring * ringThickness * Mathf.Sin(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.up +
                             sweep * ringThickness * transform.forward;
        CreateBlock(position, position + tilt * ringThickness * transform.forward, "::AOE::" + Time.time + "::" + i, trail,
            new Vector3(((Mathf.PI / 3f) * ringThickness) - (gap / (6 * ring)), 
                        (ringCount - ring) * ringThickness/3f,
                         ringThickness - (gap/5f)), trailBlock, player);
    }

    void CreateBlock(Vector3 position, Vector3 lookPosition, string ownerId, Trail trail, Vector3 scale, TrailBlock trailBlock, Player player)
    {
        var Block = Instantiate(trailBlock);
        Block.Team = player.Team;
        Block.ownerId = player.PlayerUUID;
        Block.PlayerName = player.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        //Block.GetComponent<MeshRenderer>().material = material;
        Block.ID = Block.ownerId + ownerId;
        Block.InnerDimensions = scale;
        //Block.transform.parent = TrailSpawner.TrailContainer.transform;
        Block.Trail = trail;
        trail.Add(Block);
    }
}