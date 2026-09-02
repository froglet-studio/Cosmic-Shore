using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Gameplay.Audio;
using CosmicShore.Utility;
using FMODUnity;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Serialization;

/// <summary>
/// Central audio service. Two parallel pipelines:
///
///   1. **FMOD events** (preferred / new) - one-shot SFX wired in the
///      inspector as <see cref="EventReference"/> per
///      <see cref="MenuAudioCategory"/> and <see cref="GameplaySFXCategory"/>.
///      Routed through <see cref="FMODOneShotVolumeHelper"/> so each one-shot
///      respects <see cref="GameSetting.SFXLevel"/> / SFXEnabled.
///
///   2. **Unity AudioSource** (legacy) - music routing via two
///      <see cref="AudioSource"/>s (<see cref="MusicSource1"/> /
///      <see cref="MusicSource2"/>) with crossfade support, plus a single
///      shared <see cref="sfxSource"/> for the older
///      <see cref="PlaySFXClip(AudioClip)"/> API still used by
///      <c>CountdownTimer</c>, <c>ProfileModal</c>, and <c>Crystal</c>.
///      The mixer-bus volume (<see cref="masterMixer"/>) only affects this
///      path - FMOD events bypass it entirely.
///
/// Music is FMOD too: <see cref="musicEvent"/> (the looping <c>event:/Music/Music</c>) is an
/// instance THIS class owns - created once, kept for the session, stopped and released on
/// destroy - with its volume following the Music slider / toggle. It used to be fired by a
/// scene-added <c>AudioManager</c> through <c>RuntimeManager.PlayOneShot</c>, which for a looping
/// event is an immortal instance nothing can stop, mute or turn down (the Music slider had no
/// effect on it). The Unity <c>Jukebox</c> component is removed from the Bootstrap instance and
/// is legacy.
///
/// Volume: ONE mapping (<see cref="AudioVolumeMath"/>) and ONE resolver pair
/// (<see cref="ResolveSfxInstanceVolume"/> / <see cref="ResolveMusicInstanceVolume"/>) for every
/// FMOD instance the code creates. Today the slider is applied PER INSTANCE. The FMOD project
/// carries <c>vca:/SFX</c> and <c>vca:/Music</c>, but neither is assigned to a bus, so they control
/// nothing; once the audio project routes the SFX and Music buses through them, flip
/// <see cref="driveFmodVcas"/> and this class drives the VCAs while every per-instance resolver
/// collapses to its base trim - so the slider is never applied twice. Detail:
/// <c>Docs/AudioSystem/FMOD_AUDIT.md</c>.
///
/// Migration path: convert callers from <see cref="PlaySFXClip(AudioClip)"/>
/// to <see cref="PlaySFXEvent(EventReference)"/> (or one of its overloads)
/// and wire the corresponding <c>EventReference</c> on the caller. The Unity
/// AudioSource path can be deleted once all callers have migrated.
/// </summary>
namespace CosmicShore.Core
{
    [Serializable]
    public enum MenuAudioCategory
    {
        OptionClick = 1,
        OpenView = 2,
        SwitchView = 3,
        CloseView = 4,
        SmallReward = 5,
        BigReward = 6,
        Upgrade = 7,
        Denied = 8,
        Confirmed = 9,
        LetsGo = 10,
        SwitchScreen = 11,
        RedeemTicket = 12,
    }

    [Serializable]
    public enum GameplaySFXCategory
    {
        BlockDestroy = 1,
        ShieldActivate = 2,
        ShieldDeactivate = 3,
        MineExplode = 4,
        ProjectileLaunch = 5,
        CrystalCollect = 6,
        VesselImpact = 7,
        GameEnd = 8,
        ScoreReveal = 9,
        PauseOpen = 10,
        PauseClose = 11,
        GunFire = 12,
        BoostActivate = 13,
        Explosion = 14,
        CreatureDeath = 15,
        DriftStart = 16,
        DriftEnd = 17,
        EnergyGain = 18,
        SpeedBurst = 19,
        CrystalSkim = 20,
        JoustScored = 21,
        JoustReceived = 22,
        ElementChargeReceived = 23,
        ElementMassReceived = 24,
        ElementSpaceReceived = 25,
        ElementTimeReceived = 26,
        ComebackCharge = 27,
        ComebackMass = 28,
        ComebackSpace = 29,
        ComebackTime = 30,
        JoustBuffCharge = 31,
        JoustBuffMass = 32,
        JoustBuffSpace = 33,
        JoustBuffTime = 34,
        TrackImpact = 35,
        FloraCollision = 36,
        CreatureBlockHit = 37,
    }

    [DefaultExecutionOrder(-1)]
    public class AudioSystem : MonoBehaviour
    {
        public static AudioSystem Instance { get; private set; }

        #region Fields
        [Inject] GameSetting gameSetting;

