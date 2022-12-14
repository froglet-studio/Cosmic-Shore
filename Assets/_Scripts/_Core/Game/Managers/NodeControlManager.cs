using StarWriter.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;

public class NodeControlManager : Singleton<NodeControlManager>
{
    struct TeamVolume
    {
        public float volume;
        public Teams team;
    }
    [SerializeField] List<GameObject> Nodes;

    Dictionary<GameObject, TeamVolume> NodeVolumes = new Dictionary<GameObject, TeamVolume>();

    public void AddBlock(Teams team, TrailBlockProperties blockProperties)
    {
        foreach (var node in Nodes)
        {
            if (Vector3.Distance(blockProperties.position, node.transform.position) < node.transform.localScale.x)
            {
                //NodeVolumes[node]. += blockProperties.volume;
            }
        }
    }

    public void RemoveBlock(Teams team, TrailBlockProperties blockProperties)
    {

    }

    void Start()
    {
        foreach (var node in Nodes)
        {
            //NodeVolumes.Add(node, 0);
        }
    }
}
