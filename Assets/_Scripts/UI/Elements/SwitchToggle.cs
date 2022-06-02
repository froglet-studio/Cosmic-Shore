using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SwitchToggle : MonoBehaviour
{
    [SerializeField]
    private RectTransform handleRectTransform;

    private Toggle toggle;

    private Vector3 handleDisplacement = new Vector3(20,0,0);
    
    
    void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(Toggled);
    }

    public void Toggled(bool status)
    {
        int sign = status ? 1 : -1;
        handleRectTransform.localPosition += sign * handleDisplacement;
    }

    private void OnDestroy()
    {
        toggle.onValueChanged.RemoveListener(Toggled);
    }
}