using CosmicShore.Game;
using CosmicShore.Core;
using System.Collections;
using UnityEngine;

namespace CosmicShore.Utility.Recording
{

    public class VCamRecorderController : MonoBehaviour
    {
        //[SerializeField] MiniGame game;
        [SerializeField] Player player;
        [SerializeField] Camera recordingCamera;
        // Start is called before the first frame update
        void Start()
        {
            StartCoroutine(LateStartCoroutine());
        }
        IEnumerator LateStartCoroutine() 
        {
            yield return new WaitForSeconds(3);
            if (recordingCamera != null)
            {
                Transform target = player.Ship.Transform;
                recordingCamera.transform.position = target.position;
                recordingCamera.transform.rotation = target.rotation;
            }
        }
    }
}
