#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Core;
using CosmicShore.Gameplay.Audio;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The two pure rules behind "the audio slider does not save" (Docs/AudioSystem/FMOD_AUDIT.md):
    ///
    /// 1. <see cref="GameSetting.ShouldApplyCloud"/> - which store wins on launch. Before the rule
    ///    existed the cloud snapshot was applied unconditionally, so a fresh default of 1.0 (or a
    ///    save that never reached the cloud) overwrote a slider the player had just dragged to 0.
    /// 2. <see cref="AudioVolumeMath"/> - the ONE mapping from settings to an FMOD instance volume,
    ///    including the VCA mode in which the slider must not be applied a second time.
    /// </summary>
    public class AudioSettingsPersistenceTests
    {
        // ───────────────── cloud vs local precedence ─────────────────

        [Test]
        public void Cloud_newer_than_local_is_applied()
        {
            Assert.IsTrue(GameSetting.ShouldApplyCloud(cloudTicks: 200, localTicks: 100, cloudHasPersistedData: true));
        }

        [Test]
        public void Cloud_older_than_local_is_not_applied()
        {
            // The launch-after-quit case: local was stamped at quit, cloud still holds the older save.
            Assert.IsFalse(GameSetting.ShouldApplyCloud(cloudTicks: 100, localTicks: 200, cloudHasPersistedData: true));
        }

        [Test]
        public void Fresh_default_never_overwrites_a_stamped_local()
        {
            // cloudTicks 0 + no persisted data == the new T() a missing key falls back to.
            Assert.IsFalse(GameSetting.ShouldApplyCloud(cloudTicks: 0, localTicks: 1, cloudHasPersistedData: false));
        }

        [Test]
        public void Fresh_default_never_overwrites_an_unstamped_local_either()
        {
            Assert.IsFalse(GameSetting.ShouldApplyCloud(cloudTicks: 0, localTicks: 0, cloudHasPersistedData: false));
        }

        [Test]
        public void Legacy_cloud_data_roams_onto_an_unstamped_install()
        {
            // A second device with nothing saved locally takes genuinely-saved cloud data even when
            // that data predates the stamp (ticks 0).
            Assert.IsTrue(GameSetting.ShouldApplyCloud(cloudTicks: 0, localTicks: 0, cloudHasPersistedData: true));
        }

        [Test]
        public void Legacy_cloud_data_does_not_overwrite_a_stamped_local()
        {
            Assert.IsFalse(GameSetting.ShouldApplyCloud(cloudTicks: 0, localTicks: 5, cloudHasPersistedData: true));
        }

        // ───────────────── settings -> FMOD volume ─────────────────

        [Test]
        public void Muted_channel_is_zero_in_both_modes()
        {
            Assert.AreEqual(0f, AudioVolumeMath.InstanceVolume(false, 1f, 1f, vcaDrivesLevel: false));
            Assert.AreEqual(0f, AudioVolumeMath.InstanceVolume(false, 1f, 1f, vcaDrivesLevel: true));
            Assert.AreEqual(0f, AudioVolumeMath.VcaVolume(false, 1f));
        }

        [Test]
        public void Slider_at_zero_is_zero_so_no_voice_is_created()
        {
            Assert.AreEqual(0f, AudioVolumeMath.InstanceVolume(true, 0f, 1.5f, vcaDrivesLevel: false));
            Assert.AreEqual(0f, AudioVolumeMath.InstanceVolume(true, 0f, 1.5f, vcaDrivesLevel: true));
        }

        [Test]
        public void Per_instance_mode_applies_slider_times_trim()
        {
            Assert.AreEqual(0.25f, AudioVolumeMath.InstanceVolume(true, 0.5f, 0.5f, vcaDrivesLevel: false), 1e-6f);
        }

        [Test]
        public void Vca_mode_applies_trim_only_so_the_slider_is_never_squared()
        {
            Assert.AreEqual(0.5f, AudioVolumeMath.InstanceVolume(true, 0.5f, 0.5f, vcaDrivesLevel: true), 1e-6f);
            Assert.AreEqual(0.5f, AudioVolumeMath.VcaVolume(true, 0.5f), 1e-6f);
        }

        [Test]
        public void Trim_is_clamped_to_the_documented_range()
        {
            Assert.AreEqual(AudioVolumeMath.MaxBaseMultiplier, AudioVolumeMath.InstanceVolume(true, 1f, 99f, false), 1e-6f);
            Assert.AreEqual(0f, AudioVolumeMath.InstanceVolume(true, 1f, -1f, false), 1e-6f);
        }
    }
}
#endif