        [Header("Music Routing (Unity AudioSource - legacy)")]
        [SerializeField, Tooltip(
            "Master AudioMixer driving the legacy music + SFX AudioSources. " +
            "FMOD events do NOT route through this mixer - their volume is " +
            "applied per-instance via FMODOneShotVolumeHelper.")]
        AudioMixer masterMixer;

        [SerializeField, Tooltip(
            "AudioSource used by the legacy PlaySFXClip(AudioClip) API. " +
            "FMOD-based PlayMenuAudio / PlayGameplaySFX / PlaySFXEvent do " +
            "not use this source.")]
        AudioSource sfxSource;

        [SerializeField] AudioSource musicSource1;
        [SerializeField] AudioSource musicSource2;
        [SerializeField] float musicVolume = .1f;
        [SerializeField] float sfxVolume = .1f;

        [Header("Music (FMOD) - owned looping instance")]
        [SerializeField, Tooltip(
            "The game's music event (looping). Created once on Start, kept for the whole session, " +
            "stopped + released on destroy. Volume follows the Music slider and Music toggle. " +
            "Leave empty for no music - never point it at a placeholder event.")]
        EventReference musicEvent;

        [SerializeField, Range(0f, 2f), Tooltip(
            "Trim applied to the music instance on top of the Music slider. 1 = no change.")]
        float musicBaseVolumeMultiplier = 1f;

        [Header("FMOD VCA routing (opt-in)")]
        [SerializeField, Tooltip(
            "When true, the Music / SFX sliders are written to the FMOD VCAs below and every " +
            "per-instance volume the code applies collapses to its base trim (the slider is never " +
            "applied twice). Turn on ONLY after the FMOD project assigns the Music bus to vca:/Music " +
            "and the SFX bus to vca:/SFX - today both VCAs exist but control no bus, so this would " +
            "silence nothing and the sliders would stop working. A VCA that cannot be found falls back " +
            "to per-instance volume and reports once.")]
        bool driveFmodVcas = false;

        [SerializeField, Tooltip("FMOD VCA path the SFX slider drives when driveFmodVcas is on.")]
        string sfxVcaPath = "vca:/SFX";

        [SerializeField, Tooltip("FMOD VCA path the Music slider drives when driveFmodVcas is on.")]
        string musicVcaPath = "vca:/Music";

        [Header("Menu Audio Events (FMOD) - wire in the inspector")]
        [SerializeField, Tooltip("Played for MenuAudioCategory.OptionClick.")]
        EventReference optionClickEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.OpenView.")]
        EventReference openViewEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.SwitchView.")]
        EventReference switchViewEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.CloseView.")]
        EventReference closeViewEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.SmallReward.")]
        EventReference smallRewardEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.BigReward.")]
        EventReference bigRewardEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.Upgrade.")]
        EventReference upgradeEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.Denied.")]
        EventReference deniedEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.Confirmed.")]
        EventReference confirmedEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.LetsGo.")]
        EventReference letsGoEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.SwitchScreen.")]
        EventReference switchScreenEvent;

        [SerializeField, Tooltip("Played for MenuAudioCategory.RedeemTicket.")]
        EventReference redeemTicketEvent;

        [Header("Gameplay SFX Events (FMOD) - wire in the inspector")]
        [SerializeField, Tooltip("Played for GameplaySFXCategory.BlockDestroy.")]
        EventReference blockDestroyEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ShieldActivate.")]
        EventReference shieldActivateEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ShieldDeactivate.")]
        EventReference shieldDeactivateEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.MineExplode.")]
        EventReference mineExplodeEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ProjectileLaunch.")]
        EventReference projectileLaunchEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.CrystalCollect.")]
        EventReference crystalCollectEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.VesselImpact.")]
        EventReference vesselImpactEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.GameEnd.")]
        EventReference gameEndEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ScoreReveal.")]
        EventReference scoreRevealEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.PauseOpen.")]
        EventReference pauseOpenEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.PauseClose.")]
        EventReference pauseCloseEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.GunFire.")]
        EventReference gunFireEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.BoostActivate.")]
        EventReference boostActivateEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.Explosion.")]
        EventReference explosionEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.CreatureDeath.")]
        EventReference creatureDeathEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.DriftStart.")]
        EventReference driftStartEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.DriftEnd.")]
        EventReference driftEndEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.EnergyGain.")]
        EventReference energyGainEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.SpeedBurst.")]
        EventReference speedBurstEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.CrystalSkim.")]
        EventReference crystalSkimEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.JoustScored - local player's skimmer overtook an opponent.")]
        EventReference joustScoredEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.JoustReceived - local player was overtaken by an opponent's skimmer.")]
        EventReference joustReceivedEvent;

        [Header("Elemental Crystal Receive Events (FMOD) - wire in the inspector")]
        [SerializeField, Tooltip("Played for GameplaySFXCategory.ElementChargeReceived - local player collected a Charge crystal.")]
        EventReference elementChargeReceivedEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ElementMassReceived - local player collected a Mass crystal.")]
        EventReference elementMassReceivedEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ElementSpaceReceived - local player collected a Space crystal.")]
        EventReference elementSpaceReceivedEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ElementTimeReceived - local player collected a Time crystal.")]
        EventReference elementTimeReceivedEvent;

