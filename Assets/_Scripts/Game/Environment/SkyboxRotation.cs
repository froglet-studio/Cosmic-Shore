using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Environment
{
    public class SkyboxRotation : MonoBehaviour
    {
        // Rotate the Skybox Model Geobox on the z axis
        void Update()
        {
            transform.Rotate(new Vector3(.02f, 0f, .03f));
        }
    }
}
