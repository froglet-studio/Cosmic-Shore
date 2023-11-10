using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleUI : MonoBehaviour
{
    public GameObject Toggle1;
    public GameObject Toggle2;
    public void ToggleGameObject() //I think this is a better place for this to live
    {
        //toggleObject = GetComponent<GameObject>();
        if (Toggle1.activeInHierarchy == true)
            Toggle1.SetActive(false);
        else
            Toggle1.SetActive(true);

        if (Toggle2.activeInHierarchy == true)
            Toggle2.SetActive(false);
        else
            Toggle2.SetActive(true);
    }
}
