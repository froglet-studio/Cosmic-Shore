using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores and allows changes to Player Attribute and Gameplay related stats
/// </summary>

[System.Serializable]
public class PlayerStats : MonoBehaviour
{
    //Player Attribute Stats
    [SerializeField]
    private float maxSpeed = 0f;
    [SerializeField]
    private float minSpeed = 0f;

    
    //Gameplay Stats
    [SerializeField]
    private float totalWins = 0f;
    [SerializeField]
    private float totalLosses = 0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
