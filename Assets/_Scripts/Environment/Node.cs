using System.Collections.Generic;
using UnityEngine;

public class Node : MonoBehaviour
{
    [SerializeField] public string ID;
    [SerializeField] float volumeControlThreshold = 100f;
    [SerializeField] SnowChanger SnowChanger;
    [SerializeField] Crystal Crystal;

    Dictionary<Teams, float> teamVolumes = new Dictionary<Teams, float>();

    void Start()
    {
        teamVolumes.Add(Teams.Green, 0);
        teamVolumes.Add(Teams.Red, 0);

        SnowChanger.SetOrigin(transform.position);
        Crystal.SetOrigin(transform.position);
    }

    public bool ContainsPosition(Vector3 position)
    {
        return Vector3.Distance(position, transform.position) < transform.localScale.x; //only works if nodes remain spherical
    }

    public void ChangeVolume(Teams team, float volume)
    {
        if (!teamVolumes.ContainsKey(team))
            teamVolumes.Add(team, 0);

        teamVolumes[team] += volume;
    }

    public float GetTeamVolume(Teams team)
    {
        if (!teamVolumes.ContainsKey(team))
            return 0;

        return teamVolumes[team];
    }

    public Teams ControllingTeam
    {
        get
        {
            if (!enabled || !teamVolumes.ContainsKey(Teams.Green) || !teamVolumes.ContainsKey(Teams.Red))
                return Teams.None;

            if (teamVolumes[Teams.Green] < volumeControlThreshold && teamVolumes[Teams.Red] < volumeControlThreshold)
                return Teams.None;

            if (teamVolumes[Teams.Green] == teamVolumes[Teams.Red])
                return Teams.None;

            if (teamVolumes[Teams.Green] > teamVolumes[Teams.Red])
                return Teams.Green;
            else
                return Teams.Red;
        }
    }
}