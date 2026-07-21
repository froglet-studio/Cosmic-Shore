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
        // Orthographic() can swap profiles out from under it. Home = whatever Panini
        // state the profile carried before the effect touched it — the effect offsets
        // from home and restores it exactly at zero.
        PaniniProjection _speedTunnelPanini;
        VolumeProfile _speedTunnelProfile;
        float _paniniHomeDistance;
        bool _paniniHomeActive;

        // Start is called before the first frame update
        void Start()
        {
            thisVolume = GetComponent<Volume>();
        }

        /// <summary>
        /// Drive the speed-tunnel Panini projection [0..1] as an OFFSET on top of the
        /// profile's own Panini state. Pairs with a camera FOV push (see
        /// <see cref="SpeedTunnelEffectController"/>) to produce a quasi dolly zoom
        /// without moving the camera. 0 restores exactly the pre-effect home state.
        /// </summary>
        public void SetSpeedTunnelPanini(float effectDistance)
        {
            if (!thisVolume) thisVolume = GetComponent<Volume>();
            if (!thisVolume) return;

            var profile = thisVolume.profile;
            if (_speedTunnelPanini == null || _speedTunnelProfile != profile)
            {
                _speedTunnelProfile = profile;
                if (profile.TryGet(out _speedTunnelPanini))
                {
                    _paniniHomeDistance = _speedTunnelPanini.distance.value;
                    _paniniHomeActive = _speedTunnelPanini.active;
                }
                else
                {
                    _speedTunnelPanini = profile.Add<PaniniProjection>();
                    _paniniHomeDistance = 0f;
                    _paniniHomeActive = false;
                }
            }

            if (effectDistance > 0.0001f)
            {
                _speedTunnelPanini.active = true;
                _speedTunnelPanini.distance.Override(Mathf.Clamp01(_paniniHomeDistance + effectDistance));
            }
            else
            {
                _speedTunnelPanini.distance.Override(_paniniHomeDistance);
                _speedTunnelPanini.active = _paniniHomeActive;
            }
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