        [Header("Comeback Boost Events (FMOD) - wire in the inspector")]
        [SerializeField, Tooltip("Played for GameplaySFXCategory.ComebackCharge - local player receives a Charge comeback buff.")]
        EventReference comebackChargeEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ComebackMass - local player receives a Mass comeback buff.")]
        EventReference comebackMassEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ComebackSpace - local player receives a Space comeback buff.")]
        EventReference comebackSpaceEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.ComebackTime - local player receives a Time comeback buff.")]
        EventReference comebackTimeEvent;

        [Header("Joust Buff Events (FMOD) - wire in the inspector")]
        [SerializeField, Tooltip("Played for GameplaySFXCategory.JoustBuffCharge - local ally was overtaken (jousted) by a teammate's skimmer and buffed; Charge representative.")]
        EventReference joustBuffChargeEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.JoustBuffMass - local ally was overtaken (jousted) by a teammate's skimmer and buffed; Mass representative.")]
        EventReference joustBuffMassEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.JoustBuffSpace - local ally was overtaken (jousted) by a teammate's skimmer and buffed; Space representative.")]
        EventReference joustBuffSpaceEvent;

        [SerializeField, Tooltip("Played for GameplaySFXCategory.JoustBuffTime - local ally was overtaken (jousted) by a teammate's skimmer and buffed; Time representative.")]
        EventReference joustBuffTimeEvent;

        [Header("Track Impact Event (FMOD) - wire in the inspector")]
        [SerializeField, Tooltip("Played for GameplaySFXCategory.TrackImpact - a vessel ran into the HexRace track (an indestructible, environment-owned prism) rather than a destructible player trail. Spatialized at the impact position.")]
        EventReference trackImpactEvent;

        [Header("Flora Event (FMOD) - wire in the inspector")]
        [SerializeField, Tooltip("Played for GameplaySFXCategory.FloraCollision - a flora's health prism was destroyed (by any source). Replaces the generic BlockDestroy one-shot for flora prisms. Spatialized at the prism position.")]
        EventReference floraCollisionEvent;

        [Header("Creature Event (FMOD) - wire in the inspector")]
        [SerializeField, Tooltip("Played for GameplaySFXCategory.CreatureBlockHit - a creature (fauna) destroyed/consumed a non-flora block. Replaces the generic BlockDestroy one-shot for creature kills; flora blocks still play their own FloraCollision sound. Spatialized at the prism position.")]
        EventReference creatureBlockHitEvent;

        [Header("Gameplay SFX Tuning")]
        [SerializeField, Range(0f, 1f), Tooltip(
            "Extra volume multiplier applied to BlockDestroy one-shots on top " +
            "of the SFX slider. Dozens of prisms can break in a single frame " +
            "(mode entry, trail collapse, fauna swarms); attenuating the " +
            "per-block volume keeps the stacked result from phasing into a " +
            "harsh wall of noise.")]
        float blockDestroyVolumeScale = 0.35f;

        [SerializeField, Range(0f, 1f), Tooltip(
            "Extra volume multiplier applied to Explosion one-shots on top of " +
            "the SFX slider.")]
        float explosionVolumeScale = 0.6f;

        [SerializeField, Tooltip(
            "Categories that fire in BURSTS - one one-shot per prism destroyed - " +
            "and are therefore rate-limited, each category counted independently. " +
            "EVERY prism-destruction category belongs here: Prism.PlayDestructionSFX " +
            "plays one per prism from BOTH Explode and Implode, so a blast through a " +
            "forest or a creature grazing a canopy queues thousands of FMOD instances " +
            "in a single frame - which shows up as native time inside " +
            "RuntimeManager.Update(), not in the prism code that caused it.")]
        GameplaySFXCategory[] throttledCategories =
        {
            GameplaySFXCategory.BlockDestroy,
            GameplaySFXCategory.FloraCollision,
            GameplaySFXCategory.CreatureBlockHit,
        };

        [FormerlySerializedAs("blockDestroyMaxPerWindow")]
        [SerializeField, Min(1), Tooltip(
            "Max one-shots per throttled category allowed to start within " +
            "burstThrottleWindow seconds. Breaks beyond this cap in the " +
            "same window are dropped - the sound is already saturated, so the " +
            "extra voices are inaudible individually but would otherwise stack " +
            "into harsh noise and waste FMOD voices.")]
        int burstThrottleMaxPerWindow = 4;

        [FormerlySerializedAs("blockDestroyThrottleWindow")]
        [SerializeField, Min(0.01f), Tooltip(
            "Sliding window (seconds) over which burstThrottleMaxPerWindow is " +
            "counted, per throttled category.")]
        float burstThrottleWindow = 0.1f;

