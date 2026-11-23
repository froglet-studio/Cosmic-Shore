using System;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.Core
{
    /// <summary>
    /// The boot's menu shown BEFORE the Relay host is up (Yash, 2026-11-21: the boot sat at 41% "Creating session").
    ///
    /// <para>B36 loads Menu_Main before the party session exists, and the session's create is 5-10 s of UGS calls.
    /// The splash used to stay up until the vessel spawned on the new host. Now the world is laid during those calls
    /// (<see cref="Cell"/>'s boot build), and as soon as it stands the menu is revealed with UI input held, so
    /// nothing can launch a game or open freestyle without a host. When the vessel is ready
    /// (<see cref="FadeFromSplashWhenSettled"/>) input comes back and the player is listed online. If no host comes
    /// within <see cref="bootMenuHostWaitSeconds"/>, the splash covers the menu again so the boot's retry and offline
    /// screens are seen, and the ordinary reveal brings the menu back once a host (Relay or offline) is up.</para>
    /// </summary>
    public partial class SceneLoader
    {
        [Header("Boot: the menu before the host")]
        [SerializeField, Tooltip("Reveal the boot menu once its world is laid, before the party session's Relay host " +
                                 "is up, with UI input held until the vessel is ready. Off: the splash waits for the vessel.")]
        bool revealBootMenuBeforeHost = true;

        [SerializeField, Min(1f), Tooltip("Seconds the early-revealed menu waits for the host before the splash " +
                                          "covers it again, so the boot's retry and offline screens can be seen.")]
        float bootMenuHostWaitSeconds = 12f;

        bool _bootMenuRevealedEarly;
        EventSystem _heldEventSystem;

        /// <summary>
        /// Whether the menu that just loaded is revealed before the host: the first boot into it (not a return
        /// from a game), nothing listening yet (the Relay host is still being created), no local player, and a
        /// boot world covering the screen whose release says the world stands.
        /// </summary>
        internal static bool RevealsBootMenuBeforeHost(bool enabled, bool menuReturn, bool networkListening,
                                                       bool localPlayerReady, bool worldCovering) =>
            enabled && !menuReturn && !networkListening && !localPlayerReady && worldCovering;

        static bool NetworkIsListening => NetworkManager.Singleton is { IsListening: true };

        async UniTaskVoid RevealBootMenuBeforeHostAsync(bool menuReturn)
        {
            // Cell.CoverBootWorld raises the world's cover in Start, after this scene's load callback.
            await UniTask.NextFrame(PlayerLoopTiming.Update);
            if (this == null || !gameData) return;
            if (!RevealsBootMenuBeforeHost(revealBootMenuBeforeHost, menuReturn, NetworkIsListening,
                    gameData.LocalPlayer != null, EnvironmentLoadVeil.IsCovering))
                return;

            float holdUntil = Time.unscaledTime + MaxBootWorldHoldSeconds;
            while (SceneRevealHold.IsHeld || EnvironmentLoadVeil.IsCovering && Time.unscaledTime < holdUntil)
                await UniTask.Yield(PlayerLoopTiming.Update);

            // A host that came up meanwhile spawns the vessel within a second or two: the ordinary reveal follows.
            if (this == null || !gameData || gameData.LocalPlayer != null || NetworkIsListening) return;
            if (_sceneTransitionManager == null || !_sceneTransitionManager.IsCovering) return;

            // The covered collect the ordinary reveal takes, taken now while the splash still covers the screen.
            GC.Collect();
            _collectedBeforeLoad = true;
            for (int i = 0; i < revealSettleFrames; i++)
                await UniTask.NextFrame(PlayerLoopTiming.Update);
            if (this == null || gameData.LocalPlayer != null) return;

            _bootMenuRevealedEarly = true;
            HoldMenuInput(true);
            if (LoadInsights.IsRecordingMenuLoad) LoadInsights.MarkVisualReady();
            CSDebug.LogVerbose(CSLogChannel.Boot, "[SceneLoader] Menu revealed before the host; UI input held until the vessel is ready.");
            await _sceneTransitionManager.RevealAsync();

            float giveUpAt = Time.unscaledTime + bootMenuHostWaitSeconds;
            while (_bootMenuRevealedEarly && Time.unscaledTime < giveUpAt)
                await UniTask.Yield(PlayerLoopTiming.Update);
            if (this == null || !_bootMenuRevealedEarly) return;

            // The host is late (UGS slow or failing): cover again so the boot's retry and offline screens show.
            _bootMenuRevealedEarly = false;
            HoldMenuInput(false);
            _sceneTransitionManager.SetFadeImmediate(1f);
            CSDebug.LogWarning($"[SceneLoader] No host {bootMenuHostWaitSeconds:0} s after the early menu reveal; covering the menu again.");
        }

        /// <summary>
        /// For <see cref="FadeFromSplashWhenSettled"/>: true when the menu is already on screen from the early reveal.
        /// Hands UI input back either way.
        /// </summary>
        bool TakeEarlyBootReveal()
        {
            if (!_bootMenuRevealedEarly) return false;
            _bootMenuRevealedEarly = false;
            HoldMenuInput(false);
            return true;
        }

        void HoldMenuInput(bool hold)
        {
            if (hold)
            {
                _heldEventSystem = EventSystem.current;
                if (_heldEventSystem) _heldEventSystem.enabled = false;
                return;
            }
            if (_heldEventSystem) _heldEventSystem.enabled = true;
            _heldEventSystem = null;
        }
    }
}
