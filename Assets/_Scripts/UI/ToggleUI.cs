using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleUI : MonoBehaviour
{
    private GameObject toggleObject;
    public void ToggleGameObject() //I think this is a better place for this to live
    {
        toggleObject = GetComponent<GameObject>();
        if (toggleObject.activeInHierarchy == true)
            toggleObject.SetActive(false);
        else
            toggleObject.SetActive(true);
    }
}
