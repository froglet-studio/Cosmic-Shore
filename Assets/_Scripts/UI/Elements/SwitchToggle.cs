using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SwitchToggle : MonoBehaviour
{
    [SerializeField]
    private RectTransform handleRectTransform;

    private Toggle toggle;

    private Vector3 handlePosition = new Vector3(20,0,0);
    
    
    void Awake()
    {
        toggle = GetComponent<Toggle>();
        toggle.onValueChanged.AddListener(Toggled);
    }

    public void Toggled(bool status)
    {
        int sign;

        sign = status ? -1 : 1;
        handleRectTransform.localPosition += sign * handlePosition;
    }

    public void SetToggleValue(bool status)
    {
        toggle.onValueChanged.RemoveListener(Toggled);
        toggle.isOn = status;
        toggle.onValueChanged.AddListener(Toggled);
    }

    private void OnDestroy()
    {
        toggle.onValueChanged.RemoveListener(Toggled);
    }
}
