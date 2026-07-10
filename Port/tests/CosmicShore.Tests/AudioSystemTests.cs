using System;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.Audio;
using CosmicShore.Engine.Audio.Fmod;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// AudioSystem unit (2026-07-10) — the REAL FMOD-backed AudioSystem (replacing
// the V6 type-preserving shell). Covers the local lanes the port made live:
// Start pulls GameSetting state and drives the FMOD SFX bus (volume = raw
// slider, mute = !enabled) with live re-application through GameSetting's
// static setter events; the one-shot volume contract (bus resolved → 1 so the
// bus applies the slider once; bus UNresolved → per-instance slider fallback;
// muted → ZERO instances created); the per-category volume scale (BlockDestroy
// 0.35 / Explosion 0.6 on top); the BlockDestroy sliding-window throttle (max
// 4 starts per 0.1s, window slides on unscaled time); spatialized one-shots
// carrying the impact position; the unwired-category warn lane staying silent
// (no instance, no throw); the legacy music routing (level/5 volume law,
// crossfade source flip, StopAllSongs) + mixer writes; the legacy PlaySFXClip
// one-shot; and the duplicate-instance guard.
// ─────────────────────────────────────────────────────────────────────────────

public class AudioSystemTests : IDisposable
{
    readonly GameLoop loop;
    readonly GameSetting gameSetting;
    readonly AudioSystem audio;
    readonly AudioMixer mixer = new();
    readonly AudioSource sfxSource;
    readonly AudioSource music1;
    readonly AudioSource music2;

    static readonly string[] PrefKeys = { "MusicEnabled", "SFXEnabled", "MusicLevel", "SFXLevel" };

    public AudioSystemTests()
    {
        loop = new GameLoop(nameof(AudioSystemTests));
        ResetStatics();

        // Dormant cloud service (inactive GO, Awake never runs): GameSetting.Awake
        // reads _ugsDataService.IsInitialized — false, the not-yet-signed-in boot.
        var ugsGo = new GameObject("UGSDataService(dormant)");
        ugsGo.SetActive(false);
        var ugs = ugsGo.AddComponent<CosmicShore.Core.UGSDataService>();

        var gameSettingGo = new GameObject("GameSetting");
        gameSettingGo.SetActive(false);
        gameSetting = gameSettingGo.AddComponent<GameSetting>();
        Set(gameSetting, "_ugsDataService", ugs);
        gameSettingGo.SetActive(true);

        var audioGo = new GameObject("AudioSystem");
        audioGo.SetActive(false);
        audio = audioGo.AddComponent<AudioSystem>();
        Set(audio, "gameSetting", gameSetting);
        Set(audio, "masterMixer", mixer);
        Set(audio, "sfxSource", sfxSource = audioGo.AddComponent<AudioSource>());
        Set(audio, "musicSource1", music1 = audioGo.AddComponent<AudioSource>());
        Set(audio, "musicSource2", music2 = audioGo.AddComponent<AudioSource>());
        audioGo.SetActive(true); // Awake: Instance
        loop.Tick(1f / 60f);     // Start: setting pull + bus resolve coroutine
    }

    public void Dispose()
    {
        ResetStatics();
        loop.Dispose();
    }

