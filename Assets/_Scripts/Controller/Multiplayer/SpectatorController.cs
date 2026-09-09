using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The viewing machine's half of SPECTATE MODE. Created by
    /// <see cref="PartyInviteController.SpectateAsync"/> once the Netcode client is up, and
    /// alive (DontDestroyOnLoad) until the viewer is back in its own Menu_Main.
    ///
    /// <para>It owns exactly four things a spectator needs and a pilot already has elsewhere:
    /// <b>which vessel</b> is watched (any live pair from <c>gameData.Players</c>, the clicked
    /// pilot first), <b>which camera</b> draws it (the gameplay follow rig re-pointed at that
    /// vessel, or a slow orbiting DOLLY on the manual replay rig), the <b>overlay</b> (switch
    /// player, switch camera, leave), and the <b>exit</b> - the close button, Escape/Start, the
    /// watched match ending (<see cref="GameDataSO.OnMiniGameEnd"/>, which every peer receives),
    /// or arriving home by any other route (host loss bounces via
    /// <c>PartyInviteController.HandleHostLossAsync</c>; the controller notices Menu_Main load
    /// and retires).</para>
    ///
    /// <para>It has no Player, no vessel and no input controller, so none of the fleet's
    /// per-pilot bindings (<c>VesselController.Initialize</c> under <c>IsLocalPilot</c>) ever
    /// run here. The two platform laws that describe "the ship I am looking at" -
    /// <see cref="PrismOcclusionCorridor"/> (so mass does not hide the watched hull) and
    /// <see cref="VesselVisionShading"/> (so the watched hull is not banded like a rival) - are
    /// moved by hand onto the spectated vessel and off the previous one, exactly as
    /// <c>VesselController.ChangePlayer</c> moves them together. The speed tunnel is left
    /// unbound on purpose: a viewer's field of view must not lurch with somebody else's
    /// throttle, and the dolly rig is posed by hand.</para>
    ///
    /// Record: Docs/PartySystem/SPECTATOR.md.
    /// </summary>
    public sealed class SpectatorController : MonoBehaviour
    {
        public enum CameraMode { Player = 0, Dolly = 1 }

        // ── Dolly tuning ────────────────────────────────────────────────────
        // A slow broadcast orbit around the watched vessel, sized off that vessel's own
        // authored camera distance so a Serpent (250u) and an Urchin (6.7u) both read.
        const float DollyDegreesPerSecond  = 9f;
        const float DollyRadiusMultiplier  = 2.6f;
        const float DollyMinRadius         = 60f;
        const float DollyHeightFraction    = 0.35f;
        const float DollyPositionSharpness = 2.5f;
        const float DollyRotationSharpness = 4f;

        /// <summary>Longest the veil stays up after the first vessel binds while the local arena builds.</summary>
        const float ArenaBuildFadeCapSeconds = 20f;

        public static SpectatorController Instance { get; private set; }

        GameDataSO _gameData;
        SceneTransitionManager _fade;
        SceneNameListSO _sceneNames;
        PartyPlayerData _target;

        readonly List<IPlayer> _candidates = new();
        IPlayer _spectated;
        CameraMode _mode = CameraMode.Player;
        Transform _dollyRig;
        float _dollyAngle;
        float _dollyRadius = DollyMinRadius;
        bool _dollySnap;

        SpectatorOverlay _overlay;
        bool _watching;
        bool _leaving;
        UniTaskCompletionSource<bool> _watchingTcs;

        public bool IsWatching => _watching;
        public IPlayer Spectated => _spectated;
        public CameraMode Mode => _mode;

        // ── Lifecycle ───────────────────────────────────────────────────────

        /// <summary>
        /// Stand the controller up (or re-target the live one). Safe to call before the
        /// match's scene has synced - it binds when the first pair initialises.
        /// </summary>
        public static SpectatorController Begin(PartyPlayerData target, GameDataSO gameData,
                                                SceneTransitionManager fade, SceneNameListSO sceneNames)
        {
            if (Instance != null)
            {
                Instance._target = target;
                return Instance;
            }

            var go = new GameObject("SpectatorController");
            var controller = go.AddComponent<SpectatorController>();
            controller._gameData = gameData;
            controller._fade = fade;
            controller._sceneNames = sceneNames;
            controller._target = target;
            return controller;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            if (_gameData != null)
            {
                _gameData.OnPlayerPairInitialized.OnRaised += OnPairInitialized;
                _gameData.OnMiniGameEnd.OnRaised += OnMatchEnded;
            }
            TryBind();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_gameData != null)
            {
                _gameData.OnPlayerPairInitialized.OnRaised -= OnPairInitialized;
                _gameData.OnMiniGameEnd.OnRaised -= OnMatchEnded;
            }
            Detach();
            _watchingTcs?.TrySetResult(false);
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// The join flow's success gate: true once a vessel is bound and the veil is coming
        /// down, false on timeout or if the match ended / the controller retired first. The
        /// caller bounces to its own menu on false.
        /// </summary>
        public async UniTask<bool> WaitUntilWatchingAsync(float timeoutSeconds, CancellationToken ct)
        {
            if (_watching) return true;
            if (_leaving) return false;

            _watchingTcs ??= new UniTaskCompletionSource<bool>();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(System.TimeSpan.FromSeconds(Mathf.Max(1f, timeoutSeconds)));
            try
            {
                TryBind(); // re-check under the subscription; the pair may already be there
                if (_watching) return true;
                return await _watchingTcs.Task.AttachExternalCancellation(timeoutCts.Token);
            }
            catch (System.OperationCanceledException)
            {
                return false;
            }
        }

        // ── Binding ─────────────────────────────────────────────────────────

        void OnPairInitialized(ulong _) => TryBind();

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string menuScene = _sceneNames != null ? _sceneNames.MainMenuScene : "Menu_Main";
            if (scene.name == menuScene)
            {
                // Home by any route - our own leave, a bounce, host loss. The job is over.
                Detach();
                Destroy(gameObject);
                return;
            }

            // The watched match's scene (Netcode scene sync), or a replay reload of it.
            MuteGameplayHud();
            TryBind();
        }

        void RefreshCandidates()
        {
            _candidates.Clear();
            if (_gameData == null) return;
            foreach (var p in _gameData.Players)
            {
                if (!IsAlive(p)) continue;
                _candidates.Add(p);
            }
        }

        static bool IsAlive(IPlayer p)
        {
            if (p == null) return false;
            if (p is Object po && !po) return false;
            var v = p.Vessel;
            if (v == null) return false;
            if (v is Object vo && !vo) return false;
            return v.Transform != null;
        }

        void TryBind()
        {
            if (_leaving) return;
            RefreshCandidates();

            if (_spectated != null && IsAlive(_spectated) && _candidates.Contains(_spectated))
            {
                _overlay?.SetRoster(_candidates.IndexOf(_spectated), _candidates.Count);
                return;
            }

            var pick = PickInitial();
            if (pick == null)
            {
                // The vessel we were on is gone and nothing else is bound yet - keep the last
                // camera where it was and wait for the next pair.
                return;
            }

            SetSpectated(pick);
            if (!_watching) OnFirstWatchAsync().Forget();
        }

        IPlayer PickInitial()
        {
            if (_candidates.Count == 0) return null;

            // 1. The pilot whose row was clicked, by UGS id (Player.NetUgsPlayerId replicates).
            if (!string.IsNullOrEmpty(_target.PlayerId))
                foreach (var p in _candidates)
                    if (p.UgsPlayerId == _target.PlayerId) return p;

            // 2. By display name - an older peer that never wrote its UGS id.
            if (!string.IsNullOrEmpty(_target.DisplayName))
                foreach (var p in _candidates)
                    if (p.Name == _target.DisplayName) return p;

            // 3. Any human before any bot; 4. anyone.
            foreach (var p in _candidates)
                if (!p.IsInitializedAsAI) return p;
            return _candidates[0];
        }

        void SetSpectated(IPlayer player)
        {
            var previous = _spectated;
            _spectated = player;

            if (previous != null && !ReferenceEquals(previous, player))
                UnbindLaws(previous);
            BindLaws(player);

            _dollyRadius = ResolveDollyRadius(player);
            _dollySnap = true;
            ApplyCameraMode();

            _overlay?.SetSpectated(player.Name, ResolveDomainColor(player.Domain),
                _candidates.IndexOf(player), _candidates.Count);
        }

        async UniTaskVoid OnFirstWatchAsync()
        {
            _watching = true;
            MuteGameplayHud();
            EnsureOverlay();
            _overlay.SetSpectated(_spectated.Name, ResolveDomainColor(_spectated.Domain),
                _candidates.IndexOf(_spectated), _candidates.Count);
            _overlay.SetCameraMode(_mode);
            _watchingTcs?.TrySetResult(true);

            // Hold the veil while this machine's own arena is still blooming in, the way the
            // connecting panel does for a pilot - capped, so a slow build can never strand us.
            float deadline = Time.unscaledTime + ArenaBuildFadeCapSeconds;
            while (!_leaving && this && !PrismTrailBuilder.PollArenaReady() && Time.unscaledTime < deadline)
                await UniTask.Yield();
            if (_leaving || !this) return;

            if (_fade != null) _fade.FadeFromBlack().Forget();
            _overlay.Show();
        }

        // ── Player / camera switching (overlay + input) ─────────────────────

        public void CycleSpectated(int step)
        {
            if (_leaving) return;
            RefreshCandidates();
            if (_candidates.Count == 0) return;

            int index = _candidates.IndexOf(_spectated);
            if (index < 0) index = 0;
            index = (index + step + _candidates.Count) % _candidates.Count;
            if (ReferenceEquals(_candidates[index], _spectated)) return;
            SetSpectated(_candidates[index]);
        }

        public void ToggleCameraMode() =>
            SetCameraMode(_mode == CameraMode.Player ? CameraMode.Dolly : CameraMode.Player);

        public void SetCameraMode(CameraMode mode)
        {
            if (_leaving) return;
            _mode = mode;
            _dollySnap = true;
            ApplyCameraMode();
            _overlay?.SetCameraMode(_mode);
        }

        void Update()
        {
            if (!_watching || _leaving) return;

            // A vessel that despawned under us (its pilot left) - move on.
            if (_spectated != null && !IsAlive(_spectated))
            {
                TryBind();
                return;
            }

            if (OverviewGesture.RequestedThisFrame())
            {
                Leave("closed by the viewer");
                return;
            }

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool prev = (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame))
                     || (pad != null && (pad.dpad.left.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame));
            bool next = (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
                     || (pad != null && (pad.dpad.right.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame));
            bool cam  = (kb != null && (kb.cKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame))
                     || (pad != null && pad.buttonNorth.wasPressedThisFrame);

            if (prev) CycleSpectated(-1);
            else if (next) CycleSpectated(+1);
            if (cam) ToggleCameraMode();
        }

        // ── Cameras ─────────────────────────────────────────────────────────

        void ApplyCameraMode()
        {
            var cm = CameraManager.Instance;
            if (cm == null || !IsAlive(_spectated)) return;

            var vessel = _spectated.Vessel;
            var followTarget = vessel.VesselStatus?.CameraFollowTarget != null
                ? vessel.VesselStatus.CameraFollowTarget
                : vessel.Transform;

            if (_mode == CameraMode.Dolly)
            {
                // The follow target still moves with the watched vessel so a later
                // RestoreGameplayCamera lands on it; the rig itself is posed in LateUpdate.
                cm.PlayerFollowTarget = followTarget;
                if (_dollyRig == null) _dollyRig = cm.BeginManualReplayCamera();
                if (_dollyRig != null) return;

                // No end camera in this rig - the dolly is not available here.
                _mode = CameraMode.Player;
            }

            if (_dollyRig != null)
            {
                // RestoreGameplayCamera lifts the corridor / speed-tunnel holds the replay rig
                // took; the corridor target is ours again and the tunnel stays unbound.
                cm.RestoreGameplayCamera();
                _dollyRig = null;
            }

            cm.SetupGamePlayCameras(followTarget);
            cm.SnapPlayerCameraToTarget();
        }

        void LateUpdate()
        {
            if (_mode != CameraMode.Dolly || _dollyRig == null || _leaving) return;
            if (!IsAlive(_spectated)) return;

            var t = _spectated.Vessel.Transform;
            float dt = Time.unscaledDeltaTime;
            _dollyAngle = Mathf.Repeat(_dollyAngle + DollyDegreesPerSecond * dt, 360f);

            Vector3 center  = t.position;
            Vector3 orbit   = Quaternion.AngleAxis(_dollyAngle, Vector3.up) * Vector3.back * _dollyRadius;
            Vector3 desired = center + orbit + Vector3.up * (_dollyRadius * DollyHeightFraction);
            var look = Quaternion.LookRotation((center - desired).normalized, Vector3.up);

            if (_dollySnap)
            {
                _dollyRig.SetPositionAndRotation(desired, look);
                _dollySnap = false;
                return;
            }

            float pk = 1f - Mathf.Exp(-DollyPositionSharpness * dt);
            float rk = 1f - Mathf.Exp(-DollyRotationSharpness * dt);
            _dollyRig.position = Vector3.Lerp(_dollyRig.position, desired, pk);
            var aim = Quaternion.LookRotation((center - _dollyRig.position).normalized, Vector3.up);
            _dollyRig.rotation = Quaternion.Slerp(_dollyRig.rotation, aim, rk);
        }

        static float ResolveDollyRadius(IPlayer player)
        {
            float authored = 0f;
            var t = player?.Vessel?.Transform;
            if (t != null)
            {
                var customizer = t.GetComponent<VesselCameraCustomizer>();
                if (customizer != null && customizer.Settings != null)
                    authored = customizer.Settings.followOffset.magnitude;
            }
            return Mathf.Max(DollyMinRadius, authored * DollyRadiusMultiplier);
        }

        // ── Platform laws for "the ship I am looking at" ────────────────────

        static void BindLaws(IPlayer player)
        {
            var t = player?.Vessel?.Transform;
            if (t == null) return;
            PrismOcclusionCorridor.SetTarget(t);
            VesselVisionShading.SetLocalVessel(t);
        }

        static void UnbindLaws(IPlayer player)
        {
            var t = player?.Vessel?.Transform;
            if (t == null) return;
            PrismOcclusionCorridor.ClearTarget(t);
            VesselVisionShading.ClearLocalVessel(t);
        }

        Color ResolveDomainColor(CosmicShore.Data.Domains domain)
        {
            var colorSet = _gameData != null && _gameData.ThemeManagerData != null
                ? _gameData.ThemeManagerData.ColorSet
                : null;
            return colorSet != null ? colorSet.GetDomainSignalColor(domain) : Color.white;
        }

        // ── Gameplay HUD ────────────────────────────────────────────────────

        /// <summary>
        /// The scene's game canvas is a pilot's instrument: it waits on OnClientReady (never
        /// coming), polls Escape into a pause menu that would freeze THIS machine's time, and
        /// draws nothing a viewer should read through the overlay. Its Canvas is switched off
        /// and its HUD component disabled (which unsubscribes it) - never destroyed, because
        /// fifteen scenes fork that prefab and none of them should learn about spectators.
        /// </summary>
        static void MuteGameplayHud()
        {
            foreach (var hud in FindObjectsByType<MiniGameHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                hud.enabled = false;
                var canvas = hud.GetComponentInParent<Canvas>(true);
                if (canvas != null) canvas.enabled = false;
            }
            foreach (var gc in FindObjectsByType<GameCanvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var canvas = gc.GetComponent<Canvas>();
                if (canvas != null) canvas.enabled = false;
            }
        }

        // ── Overlay ─────────────────────────────────────────────────────────

        void EnsureOverlay()
        {
            if (_overlay != null) return;
            _overlay = SpectatorOverlay.Create(
                onPrevious:     () => CycleSpectated(-1),
                onNext:         () => CycleSpectated(+1),
                onToggleCamera: ToggleCameraMode,
                onLeave:        () => Leave("closed by the viewer"));
            _overlay.transform.SetParent(transform, false);
        }

        // ── Exit ────────────────────────────────────────────────────────────

        void OnMatchEnded() => Leave("the match ended");

        /// <summary>
        /// Leave the watched match and return to the viewer's own menu. Idempotent. Before the
        /// first vessel bound (the join flow still running), the flow's own gate is failed
        /// instead, so it bounces exactly once through its own recovery.
        /// </summary>
        public void Leave(string reason)
        {
            if (_leaving) return;
            _leaving = true;
            CSDebug.Log($"[SpectatorController] Leaving spectator mode - {reason}.");

            Detach();

            if (!_watching)
            {
                _watchingTcs?.TrySetResult(false);
                return;
            }

            LeaveAsync().Forget();
        }

        async UniTaskVoid LeaveAsync()
        {
            var pic = PartyInviteController.Instance;
            float deadline = Time.unscaledTime + 15f;
            while (pic != null && pic.IsTransitioning && Time.unscaledTime < deadline)
                await UniTask.Yield();

            SpectatorSession.EndLocal();
            if (pic != null)
                await pic.LeavePartyAndReturnToMenuAsync();
            else
                _gameData?.InvokeOnSessionEnded();
        }

        void Detach()
        {
            if (_overlay != null) _overlay.Hide();

            if (_spectated != null)
            {
                UnbindLaws(_spectated);
                _spectated = null;
            }

            var cm = CameraManager.Instance;
            if (_dollyRig != null && cm != null)
            {
                cm.RestoreGameplayCamera();
                _dollyRig = null;
            }
        }
    }
}
