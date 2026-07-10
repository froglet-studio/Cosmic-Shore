using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.Audio;
using CosmicShore.Utility;

namespace CosmicShore.Cli
{
    /// <summary>
    /// Shared harness authoring for the scene's audio singleton (AudioSystem
    /// unit): every Unity scene carries the AppManager-registered AudioSystem
    /// with its GameSetting, master AudioMixer, and legacy AudioSources wired
    /// in the inspector — this rig transcribes that authoring for the round /
    /// sim / menushell worlds so the REAL AudioSystem.Start runs its settings
    /// pull cleanly (no "GameSetting not injected" error lane). No FMOD events
    /// exist in the port fixture yet, so the AudioClip→FMOD migration warn
    /// flag is off — the authored-scene posture once all slots are filled.
    ///
    /// Statics are cleared first (AudioSystem.Instance,
    /// SingletonPersistent&lt;GameSetting&gt;.Instance): worlds rebuild without the
    /// previous world's objects being destroyed, so the duplicate-instance
    /// guards would otherwise kill every rebuilt world's components.
    /// </summary>
    public static class AudioSystemRig
    {
        public static AudioSystem Create()
        {
            typeof(AudioSystem)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
                .SetValue(null, null);
            typeof(SingletonPersistent<GameSetting>)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
                .SetValue(null, null);

            // Dormant cloud service (GO stays inactive so Awake never runs):
            // GameSetting.Awake reads its [Inject] _ugsDataService.IsInitialized —
            // false here, exactly the not-yet-signed-in boot posture — and every
            // other cloud lane (SyncToCloud, OnDestroy) is null/repo-guarded.
            var ugsGo = new GameObject("UGSDataService(dormant)");
            ugsGo.SetActive(false);
            var ugs = ugsGo.AddComponent<UGSDataService>();

            var gameSettingGo = new GameObject("GameSetting");
            gameSettingGo.SetActive(false);
            var gameSetting = gameSettingGo.AddComponent<GameSetting>();
            SetOn(gameSetting, "_ugsDataService", ugs);
            gameSettingGo.SetActive(true);

            var audioGo = new GameObject("AudioSystem");
            audioGo.SetActive(false);
            var audioSystem = audioGo.AddComponent<AudioSystem>();
            Set(audioSystem, "gameSetting", gameSetting);
            Set(audioSystem, "masterMixer", new AudioMixer { name = "MasterMixer" });
            Set(audioSystem, "sfxSource", audioGo.AddComponent<AudioSource>());
            Set(audioSystem, "musicSource1", audioGo.AddComponent<AudioSource>());
            Set(audioSystem, "musicSource2", audioGo.AddComponent<AudioSource>());
            Set(audioSystem, "warnOnUnwiredCategory", false);
            audioGo.SetActive(true);
            return audioSystem;
        }

        static void Set(object target, string field, object value)
            => typeof(AudioSystem)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(target, value);

        static void SetOn(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f == null) continue;
                f.SetValue(target, value);
                return;
            }
            throw new System.MissingFieldException(target.GetType().Name, field);
        }
    }
}