    static void ResetStatics()
    {
        RuntimeManager.ResetForTests();
        NullStaticAutoProp(typeof(AudioSystem), "Instance");
        NullStaticAutoProp(typeof(SingletonPersistent<GameSetting>), "Instance");
        // GameSetting persists its levels through file-backed PlayerPrefs; drop
        // the audio keys so Awake refills defaults regardless of prior runs.
        foreach (var key in PrefKeys) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    static void NullStaticAutoProp(Type t, string name)
        => t.GetField($"<{name}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, null);

    static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    static EventReference Wire(AudioSystem target, string field, string path)
    {
        var reference = new EventReference { Path = path };
        Set(target, field, reference);
        // The category dictionaries snapshot the serialized fields; rebuild so
        // the newly wired reference is visible (same lazy-init entry points the
        // class itself uses).
        typeof(AudioSystem).GetMethod("InitializeMenuAudioEvents", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, null);
        typeof(AudioSystem).GetMethod("InitializeGameplaySFXEvents", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, null);
        return reference;
    }

    Bus SfxBus() => RuntimeManager.GetBus("bus:/");

    [Fact]
    public void Start_DrivesSfxBusFromGameSetting_AndKeepsFollowingTheSlider()
    {
        // Fresh defaults: enabled, level 1 → bus volume 1, unmuted.
        SfxBus().getVolume(out float volume);
        SfxBus().getMute(out bool mute);
        Assert.Equal(1f, volume);
        Assert.False(mute);

        gameSetting.SetSFXLevel(0.4f);          // static event → ApplySfxBus
        SfxBus().getVolume(out volume);
        Assert.Equal(0.4f, volume);

        gameSetting.ChangeSFXEnabledSetting();  // toggle off → bus mutes
        SfxBus().getMute(out mute);
        Assert.True(mute);
        Assert.False(audio.SFXEnabled);

        gameSetting.ChangeSFXEnabledSetting();  // back on
        SfxBus().getMute(out mute);
        Assert.False(mute);
    }

    [Fact]
    public void OneShot_PassesFullVolume_WhenBusCarriesTheSlider()
    {
        Wire(audio, "optionClickEvent", "event:/UI/OptionClick");
        gameSetting.SetSFXLevel(0.4f);

        audio.PlayMenuAudio(MenuAudioCategory.OptionClick);

        var started = Assert.Single(RuntimeManager.StartedInstances);
        Assert.Equal("event:/UI/OptionClick", started.Path);
        // Bus applies the 0.4 slider globally; folding it in per-instance too
        // would attenuate by slider² — the one-shot must pass through at 1.
        Assert.Equal(1f, started.Volume);
        Assert.Equal(Vector3.zero, started.Position);
    }

    [Fact]
    public void OneShot_FallsBackToPerInstanceSlider_WhenBusNeverResolves()
    {
        // A world whose banks never load: GetBus throws, Start's retry loop
        // runs dry, and the per-instance fallback carries the slider instead.
        RuntimeManager.ResetForTests();
        RuntimeManager.FailBusResolution = true;
        NullStaticAutoProp(typeof(AudioSystem), "Instance");

        var go = new GameObject("AudioSystem-nobus");
        go.SetActive(false);
        var noBus = go.AddComponent<AudioSystem>();
        Set(noBus, "gameSetting", gameSetting);
        Set(noBus, "masterMixer", new AudioMixer());
        go.SetActive(true);
        loop.Tick(1f / 60f);

        gameSetting.SetSFXLevel(0.4f);
        Wire(noBus, "explosionEvent", "event:/SFX/Explosion");
        noBus.PlayGameplaySFX(GameplaySFXCategory.Explosion);

        var started = Assert.Single(RuntimeManager.StartedInstances);
        Assert.Equal(0.4f * 0.6f, started.Volume, 5); // slider fallback × Explosion category scale
    }

    [Fact]
    public void OneShot_Muted_CreatesZeroInstances()
    {
        Wire(audio, "crystalCollectEvent", "event:/SFX/CrystalCollect");
        gameSetting.ChangeSFXEnabledSetting(); // mute

        audio.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect);
        audio.PlayMenuAudio(MenuAudioCategory.OptionClick);

        Assert.Empty(RuntimeManager.StartedInstances); // volume 0 short-circuits pre-create
    }

    [Fact]
    public void BlockDestroy_AttenuatesAndThrottles_WindowSlidesOnUnscaledTime()
    {
        Wire(audio, "blockDestroyEvent", "event:/SFX/BlockDestroy");

        for (int i = 0; i < 7; i++)
            audio.PlayGameplaySFX(GameplaySFXCategory.BlockDestroy);

        // Only the first blockDestroyMaxPerWindow (4) voices start in one window.
        Assert.Equal(4, RuntimeManager.StartedInstances.Count);
        Assert.All(RuntimeManager.StartedInstances, s => Assert.Equal(0.35f, s.Volume, 5));

        loop.Tick(0.2f); // > blockDestroyThrottleWindow (0.1s) → window resets
        audio.PlayGameplaySFX(GameplaySFXCategory.BlockDestroy);
        Assert.Equal(5, RuntimeManager.StartedInstances.Count);
    }

    [Fact]
    public void SpatialOneShot_CarriesTheImpactPosition()
    {
        Wire(audio, "trackImpactEvent", "event:/SFX/TrackImpact");
        var impact = new Vector3(10f, -4f, 25f);

        audio.PlayGameplaySFX(GameplaySFXCategory.TrackImpact, impact);

        Assert.Equal(impact, Assert.Single(RuntimeManager.StartedInstances).Position);
    }

    [Fact]
    public void UnwiredCategory_IsSilent_NoInstanceNoThrow()
    {
        audio.PlayGameplaySFX(GameplaySFXCategory.GunFire);
        audio.PlayGameplaySFX(GameplaySFXCategory.GunFire); // warn-once path re-entered
        audio.PlayMenuAudio(MenuAudioCategory.Denied);

        Assert.Empty(RuntimeManager.StartedInstances);
    }

    [Fact]
    public void MusicLevel_ScalesByFive_AndCrossfadeFlipsTheActiveSource()
    {
        gameSetting.SetMusicLevel(1f);
        Assert.Equal(0.2f, music1.volume, 5); // level / 5 — "default max volume is too high"
        Assert.Equal(0.2f, music2.volume, 5);

        var clipA = new AudioClip { name = "songA" };
        var clipB = new AudioClip { name = "songB" };

        audio.PlayMusicClip(clipA);
        Assert.True(music1.isPlaying);
        Assert.Same(clipA, music1.clip);
        Assert.True(audio.IsMusicSourcePlaying());

        audio.PlayMusicClipWithCrossFade(clipB, transitionTime: 0.1f);
        Assert.True(music2.isPlaying);          // crossfade target starts immediately
        Assert.Same(clipB, music2.clip);
        loop.Tick(0.2f);                        // fade elapses → original stops
        loop.Tick(0.2f);
        Assert.False(music1.isPlaying);

        audio.StopAllSongs();
        Assert.False(audio.IsMusicSourcePlaying());
    }

    [Fact]
    public void MusicEnabledToggle_WritesTheMixer()
    {
        gameSetting.SetMusicLevel(1f); // musicVolume = 0.2

        gameSetting.ChangeMusicEnabledSetting(); // off → mixer music volume 0
        Assert.True(mixer.GetFloat("MusicVolume", out float value));
        Assert.Equal(0f, value);
        Assert.False(audio.MusicEnabled);

        gameSetting.ChangeMusicEnabledSetting(); // on → restored to musicVolume
        mixer.GetFloat("MusicVolume", out value);
        Assert.Equal(0.2f, value, 5);
    }

    [Fact]
    public void LegacyPlaySFXClip_AppliesTheScaledSliderToTheSharedSource()
    {
        gameSetting.SetSFXLevel(1f); // sfxVolume = 0.2 (legacy /5 law)
        var beep = new AudioClip { name = "beep" };

        audio.PlaySFXClip(beep);

        Assert.Same(beep, sfxSource.LastOneShotClip);
        Assert.Equal(1, sfxSource.OneShotCount);
        Assert.Equal(0.2f, sfxSource.volume, 5);
    }

    [Fact]
    public void DuplicateAudioSystem_DestroysItself_InstanceKeepsTheFirst()
    {
        var second = new GameObject("AudioSystem-dup").AddComponent<AudioSystem>();
        loop.Tick(1f / 60f);

        Assert.Same(audio, AudioSystem.Instance);
        Assert.True(second == null); // engine fake-null: the duplicate was destroyed
    }
}