        [Header("Logging")]
        [SerializeField, Tooltip(
            "When true, log a warning the first time a category is played " +
            "without an EventReference wired. Useful during the AudioClip " +
            "→ FMOD migration; turn off once all slots are filled.")]
        bool warnOnUnwiredCategory = true;

        public AudioSource MusicSource1 { get => musicSource1; set => musicSource1 = value; }
        public AudioSource MusicSource2 { get => musicSource2; set => musicSource2 = value; }

        float SFXVolume { get { return sfxEnabled ? sfxVolume : 0; } }
        float MusicVolume { get { return musicEnabled ? musicVolume : 0; } }

        bool firstMusicSourceIsPlaying = true;
        bool musicEnabled = true;
        bool sfxEnabled = true;

        Dictionary<MenuAudioCategory, EventReference> MenuAudioEvents;
        Dictionary<GameplaySFXCategory, EventReference> GameplaySFXEvents;

        // Tracks per-category warnings so we don't spam the log every frame
        // when a category is fired with no event wired.
        readonly HashSet<MenuAudioCategory> _warnedMenuCategories = new();
        readonly HashSet<GameplaySFXCategory> _warnedGameplayCategories = new();

        // Sliding-window throttle state, one slot per entry in throttledCategories
        // (parallel arrays rather than a dictionary: this is consulted once per prism
        // destroyed, so it must not allocate or box an enum key). Allocated once, on
        // first use, by EnsureThrottleState.
        float[] _throttleWindowStart;
        int[] _throttleWindowCount;

        // Owned FMOD music instance (see class summary).
        FMOD.Studio.EventInstance _musicInstance;
        bool _musicStarted;

        // VCA routing state. The statics are what the per-instance resolvers read; they are true only
        // while driveFmodVcas is on AND the VCA actually resolved, so a missing VCA can never leave a
        // slider with no effect at all.
        FMOD.Studio.VCA _sfxVca;
        FMOD.Studio.VCA _musicVca;
        bool _vcaLookupDone;

        /// <summary>True while the SFX slider is applied on the FMOD SFX VCA rather than per instance.</summary>
        public static bool VcaDrivesSfx { get; private set; }

        /// <summary>True while the Music slider is applied on the FMOD Music VCA rather than per instance.</summary>
        public static bool VcaDrivesMusic { get; private set; }

        public bool MusicEnabled { get { return musicEnabled; } }
        public bool SFXEnabled { get { return sfxEnabled; } }
        #endregion

        // ---------- Shared volume resolvers (the only place settings become FMOD volume) ----------

        /// <summary>
        /// Linear volume to give a code-created SFX instance: 0 when SFX is muted, otherwise the SFX
        /// slider times <paramref name="baseMultiplier"/> - or the trim alone while the slider lives
        /// on the VCA. Every emitter (engine, drift, boost, flora bed, one-shots) routes through here.
        /// </summary>
        public static float ResolveSfxInstanceVolume(float baseMultiplier = 1f)
        {
            var gs = GameSetting.Instance;
            if (gs == null) return AudioVolumeMath.InstanceVolume(true, 1f, baseMultiplier, false);
            return AudioVolumeMath.InstanceVolume(gs.SFXEnabled, gs.SFXLevel, baseMultiplier, VcaDrivesSfx);
        }

        /// <summary>Music twin of <see cref="ResolveSfxInstanceVolume"/>.</summary>
        public static float ResolveMusicInstanceVolume(float baseMultiplier = 1f)
        {
            var gs = GameSetting.Instance;
            if (gs == null) return AudioVolumeMath.InstanceVolume(true, 1f, baseMultiplier, false);
            return AudioVolumeMath.InstanceVolume(gs.MusicEnabled, gs.MusicLevel, baseMultiplier, VcaDrivesMusic);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            InitializeMenuAudioEvents();
            InitializeGameplaySFXEvents();

            // Fallback: when auto-created by AppManager.EnsureService before the
            // Reflex container is built, [Inject] will not have resolved yet.
            gameSetting ??= FindFirstObjectByType<GameSetting>();

            if (gameSetting == null)
            {
                CSDebug.LogError("[AudioSystem] GameSetting not injected - ensure GameSetting is registered in the DI container.");
                return;
            }

            musicEnabled = gameSetting.MusicEnabled;
            sfxEnabled = gameSetting.SFXEnabled;
            ChangeMusicLevel(gameSetting.MusicLevel);
            ChangeSFXLevel(gameSetting.SFXLevel);
            ChangeMusicEnabledStatus(musicEnabled);

            ApplyVcaVolumes();
            StartMusic();
        }

