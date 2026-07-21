using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    public class PostProcessingManager : Singleton<PostProcessingManager>
    {
        Volume thisVolume;
        // this serializes a new postprocess profile
        [SerializeField] VolumeProfile orthographicProfile;
        [SerializeField] VolumeProfile perspectiveProfile;

        // Speed-tunnel quasi dolly zoom: Panini override added at runtime to the volume's
        // INSTANTIATED profile (Volume.profile clones the asset), so the shared profile
        // asset is never mutated. Cache is revalidated against the live profile because
        // Orthographic() can swap profiles out from under it.
        PaniniProjection _speedTunnelPanini;
        VolumeProfile _speedTunnelProfile;

        // Start is called before the first frame update
        void Start()
        {
            thisVolume = GetComponent<Volume>();
        }

        /// <summary>
        /// Drive the speed-tunnel Panini projection distance [0..1]. Pairs with a camera
        /// FOV push (see <see cref="SpeedTunnelEffectController"/>) to produce a quasi
        /// dolly zoom without moving the camera. 0 deactivates the override.
        /// </summary>
        public void SetSpeedTunnelPanini(float distance)
        {
            if (!thisVolume) thisVolume = GetComponent<Volume>();
            if (!thisVolume) return;

            var profile = thisVolume.profile;
            if (_speedTunnelPanini == null || _speedTunnelProfile != profile)
            {
                _speedTunnelProfile = profile;
                if (!profile.TryGet(out _speedTunnelPanini))
                    _speedTunnelPanini = profile.Add<PaniniProjection>();
            }

            distance = Mathf.Clamp01(distance);
            _speedTunnelPanini.active = distance > 0.0001f;
            _speedTunnelPanini.distance.Override(distance);
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
