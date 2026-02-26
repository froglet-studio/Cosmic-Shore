using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    [DisallowMultipleComponent()]
    public class InstancerSSU : MonoBehaviour
    {
        //To prevent multiple components of subtypes.
        [HideInInspector]
        public Material runtimeMaterial;
    }
}
