using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OptionsMenu : MonoBehaviour
{
    public GameObject Main_Menu_Panel;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnPressButtonReturnToMain()
    {
        Debug.Log("Game Settings Pressed");
        Main_Menu_Panel.SetActive(true);
        gameObject.SetActive(false);
    }
}