        void OnDestroy()
        {
            // Quit-safe: FmodSafe skips the runtime once it is tearing down, so this can never
            // resurrect the RuntimeManager mid-quit (the classic FMOD-on-exit hang).
            FmodSafe.StopAndRelease(ref _musicInstance, _musicStarted, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _musicStarted = false;
            if (Instance == this) Instance = null;
        }

        // ---------- Music (FMOD) ----------

        void StartMusic()
        {
            if (_musicStarted || musicEvent.IsNull) return;
            if (!FmodSafe.TryCreateInstance(musicEvent, out _musicInstance, this)) return;

            ApplyMusicVolume();
            var result = _musicInstance.start();
            _musicStarted = result == FMOD.RESULT.OK;
            if (!_musicStarted)
            {
                Debug.LogError($"[AudioSystem] music event '{musicEvent}' start() returned {result}.", this);
                FmodSafe.StopAndRelease(ref _musicInstance, false, FMOD.Studio.STOP_MODE.IMMEDIATE);
            }
        }

        void ApplyMusicVolume()
        {
            if (!_musicInstance.isValid()) return;
            _musicInstance.setVolume(AudioVolumeMath.InstanceVolume(
                musicEnabled, gameSetting != null ? gameSetting.MusicLevel : 1f,
                musicBaseVolumeMultiplier, VcaDrivesMusic));
        }

        // ---------- VCA routing (opt-in, see driveFmodVcas) ----------

        void ApplyVcaVolumes()
        {
            if (!driveFmodVcas)
            {
                VcaDrivesSfx = false;
                VcaDrivesMusic = false;
                return;
            }

            if (!_vcaLookupDone)
            {
                _vcaLookupDone = true;
                VcaDrivesSfx = TryResolveVca(sfxVcaPath, out _sfxVca);
                VcaDrivesMusic = TryResolveVca(musicVcaPath, out _musicVca);
            }

            var gs = gameSetting != null ? gameSetting : GameSetting.Instance;
            float sfxLevel = gs != null ? gs.SFXLevel : 1f;
            float musicLevel = gs != null ? gs.MusicLevel : 1f;

            if (VcaDrivesSfx) _sfxVca.setVolume(AudioVolumeMath.VcaVolume(sfxEnabled, sfxLevel));
            if (VcaDrivesMusic) _musicVca.setVolume(AudioVolumeMath.VcaVolume(musicEnabled, musicLevel));
        }

        bool TryResolveVca(string path, out FMOD.Studio.VCA vca)
        {
            vca = default;
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                vca = RuntimeManager.GetVCA(path);
                return vca.isValid();
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[AudioSystem] driveFmodVcas is on but '{path}' could not be resolved ({ex.Message}). " +
                    "Falling back to per-instance volume for that channel. Author the VCA in FMOD Studio " +
                    "and rebuild banks, or turn driveFmodVcas off.", this);
                return false;
            }
        }

        void OnEnable()
        {
            GameSetting.OnChangeMusicEnabledStatus += ChangeMusicEnabledStatus;
            GameSetting.OnChangeSFXEnabledStatus += ChangeSFXEnabledStatus;
            GameSetting.OnChangeMusicLevel += ChangeMusicLevel;
            GameSetting.OnChangeSFXLevel += ChangeSFXLevel;
        }

        void OnDisable()
        {
            GameSetting.OnChangeMusicEnabledStatus -= ChangeMusicEnabledStatus;
            GameSetting.OnChangeSFXEnabledStatus -= ChangeSFXEnabledStatus;
            GameSetting.OnChangeMusicLevel -= ChangeMusicLevel;
            GameSetting.OnChangeSFXLevel -= ChangeSFXLevel;
        }

        void ChangeMusicEnabledStatus(bool status)
        {
            musicEnabled = status;
            SetMixerMusicVolume(musicEnabled ? musicVolume : 0);
            ApplyVcaVolumes();
            ApplyMusicVolume();
        }

        void ChangeSFXEnabledStatus(bool status)
        {
            sfxEnabled = status;
            ApplyVcaVolumes();
        }

        void ChangeMusicLevel(float level)
        {
            musicVolume = level / 5f;   // max .2 -- default max volume is too high (legacy AudioSource path)
            if (musicSource1 != null) musicSource1.volume = musicVolume;
            if (musicSource2 != null) musicSource2.volume = musicVolume;
            ApplyVcaVolumes();
            ApplyMusicVolume();
        }

        void ChangeSFXLevel(float level)
        {
            sfxVolume = level / 5f;   // max .2 -- default max volume is too high (legacy AudioSource path)
            ApplyVcaVolumes();
        }

        // ---------- Music API (Unity AudioSource - legacy) ----------

