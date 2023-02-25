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
    int ringCount = 6;
    float ringThickness = 30f;
    Vector3 blockScale = new Vector3(20f, 10f, 5f);


    //dartboard position
    float dartBoardRadius = 300;
    public float difficultyAngle = 20;
    //Vector3 dartBoardPosition;

    //[SerializeField] Material blockMaterial;
    List<Trail> trails = new List<Trail>();

    private void Start()
    {
        transform.position = Quaternion.Euler(
            Random.Range(0,360), 
            Random.Range(0, difficultyAngle), 
            0) * (dartBoardRadius * Vector3.forward);

        transform.LookAt(Vector3.zero);
        CreateRings();
    }

    void CreateRings()
    {
        for (int ring = 0; ring < ringCount; ring++)
        {
            trails.Add(new Trail());
            for (int block = 0; block < blockCount*ring; block++)
            {
                CreateRingBlock(block, ring % 2 * .5f, ring / 2f + 1f, 0, 0, trails[ring], ring);
            }
        }
    }

    void CreateRingBlock(int i, float phase, float scale, float tilt, float sweep, Trail trail, int ring)
    {
        var position = transform.position +
                             scale * ringThickness * Mathf.Cos(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.right +
                             scale * ringThickness * Mathf.Sin(((i + phase) / (blockCount * ring)) * 2 * Mathf.PI) * transform.up +
                             sweep * ringThickness * transform.forward;
        CreateBlock(position, position + tilt * ringThickness * transform.forward, "::AOE::" + Time.time + "::" + i, trail);
    }

    void CreateBlock(Vector3 position, Vector3 lookPosition, string ownerId, Trail trail)
    {
        var Block = Instantiate(trailBlock);
        //Block.Team = Team;
        //Block.ownerId = Ship.Player.PlayerUUID;
        //Block.PlayerName = Ship.Player.PlayerName;
        Block.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookPosition - transform.position, transform.forward));
        //Block.GetComponent<MeshRenderer>().material = blockMaterial;
        Block.ID = Block.ownerId + ownerId;
        Block.InnerDimensions = blockScale;
        //Block.transform.parent = TrailSpawner.TrailContainer.transform;
        Block.Trail = trail;
        trail.Add(Block);
    }
}