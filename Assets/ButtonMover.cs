using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonMover : MonoBehaviour
{
    [SerializeField]
    RectTransform button;
    bool toggle;
    

    public void MoveButton()
    {
        int sign;
        if (toggle) {sign = -1;} else {sign = 1;}
        button.localPosition += new Vector3(sign*20,0,0);
        toggle = !toggle;
    }
}