        public void PlayMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = MusicVolume;
            activeAudioSource.Play();
            CSDebug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");
        }

        public void PlayNextMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource2 : musicSource1);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = MusicVolume;
            activeAudioSource.Play();
            CSDebug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");
        }

        public void PlayMusicClipWithFade(AudioClip audioClip, float transitionTime = 1.0f)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            StartCoroutine(UpdateMusicWithFade(activeAudioSource, audioClip, transitionTime));
        }

        IEnumerator UpdateMusicWithFade(AudioSource activeAudioSource, AudioClip newAudioClip, float transitionTime)
        {
            // Make sure source is active and playing
            if (!activeAudioSource.isPlaying)
                activeAudioSource.Play();

            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade out original clip masterVolume
                activeAudioSource.volume = SFXVolume * (1 - (t / transitionTime));
                yield return null;
            }

            activeAudioSource.Stop();
            activeAudioSource.clip = newAudioClip; // Change AudioClip
            activeAudioSource.Play();
            CSDebug.Log($"Playing New Music Clip: {activeAudioSource.clip.name}");

            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                // Fade in new clip masterVolume
                activeAudioSource.volume = SFXVolume * (t / transitionTime);
                yield return null;
            }
        }

        public void PlayMusicClipWithCrossFade(AudioClip newAudioClip, float transitionTime = 1.0f)
        {
            //Determine the active audio source
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            AudioSource newAudioSource = (firstMusicSourceIsPlaying ? musicSource2 : musicSource1);

            //Switch the bool
            firstMusicSourceIsPlaying = !firstMusicSourceIsPlaying;

            //Set the new audio source
            newAudioSource.clip = newAudioClip;
            newAudioSource.Play();
            CSDebug.Log($"Playing New Music Clip: {newAudioSource.clip.name}");

            //crossfade music
            StartCoroutine(UpdateMusicWithCrossFade(activeAudioSource, newAudioSource, transitionTime));
        }

        IEnumerator UpdateMusicWithCrossFade(AudioSource originalSource, AudioSource newSource, float transitionTime)
        {
            for (float t = 0; t < transitionTime; t += Time.deltaTime)
            {
                originalSource.volume = SFXVolume * (1 - (t / transitionTime));
                newSource.volume = SFXVolume * (t / transitionTime);
                yield return null;
            }

            originalSource.Stop();
        }

        public void StopAllSongs()
        {
            musicSource1.Stop();
            musicSource2.Stop();
        }

        public bool IsMusicSourcePlaying()
        {
            return musicSource1.isPlaying || musicSource2.isPlaying;
        }

        // ---------- SFX API (FMOD - preferred) ----------

        /// <summary>
        /// Plays the FMOD event wired for <paramref name="category"/>. Volume
        /// is applied per-instance from <see cref="GameSetting.SFXLevel"/> /
        /// SFXEnabled via <see cref="FMODOneShotVolumeHelper"/>.
        /// </summary>
        public void PlayMenuAudio(MenuAudioCategory category)
        {
            if (MenuAudioEvents == null) InitializeMenuAudioEvents();

            if (MenuAudioEvents.TryGetValue(category, out var reference) && !reference.IsNull)
            {
                PlaySFXEvent(reference);
                return;
            }

            if (warnOnUnwiredCategory && _warnedMenuCategories.Add(category))
            {
                Debug.LogWarning(
                    $"[AudioSystem] No FMOD EventReference wired for MenuAudioCategory.{category}. " +
                    $"Wire it on the AudioSystem GameObject. (This warning fires once per category.)");
            }
        }

        /// <summary>
        /// Plays the FMOD event wired for <paramref name="category"/>. Volume
        /// is applied per-instance from <see cref="GameSetting.SFXLevel"/> /
        /// SFXEnabled via <see cref="FMODOneShotVolumeHelper"/>.
        /// </summary>
        public void PlayGameplaySFX(GameplaySFXCategory category)
            => PlayGameplaySFXInternal(category, spatial: false, Vector3.zero);

        /// <summary>
        /// Plays the FMOD event wired for <paramref name="category"/> as a
        /// spatialized one-shot at <paramref name="worldPosition"/>. Use for
        /// in-world events (crystal explosions, skims) where the sound should
        /// emanate from the collision site rather than the listener origin.
        /// </summary>
        public void PlayGameplaySFX(GameplaySFXCategory category, Vector3 worldPosition)
            => PlayGameplaySFXInternal(category, spatial: true, worldPosition);

        /// <summary>
        /// Shared implementation for the gameplay SFX overloads. Applies the
        /// per-category volume scale (see <see cref="GetCategoryVolumeScale"/>)
        /// on top of the SFX slider and gates throttled categories
        /// (see <see cref="PassesGameplaySFXThrottle"/>) so a burst of
        /// simultaneous prism breaks can't stack into harsh noise.
        /// </summary>
        void PlayGameplaySFXInternal(GameplaySFXCategory category, bool spatial, Vector3 worldPosition)
        {
            if (GameplaySFXEvents == null) InitializeGameplaySFXEvents();

            if (GameplaySFXEvents.TryGetValue(category, out var reference) && !reference.IsNull)
            {
                if (!PassesGameplaySFXThrottle(category)) return;

                float volume = ResolveFMODSFXVolume() * GetCategoryVolumeScale(category);
                FMODOneShotVolumeHelper.PlaySFXOneShot(reference, spatial ? worldPosition : Vector3.zero, volume);
                return;
            }

            if (warnOnUnwiredCategory && _warnedGameplayCategories.Add(category))
            {
                Debug.LogWarning(
                    $"[AudioSystem] No FMOD EventReference wired for GameplaySFXCategory.{category}. " +
                    $"Wire it on the AudioSystem GameObject. (This warning fires once per category.)");
            }
        }

        /// <summary>
        /// Plays an arbitrary FMOD event as a 2D one-shot at world origin
        /// with the SFX slider volume applied. Use for UI / menu sounds
        /// authored as 2D events in FMOD Studio (no spatializer).
        /// </summary>
        public void PlaySFXEvent(EventReference reference)
        {
            FMODOneShotVolumeHelper.PlaySFXOneShot(reference, Vector3.zero, ResolveFMODSFXVolume());
        }

        /// <summary>
        /// Plays an FMOD event as a one-shot at <paramref name="worldPosition"/>
        /// with the SFX slider volume applied. Use for spatialized 3D events.
        /// </summary>
        public void PlaySFXEvent(EventReference reference, Vector3 worldPosition)
        {
            FMODOneShotVolumeHelper.PlaySFXOneShot(reference, worldPosition, ResolveFMODSFXVolume());
        }

        /// <summary>
        /// Plays an FMOD event attached to <paramref name="attachTo"/> with
        /// the SFX slider volume applied. The instance follows the GameObject
        /// for the duration of playback.
        /// </summary>
        public void PlaySFXEventAttached(EventReference reference, GameObject attachTo)
        {
            FMODOneShotVolumeHelper.PlaySFXOneShotAttached(reference, attachTo, ResolveFMODSFXVolume());
        }

        // ---------- SFX API (Unity AudioSource - legacy) ----------

        public void PlaySFXClip(AudioClip audioClip, AudioSource sfxSource)
        {
            sfxSource.volume = SFXVolume;
            sfxSource.PlayOneShot(audioClip);
        }

        public void PlaySFXClip(AudioClip audioClip)
        {
            sfxSource.volume = SFXVolume;
            sfxSource.PlayOneShot(audioClip);
        }

        // ---------- Mixer ----------

        #region Mixer Methods

        public void SetMixerMusicVolume(float value)
        {
            masterMixer.SetFloat("MusicVolume", value);
        }

        public void SetMixerSFXVolume(float value)
        {
            masterMixer.SetFloat("SFXVolume", value);
        }

        #endregion

        // ---------- Internals ----------

        /// <summary>
        /// Linear 0..1 volume for FMOD SFX one-shots. Returns 0 when SFX is
        /// muted via the settings panel; otherwise returns the slider value.
        /// Decoupled from <see cref="sfxVolume"/> (which uses a /5 scale for
        /// the legacy AudioSource path) - FMOD events are authored at
        /// project-appropriate levels in FMOD Studio so we pass the slider
        /// through unscaled.
        /// </summary>
        float ResolveFMODSFXVolume()
        {
            if (!sfxEnabled) return 0f;
            return ResolveSfxInstanceVolume(1f);
        }

        /// <summary>
        /// Per-category volume multiplier applied on top of the SFX slider.
        /// Categories that tend to fire en masse in a single frame (block
        /// destruction, explosions) are attenuated so the stacked result
        /// doesn't clip into harsh noise. Everything else passes through at 1.
        /// </summary>
        float GetCategoryVolumeScale(GameplaySFXCategory category) => category switch
        {
            GameplaySFXCategory.BlockDestroy => blockDestroyVolumeScale,
            GameplaySFXCategory.Explosion => explosionVolumeScale,
            _ => 1f,
        };

        /// <summary>
        /// Sliding-window concurrency gate for the categories that fire in large
        /// bursts (<see cref="throttledCategories"/>): at most
        /// <see cref="burstThrottleMaxPerWindow"/> voices may start per
        /// <see cref="burstThrottleWindow"/> seconds, counted INDEPENDENTLY per
        /// category so a burning forest cannot starve plain block destruction.
        /// Returns false to drop the one-shot; non-throttled categories always pass.
        ///
        /// <para>This used to gate BlockDestroy alone, which is the one prism
        /// destruction category the ecology does NOT use: HealthPrism overrides
        /// flora to FloraCollision and Prism.PlayDestructionSFX swaps a
        /// creature kill to CreatureBlockHit, so the food web and every AOE blast
        /// through a forest routed straight around the guard - queueing an
        /// unbounded number of CreateInstance/start commands that FMOD then paid
        /// for inside RuntimeManager.Update().</para>
        /// </summary>
        bool PassesGameplaySFXThrottle(GameplaySFXCategory category)
        {
            int slot = ThrottleSlotOf(category);
            if (slot < 0) return true;   // not a burst category - always passes

            EnsureThrottleState();

            float now = Time.unscaledTime;
            if (now - _throttleWindowStart[slot] > burstThrottleWindow)
            {
                _throttleWindowStart[slot] = now;
                _throttleWindowCount[slot] = 0;
            }

            if (_throttleWindowCount[slot] >= burstThrottleMaxPerWindow) return false;

            _throttleWindowCount[slot]++;
            return true;
        }

        /// <summary>
        /// Index of <paramref name="category"/> in <see cref="throttledCategories"/>, or -1
        /// when it is not throttled. A linear scan over a three-entry array, deliberately:
        /// it is allocation-free (an enum-keyed Dictionary boxes) and this runs once per
        /// prism destroyed.
        /// </summary>
        int ThrottleSlotOf(GameplaySFXCategory category)
        {
            if (throttledCategories == null) return -1;
            for (int i = 0; i < throttledCategories.Length; i++)
                if (throttledCategories[i] == category) return i;
            return -1;
        }

        void EnsureThrottleState()
        {
            int n = throttledCategories.Length;
            if (_throttleWindowStart != null && _throttleWindowStart.Length == n) return;
            _throttleWindowStart = new float[n];
            _throttleWindowCount = new int[n];
        }

        void InitializeMenuAudioEvents()
        {
            MenuAudioEvents = new Dictionary<MenuAudioCategory, EventReference>()
            {
                {MenuAudioCategory.OptionClick, optionClickEvent},
                {MenuAudioCategory.OpenView, openViewEvent},
                {MenuAudioCategory.SwitchView, switchViewEvent},
                {MenuAudioCategory.CloseView, closeViewEvent},
                {MenuAudioCategory.SmallReward, smallRewardEvent},
                {MenuAudioCategory.BigReward, bigRewardEvent},
                {MenuAudioCategory.Upgrade, upgradeEvent},
                {MenuAudioCategory.Denied, deniedEvent},
                {MenuAudioCategory.Confirmed, confirmedEvent},
                {MenuAudioCategory.LetsGo, letsGoEvent},
                {MenuAudioCategory.SwitchScreen, switchScreenEvent},
                {MenuAudioCategory.RedeemTicket, redeemTicketEvent},
            };
        }

        void InitializeGameplaySFXEvents()
        {
            GameplaySFXEvents = new Dictionary<GameplaySFXCategory, EventReference>()
            {
                {GameplaySFXCategory.BlockDestroy, blockDestroyEvent},
                {GameplaySFXCategory.ShieldActivate, shieldActivateEvent},
                {GameplaySFXCategory.ShieldDeactivate, shieldDeactivateEvent},
                {GameplaySFXCategory.MineExplode, mineExplodeEvent},
                {GameplaySFXCategory.ProjectileLaunch, projectileLaunchEvent},
                {GameplaySFXCategory.CrystalCollect, crystalCollectEvent},
                {GameplaySFXCategory.VesselImpact, vesselImpactEvent},
                {GameplaySFXCategory.GameEnd, gameEndEvent},
                {GameplaySFXCategory.ScoreReveal, scoreRevealEvent},
                {GameplaySFXCategory.PauseOpen, pauseOpenEvent},
                {GameplaySFXCategory.PauseClose, pauseCloseEvent},
                {GameplaySFXCategory.GunFire, gunFireEvent},
                {GameplaySFXCategory.BoostActivate, boostActivateEvent},
                {GameplaySFXCategory.Explosion, explosionEvent},
                {GameplaySFXCategory.CreatureDeath, creatureDeathEvent},
                {GameplaySFXCategory.DriftStart, driftStartEvent},
                {GameplaySFXCategory.DriftEnd, driftEndEvent},
                {GameplaySFXCategory.EnergyGain, energyGainEvent},
                {GameplaySFXCategory.SpeedBurst, speedBurstEvent},
                {GameplaySFXCategory.CrystalSkim, crystalSkimEvent},
                {GameplaySFXCategory.JoustScored, joustScoredEvent},
                {GameplaySFXCategory.JoustReceived, joustReceivedEvent},
                {GameplaySFXCategory.ElementChargeReceived, elementChargeReceivedEvent},
                {GameplaySFXCategory.ElementMassReceived, elementMassReceivedEvent},
                {GameplaySFXCategory.ElementSpaceReceived, elementSpaceReceivedEvent},
                {GameplaySFXCategory.ElementTimeReceived, elementTimeReceivedEvent},
                {GameplaySFXCategory.ComebackCharge, comebackChargeEvent},
                {GameplaySFXCategory.ComebackMass, comebackMassEvent},
                {GameplaySFXCategory.ComebackSpace, comebackSpaceEvent},
                {GameplaySFXCategory.ComebackTime, comebackTimeEvent},
                {GameplaySFXCategory.JoustBuffCharge, joustBuffChargeEvent},
                {GameplaySFXCategory.JoustBuffMass, joustBuffMassEvent},
                {GameplaySFXCategory.JoustBuffSpace, joustBuffSpaceEvent},
                {GameplaySFXCategory.JoustBuffTime, joustBuffTimeEvent},
                {GameplaySFXCategory.TrackImpact, trackImpactEvent},
                {GameplaySFXCategory.FloraCollision, floraCollisionEvent},
                {GameplaySFXCategory.CreatureBlockHit, creatureBlockHitEvent},
            };
        }
    }
}
