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

        // Silence vignette (Docs/ElementalAbilitySystem/SILENCE.md §5): the same home/offset shape as
        // the Panini above, on the profile's own Vignette override (the gameplay profile authors one,
        // switched off). Home = the override's state before silence touched it, restored exactly at 0.
        Vignette _silenceVignette;
        VolumeProfile _silenceProfile;
        bool _vignetteHomeActive;
        Color _vignetteHomeColor;
        float _vignetteHomeIntensity;
        float _vignetteHomeSmoothness;
        bool _vignetteHomeColorOverride, _vignetteHomeIntensityOverride, _vignetteHomeSmoothnessOverride;

        // Start is called before the first frame update
        void Start()
        {
            thisVolume = GetComponent<Volume>();
        }

        /// <summary>
        /// Drive the speed-tunnel Panini projection as a SIGNED offset on top of the
        /// profile's own Panini state (the tunnel pulls it negative, below the shared
        /// baseline). Pairs with a camera FOV narrow to produce a quasi dolly zoom without
        /// moving the camera. 0 restores exactly the pre-effect home state.
        ///
        /// SINGLE WRITER: this is one global override with no ref-counting, so the last
        /// caller wins and any caller passing 0 releases everyone else's effect. The only
        /// sanctioned driver is the <c>VesselSpeedTunnel</c> platform law
        /// (Docs/SPEED_TUNNEL.md) — do not add a second caller, and in particular do not
        /// drive this from a per-vessel component, which races itself across a vessel swap.
        /// </summary>
        public void SetSpeedTunnelPanini(float offset)
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

            if (Mathf.Abs(offset) > 0.0001f)
            {
                _speedTunnelPanini.active = true;
                _speedTunnelPanini.distance.Override(Mathf.Clamp01(_paniniHomeDistance + offset));
            }
            else
            {
                _speedTunnelPanini.distance.Override(_paniniHomeDistance);
                _speedTunnelPanini.active = _paniniHomeActive;
            }
        }

        /// <summary>
        /// Drive the SILENCE vignette: a danger-coloured edge on the local pilot's screen while
        /// their abilities are silenced (<c>VesselSilence</c>). <paramref name="weight"/> 0..1 fades
        /// it between the profile's own vignette (home) and <paramref name="color"/> at
        /// <paramref name="intensity"/> / <paramref name="smoothness"/>; 0 restores home exactly.
        ///
        /// <para>SINGLE WRITER, like <see cref="SetSpeedTunnelPanini"/>: one global override with no
        /// ref-counting. The only sanctioned driver is <c>SilenceScreenFeedback</c>.</para>
        /// </summary>
        public void SetSilenceVignette(float weight, Color color, float intensity, float smoothness)
        {
            if (!thisVolume) thisVolume = GetComponent<Volume>();
            if (!thisVolume) return;

            var profile = thisVolume.profile;
            if (_silenceVignette == null || _silenceProfile != profile)
            {
                _silenceProfile = profile;
                if (!profile.TryGet(out _silenceVignette))
                {
                    _silenceVignette = profile.Add<Vignette>();
                    _silenceVignette.active = false;
                }
                _vignetteHomeActive = _silenceVignette.active;
                _vignetteHomeColor = _silenceVignette.color.value;
                _vignetteHomeIntensity = _silenceVignette.intensity.value;
                _vignetteHomeSmoothness = _silenceVignette.smoothness.value;
                _vignetteHomeColorOverride = _silenceVignette.color.overrideState;
                _vignetteHomeIntensityOverride = _silenceVignette.intensity.overrideState;
                _vignetteHomeSmoothnessOverride = _silenceVignette.smoothness.overrideState;
            }

            weight = Mathf.Clamp01(weight);
            if (weight > 0.0001f)
            {
                // From an inactive home the vignette starts at zero strength, so the fade grows the
                // edge in rather than snapping the authored (purple) vignette on first.
                float homeIntensity = _vignetteHomeActive ? _vignetteHomeIntensity : 0f;
                Color homeColor = _vignetteHomeActive ? _vignetteHomeColor : color;
                _silenceVignette.active = true;
                _silenceVignette.color.Override(Color.Lerp(homeColor, color, weight));
                _silenceVignette.intensity.Override(Mathf.Lerp(homeIntensity, intensity, weight));
                _silenceVignette.smoothness.Override(Mathf.Lerp(_vignetteHomeSmoothness, smoothness, weight));
            }
            else
            {
                _silenceVignette.color.value = _vignetteHomeColor;
                _silenceVignette.intensity.value = _vignetteHomeIntensity;
                _silenceVignette.smoothness.value = _vignetteHomeSmoothness;
                _silenceVignette.color.overrideState = _vignetteHomeColorOverride;
                _silenceVignette.intensity.overrideState = _vignetteHomeIntensityOverride;
                _silenceVignette.smoothness.overrideState = _vignetteHomeSmoothnessOverride;
                _silenceVignette.active = _vignetteHomeActive;
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
