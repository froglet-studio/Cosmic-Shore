using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using CosmicShore.Utility.Singleton;

namespace CosmicShore
{
    public class PostProcessingManager : SingletonPersistent<PostProcessingManager>
    {
        Volume thisVolume;
        // this serializes a new postprocess profile
        [SerializeField] VolumeProfile orthographicProfile;
        [SerializeField] VolumeProfile perspectiveProfile;

        // Start is called before the first frame update
        void Start()
        {
            thisVolume = GetComponent<Volume>();
        }

        public void Orthographic(bool isOrthographic)
        {
            if (isOrthographic)
            {
                thisVolume.profile = orthographicProfile;
            }
            else if (!thisVolume)
            {
                thisVolume = GetComponent<Volume>();
                thisVolume.profile = perspectiveProfile;
            }
            else
            {
                thisVolume.profile = perspectiveProfile;
            }
        }
    }
}
