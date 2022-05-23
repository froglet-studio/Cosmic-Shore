using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OptionsMenu : MonoBehaviour
{
    [SerializeField]
    private GameObject Main_Menu_Panel;

    public void OnPressButtonReturnToMain()
    {
        Main_Menu_Panel.SetActive(true);
        gameObject.SetActive(false);
    }
}
