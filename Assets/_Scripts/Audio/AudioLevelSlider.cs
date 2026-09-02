using CosmicShore.Core;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Binds a UI <see cref="Slider"/> to one of <see cref="GameSetting"/>'s audio levels (Music or
    /// SFX) so the slider READS the saved level whenever it is shown and WRITES through the one
    /// persisted setter when dragged.
    ///
    /// <para>This component replaced <c>Mixer</c>, which drove an FMOD VCA (<c>vca:/Music</c>,
    /// <c>vca:/SFX</c>) straight from the slider. Both VCAs exist in the FMOD project but are assigned
    /// to no bus, so that write changed nothing audible, and nothing persisted the value or restored
    /// the slider on the next launch - which is the "set the slider to 0, restart, it is back" report.
    /// The file keeps <c>Mixer</c>'s asset GUID, so every prefab that carried a Mixer now carries this
    /// with no re-wiring; the old <c>VCA</c> string ("Music"/"SFX") is read as the channel selector.</para>
    ///
    /// <para>The actual FMOD volume application lives in <see cref="AudioSystem"/> (per-instance today,
    /// VCA-driven once the FMOD project routes its buses through the VCAs - see
    /// <c>Docs/AudioSystem/FMOD_AUDIT.md</c>). A slider only ever talks to GameSetting.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioLevelSlider : MonoBehaviour
    {
        public enum Channel { Music, SFX }

        [FormerlySerializedAs("VCA")]
        [SerializeField, Tooltip("Which GameSetting level this slider edits: 'Music' or 'SFX' (legacy Mixer VCA name).")]
        string channelName = "SFX";

        [SerializeField, Tooltip("Slider to bind. Defaults to the Slider on this GameObject.")]
        Slider slider;

        bool _listening;

        Channel ResolvedChannel =>
            string.Equals(channelName, "Music", System.StringComparison.OrdinalIgnoreCase)
                ? Channel.Music
                : Channel.SFX;

        void Awake()
        {
            if (slider == null) slider = GetComponent<Slider>();
        }

        void OnEnable()
        {
            if (slider == null) return;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            RefreshFromSettings();

            if (!_listening)
            {
                slider.onValueChanged.AddListener(OnSliderChanged);
                _listening = true;
            }

            if (ResolvedChannel == Channel.Music) GameSetting.OnChangeMusicLevel += OnLevelChangedExternally;
            else GameSetting.OnChangeSFXLevel += OnLevelChangedExternally;
        }

        void OnDisable()
        {
            if (slider != null && _listening)
            {
                slider.onValueChanged.RemoveListener(OnSliderChanged);
                _listening = false;
            }

            GameSetting.OnChangeMusicLevel -= OnLevelChangedExternally;
            GameSetting.OnChangeSFXLevel -= OnLevelChangedExternally;
        }

        /// <summary>
        /// Persistent-listener entry point kept for prefabs whose slider still calls the old
        /// <c>Mixer.SetVolume</c>; identical to the code-bound path. GameSetting drops a repeat of the
        /// same value, so a slider wired both ways costs nothing extra.
        /// </summary>
        public void SetVolume(float volume) => Write(volume);

        void OnSliderChanged(float value) => Write(value);

        void Write(float value)
        {
            var gs = GameSetting.Instance;
            if (gs == null) return;

            if (ResolvedChannel == Channel.Music) gs.SetMusicLevel(value);
            else gs.SetSFXLevel(value);
        }

        void OnLevelChangedExternally(float level)
        {
            if (slider != null) slider.SetValueWithoutNotify(level);
        }

        void RefreshFromSettings()
        {
            var gs = GameSetting.Instance;
            if (gs == null) return;

            slider.SetValueWithoutNotify(ResolvedChannel == Channel.Music ? gs.MusicLevel : gs.SFXLevel);
        }
    }
}
