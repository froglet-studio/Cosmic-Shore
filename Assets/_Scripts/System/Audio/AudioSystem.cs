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
///      <see cref="PlaySFXClip(AudioClip)"/> API, which as of 12 Sep 2026 has
///      <b>ZERO callers</b> - <c>CountdownTimer</c> and <c>ProfileModal</c> were the
///      last two and moved to FMOD with the NiceVibrations demo-audio removal.
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
/// and wire the corresponding <c>EventReference</c> on the caller. <b>That is now
/// DONE for SFX</b>, so this path's own stated precondition for deletion is met:
/// <see cref="PlaySFXClip"/>, <see cref="sfxSource"/> and the
/// <see cref="masterMixer"/> bus they route through are dead for SFX and can go.
/// They are deliberately NOT deleted here - the MUSIC AudioSources are a separate
/// live consumer of the same legacy tier, so the removal is its own scoped change
/// rather than a side effect of migrating two UI sounds
/// (<c>Docs/AudioSystem/FMOD_AUDIT.md</c> section 4).
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

    [DefaultExecutionOrder(-1)]
    public class AudioSystem : MonoBehaviour, SoundtrackPlayer.ILegacyMusic
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

        [SerializeField, Tooltip(
            "The soundtrack the Audio Studio lists (Docs/MENU_REDESIGN_B.md §56). While none of its tracks has its own " +
            "event, the music event above keeps playing at random; once any has, the player's ticked tracks play instead.")]
        CosmicShore.ScriptableObjects.SoundtrackSO soundtrack;

        /// <summary>The player's music: rotation, shuffle and transport (the Audio Studio and now-playing card).</summary>
        public SoundtrackPlayer Soundtrack { get; private set; }

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
        [SerializeField, Tooltip("Played for GameplaySFXCategory.TrackImpact - a vessel ran into the SkimRace track (an indestructible, environment-owned prism) rather than a destructible player trail. Spatialized at the impact position.")]
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

        // Held gameplay SFX: owned instances for LOOPING / sustaining category events (Boost
        // Activate), one per (owner, category). See StartHeldGameplaySFX. A plain list - there are
        // a handful at most (one per locally piloted boosting vessel) and it must not allocate.
        struct HeldSfx
        {
            public GameplaySFXCategory Category;
            public UnityEngine.Object Owner;
            public int OwnerId;
            public GameObject AttachTo;
            public FMOD.Studio.EventInstance Instance;
        }
        readonly List<HeldSfx> _heldSfx = new();

        // Owned FMOD music instance (see class summary).
        FMOD.Studio.EventInstance _musicInstance;
        bool _musicStarted;

        // Which song the random music event started (the owner, 2026-10-19: "also show song names"). FMOD raises
        // SOUND_PLAYED on its own thread with the sound it picked; the callback only records the name, and Update
        // hands it to the soundtrack on the main thread. The delegate is a static field so the GC never collects it
        // while FMOD holds it, and the handle check drops a late callback from an instance being faded out.
        static readonly FMOD.Studio.EVENT_CALLBACK s_musicCallback = OnMusicEvent;
        static readonly object s_heardLock = new();
        static IntPtr s_musicHandle;
        static string s_heardSound;

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
            Soundtrack = new SoundtrackPlayer(soundtrack, this, this);
            if (Soundtrack.CanPickTracks) Soundtrack.Begin();
            else StartMusic();
        }

        void Update()
        {
            string heard = null;
            lock (s_heardLock)
            {
                if (s_heardSound != null) { heard = s_heardSound; s_heardSound = null; }
            }
            if (heard != null) Soundtrack?.NoteLegacySound(heard);
            Soundtrack?.Tick();
            if (_heldSfx.Count > 0) SweepOrphanedHeldSfx();
        }

        [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
        static FMOD.RESULT OnMusicEvent(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instance, IntPtr parameters)
        {
            if (type != FMOD.Studio.EVENT_CALLBACK_TYPE.SOUND_PLAYED || instance != s_musicHandle) return FMOD.RESULT.OK;
            if (new FMOD.Sound(parameters).getName(out string name, 256) == FMOD.RESULT.OK)
                lock (s_heardLock) s_heardSound = name;
            return FMOD.RESULT.OK;
        }

        void OnDestroy()
        {
            // Quit-safe: FmodSafe skips the runtime once it is tearing down, so this can never
            // resurrect the RuntimeManager mid-quit (the classic FMOD-on-exit hang).
            s_musicHandle = IntPtr.Zero;
            FmodSafe.StopAndRelease(ref _musicInstance, _musicStarted, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _musicStarted = false;
            StopAllHeldGameplaySFX(FMOD.Studio.STOP_MODE.IMMEDIATE);
            Soundtrack?.Dispose();
            if (Instance == this) Instance = null;
        }

        // ---------- Music (FMOD) ----------

        void StartMusic()
        {
            if (_musicStarted || musicEvent.IsNull) return;
            if (!FmodSafe.TryCreateInstance(musicEvent, out _musicInstance, this)) return;

            ApplyMusicVolume();
            s_musicHandle = _musicInstance.handle;
            _musicInstance.setCallback(s_musicCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.SOUND_PLAYED);
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
            Soundtrack?.ApplyVolume();
            if (!_musicInstance.isValid()) return;
            _musicInstance.setVolume(MusicInstanceVolume);
        }

        // ---------- The legacy random music event, driven by the soundtrack's transport ----------

        public float MusicInstanceVolume => AudioVolumeMath.InstanceVolume(
            musicEnabled, gameSetting != null ? gameSetting.MusicLevel : 1f, musicBaseVolumeMultiplier, VcaDrivesMusic);

        public void SetLegacyMusicPaused(bool paused)
        {
            if (_musicInstance.isValid()) _musicInstance.setPaused(paused);
        }

        /// <summary>Starts the random music event again (it picks a track again): skip / back / resume in legacy mode.</summary>
        public void RestartLegacyMusic()
        {
            StopLegacyMusic();
            StartMusic();
        }

        public void StopLegacyMusic()
        {
            s_musicHandle = IntPtr.Zero;
            FmodSafe.StopAndRelease(ref _musicInstance, _musicStarted, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _musicStarted = false;
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
            ApplyHeldSfxVolumes();
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
            ApplyHeldSfxVolumes();
        }

        // ---------- Music API (Unity AudioSource - legacy) ----------

        public void PlayMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource1 : musicSource2);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = MusicVolume;
            activeAudioSource.Play();
            CSDebug.LogVerbose(CSLogChannel.Audio, $"[Audio] Music clip playing - {activeAudioSource.clip.name}");
        }

        public void PlayNextMusicClip(AudioClip audioClip)
        {
            AudioSource activeAudioSource = (firstMusicSourceIsPlaying ? musicSource2 : musicSource1);
            activeAudioSource.clip = audioClip;
            activeAudioSource.volume = MusicVolume;
            activeAudioSource.Play();
            CSDebug.LogVerbose(CSLogChannel.Audio, $"[Audio] Music clip playing - {activeAudioSource.clip.name}");
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
            CSDebug.LogVerbose(CSLogChannel.Audio, $"[Audio] Music clip playing - {activeAudioSource.clip.name}");

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
            CSDebug.LogVerbose(CSLogChannel.Audio, $"[Audio] Music clip playing - {newAudioSource.clip.name}");

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

        // ---------- Held gameplay SFX (owned instances) ----------
        //
        // For a category whose sound lasts as long as an ability does (Boost Activate: a looping,
        // Speed-driven boost bed authored with a crossfaded loop region). Such an event can never
        // go through PlayGameplaySFX: fire-and-forget start()+release() leaks one immortal voice
        // per call, so FMODOneShotVolumeHelper refuses it (logs an error, plays nothing). Here the
        // AudioSystem OWNS the instance instead - started on the ability's begin, stopped with a
        // fade-out and released on its end - which is the "own the instance" fix that error asks for.
        //
        // An event that is NOT looping is played as an ordinary attached one-shot and not tracked,
        // so the call sites are correct whichever way FMOD Studio authors the event.

        /// <summary>
        /// Starts the event wired for <paramref name="category"/> as a HELD sound owned by
        /// <paramref name="owner"/>, attached to <paramref name="attachTo"/>. Pair every call with
        /// <see cref="StopHeldGameplaySFX"/> on the ability's end. Re-starting a sound the same
        /// owner already holds replaces it (never two voices per owner). If the owner is destroyed
        /// or deactivated without stopping it, the next Update stops and releases it anyway.
        /// The caller decides WHO hears it (the boost sites gate on the local pilot).
        /// </summary>
        public void StartHeldGameplaySFX(GameplaySFXCategory category, UnityEngine.Object owner, GameObject attachTo)
        {
            if (owner == null) return;
            if (GameplaySFXEvents == null) InitializeGameplaySFXEvents();

            if (!GameplaySFXEvents.TryGetValue(category, out var reference) || reference.IsNull)
            {
                if (warnOnUnwiredCategory && _warnedGameplayCategories.Add(category))
                {
                    Debug.LogWarning(
                        $"[AudioSystem] No FMOD EventReference wired for GameplaySFXCategory.{category}. " +
                        $"Wire it on the AudioSystem GameObject. (This warning fires once per category.)");
                }
                return;
            }

            // Re-trigger: drop the previous voice before starting a new one (no double voices).
            StopHeldGameplaySFX(category, owner, FMOD.Studio.STOP_MODE.IMMEDIATE);

            float volume = ResolveFMODSFXVolume() * GetCategoryVolumeScale(category);
            if (volume <= 0f) return;   // muted: no instance, same contract as the one-shot path

            if (!FmodSafe.TryCreateInstance(reference, out FMOD.Studio.EventInstance instance, this)) return;

            if (FMODOneShotVolumeHelper.IsFireAndForgetSafe(reference, instance))
            {
                // Stops by itself: an ordinary one-shot, nothing to own.
                instance.setVolume(volume);
                if (attachTo != null) FmodSafe.Attach(instance, attachTo);
                instance.start();
                instance.release();
                return;
            }

            instance.setVolume(volume);
            if (attachTo != null)
            {
                instance.set3DAttributes(RuntimeUtils.To3DAttributes(attachTo.transform.position));
                FmodSafe.Attach(instance, attachTo);
            }

            var result = instance.start();
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogError($"[AudioSystem] held SFX '{reference}' ({category}) start() returned {result}.", this);
                FmodSafe.StopAndRelease(ref instance, false, FMOD.Studio.STOP_MODE.IMMEDIATE);
                return;
            }

            _heldSfx.Add(new HeldSfx
            {
                Category = category,
                Owner = owner,
                OwnerId = owner.GetInstanceID(),
                AttachTo = attachTo,
                Instance = instance,
            });
        }

        /// <summary>
        /// Stops (fading out by default) and releases the held sound <paramref name="owner"/> started
        /// for <paramref name="category"/>. A no-op when nothing is held, so end paths may call it
        /// unconditionally (and more than once).
        /// </summary>
        public void StopHeldGameplaySFX(GameplaySFXCategory category, UnityEngine.Object owner,
            FMOD.Studio.STOP_MODE stopMode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
        {
            if (_heldSfx.Count == 0) return;
            // Compare ids, not references: a destroyed owner still has its id.
            int ownerId = ReferenceEquals(owner, null) ? 0 : owner.GetInstanceID();
            for (int i = _heldSfx.Count - 1; i >= 0; i--)
            {
                var held = _heldSfx[i];
                if (held.Category != category || held.OwnerId != ownerId) continue;
                ReleaseHeldAt(i, stopMode);
            }
        }

        /// <summary>Stops and releases every held sound (scene teardown / AudioSystem destroy).</summary>
        public void StopAllHeldGameplaySFX(FMOD.Studio.STOP_MODE stopMode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
        {
            for (int i = _heldSfx.Count - 1; i >= 0; i--)
                ReleaseHeldAt(i, stopMode);
        }

        void ReleaseHeldAt(int index, FMOD.Studio.STOP_MODE stopMode)
        {
            var held = _heldSfx[index];
            _heldSfx.RemoveAt(index);
            FmodSafe.StopAndRelease(ref held.Instance, true, stopMode);
        }

        /// <summary>
        /// Safety net for an end path that never ran: an owner (or the object the sound rides on)
        /// that was destroyed or deactivated - a vessel despawned, a scene unloaded mid-boost - has
        /// its sound faded out and released here rather than looping forever.
        /// </summary>
        void SweepOrphanedHeldSfx()
        {
            for (int i = _heldSfx.Count - 1; i >= 0; i--)
            {
                var held = _heldSfx[i];
                bool ownerGone = held.Owner == null
                                 || (held.Owner is Behaviour b && !b.isActiveAndEnabled)
                                 || (held.Owner is Component c && !c.gameObject.activeInHierarchy);
                bool targetGone = !ReferenceEquals(held.AttachTo, null)
                                  && (held.AttachTo == null || !held.AttachTo.activeInHierarchy);
                if (ownerGone || targetGone) ReleaseHeldAt(i, FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }
        }

        /// <summary>SFX slider / mute changed: re-apply to every held sound (one-shots are too short to care).</summary>
        void ApplyHeldSfxVolumes()
        {
            if (_heldSfx.Count == 0) return;
            float baseVolume = ResolveFMODSFXVolume();
            for (int i = 0; i < _heldSfx.Count; i++)
            {
                var held = _heldSfx[i];
                if (held.Instance.isValid())
                    held.Instance.setVolume(baseVolume * GetCategoryVolumeScale(held.Category));
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
