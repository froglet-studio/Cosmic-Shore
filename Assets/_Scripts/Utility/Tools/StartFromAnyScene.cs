using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartFromAnyScene : MonoBehaviour
{

    [SerializeField] List<GameObject> MainMenuSingletonPersistents;

    void Awake()
    {
        if (MainMenuSingletonPersistents != null && GameManager.Instance == null)
        {
            foreach (var go in MainMenuSingletonPersistents)
                Instantiate(go);

            StartCoroutine(SetupCameraCoroutine());
        }
    }

    IEnumerator SetupCameraCoroutine()
    {
        yield return new WaitForEndOfFrame();
        CameraManager.Instance.SetupGamePlayCameras();
    }


}