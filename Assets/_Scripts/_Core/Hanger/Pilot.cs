using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pilot : MonoBehaviour
{
    [SerializeField] string pilotName;

    [SerializeField] SO_Pilot pilotSO;

    public string PilotName { get => pilotName; }

    // Start is called before the first frame update
    void Start()
    {
        pilotName = pilotSO.CharacterName;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
