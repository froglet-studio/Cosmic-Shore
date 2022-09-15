using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class VersionDisplay : MonoBehaviour
{
    [SerializeField] TMPro.TMP_Text tmpText;
    [SerializeField] string prefix;
    
    void Start()
    {
        Debug.Log("Application Version : " + Application.version);
        tmpText.text = prefix + " " + Application.version;
    }
}
