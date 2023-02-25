using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using StarWriter.Core;
using UnityEngine;
using UnityEngine.Serialization;

public class DartBoard : MonoBehaviour
{
    [SerializeField] TrailBlock trailBlock;
    float blockCount = 6; // TODO: make int
    int ringCount = 25;
    float ringThickness = 5f;
    float gap = 6;
    //Vector3 blockScale = new Vector3(3f, 2f, 1f);


    //dartboard position
    float dartBoardRadius = 300;
    public float difficultyAngle = 5;
    //Vector3 dartBoardPosition;

    //[SerializeField] Material blockMaterial;
    List<Trail> trails = new List<Trail>();

    private void Start()
    {
        transform.position = Quaternion.Euler(
            Random.Range(0,360), 
            Random.Range(Mathf.Max(difficultyAngle - 30, 5) , difficultyAngle), 
            0) * (dartBoardRadius * Vector3.forward);

        transform.LookAt(Vector3.zero);
        CreateRings();
    }

    void CreateRings()
    {
        for (int ring = 1; ring <= ringCount; ring++)
        {
            trails.Add(new Trail());
            for (int block = 0; block < blockCount*ring; block++)
            {
                CreateRingBlock(block, ring % 2 * .5f, 0, 0, trails[ring-1], ring);
            }
        }
    }

    void CreateRingBlock(int i, float phase, float tilt, float sweep, Trail trail, int ring)
    {
        var position = transform.position +
                             ring * ringThickness * Mathf.Cos(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.right +
                             ring * ringThickness * Mathf.Sin(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.up +
                             sweep * ringThickness * transform.forward;
        CreateBlock(position, position + tilt * ringThickness * transform.forward, "::AOE::" + Time.time + "::" + i, trail,
            new Vector3(((Mathf.PI / 3f) * ringThickness) - (gap / (6 * ring)), 
                        (ringCount - ring) * ringThickness/4f,
                         ringThickness - (gap/5f)));
    }

    void CreateBlock(Vector3 position, Vector3 lookPosition, string ownerId, Trail trail, Vector3 scale)
    {
        var Block = Instantiate(trailBlock);
        //Block.Team = Team;
        //Block.ownerId = Ship.Player.PlayerUUID;
        //Block.PlayerName = Ship.Player.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        //Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.ID = Block.ownerId + ownerId;
        Block.InnerDimensions = scale;
        //Block.transform.parent = TrailSpawner.TrailContainer.transform;
        Block.Trail = trail;
        trail.Add(Block);
    }
}