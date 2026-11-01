using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The voyage scene's <b>smoke test</b> (<c>-voyageSmoke</c>, started by <c>ArkEscortSimLauncher.RunSmoke</c>): from
    /// the menu it sets sail on the Ark escort exactly as the Voyage window does (<see cref="VoyageLaunch.Begin"/> with
    /// the window's request), then checks that the voyage scene loaded clean (no toybox, no freestyle toggle, no toy) and
    /// that the local pilot is in a vessel they can fly (the hull they flew in the menu, autopilot off, input live, moving).
    /// It then leaves the voyage and checks the menu comes back with its Toy Box. A tool, never a player path.
    /// </summary>
    public sealed class VoyageSmoke : MonoBehaviour
    {
        const string Arg = "-voyageSmoke";
        const string MenuScene = "Menu_Main";

        /// <summary>Raised once with the verdict: the editor launcher exits on it.</summary>
        public static event Action<bool> Done;

        readonly List<string> _failures = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Arg) < 0) return;
            AudioListener.volume = 0f;
            AudioListener.pause = true;
            var go = new GameObject("VoyageSmoke");
            DontDestroyOnLoad(go);
            var smoke = go.AddComponent<VoyageSmoke>();
            smoke.RunAsync(smoke.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid RunAsync(CancellationToken ct)
        {
            bool ok = false;
            try
            {
                if (!await WaitAsync(MenuReady, 600f, ct)) { Fail("the menu never became ready"); return; }
                await UniTask.Delay(5000, DelayType.Realtime, cancellationToken: ct);
                var gameData = VoyageLaunch.ResolveGameData();
                var flown = gameData.LocalPlayer.Vessel.VesselStatus.VesselType;
                int surfaces = ToyShellRegistry.Surfaces.Count;
                Log($"menu ready: flying {flown}, {surfaces} toys in the Toy Box, " +
                    $"{FindObjectsByType<Toy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length} toy objects, " +
                    $"{FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length} colliders");
                if (surfaces == 0) Fail("the menu's Toy Box lists no toys");
                foreach (var toy in FindObjectsByType<Toy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (toy.GetComponentInChildren<Collider>(true)) Fail($"toy '{toy.name}' still has a collider to fly into");

                // The Voyage window's Set sail on the escort card.
                if (!VoyageLaunch.Begin(VoyageLaunchRequest.ForEscort(1))) { Fail("VoyageLaunch.Begin refused"); return; }
                if (!await WaitAsync(() => VoyageScene.Current && VoyageScene.Current.IsFlying, 240f, ct))
                {
                    Fail("the voyage scene never handed over the stick");
                    return;
                }
                if (SceneManager.GetActiveScene().name != VoyageLaunch.SceneName)
                    Fail($"active scene is {SceneManager.GetActiveScene().name}, not {VoyageLaunch.SceneName}");
                if (FindAnyObjectByType<ToyboxController>(FindObjectsInactive.Include)) Fail("a ToyboxController stands in the voyage scene");
                if (FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include)) Fail("a freestyle toggle stands in the voyage scene");
                if (FindObjectsByType<Toy>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0) Fail("a toy stands in the voyage scene");

                await WaitAsync(() => VoyageHarbour.Current && VoyageHarbour.Current.IsEscorting, 60f, ct);
                if (!VoyageHarbour.Current || !VoyageHarbour.Current.IsEscorting) Fail("the escort did not sail");
                await UniTask.Delay(8000, DelayType.Realtime, cancellationToken: ct);

                var player = gameData.LocalPlayer;
                var status = player?.Vessel?.VesselStatus;
                if (status == null || !player.Vessel.IsAlive()) Fail("no live local vessel");
                else
                {
                    var from = status.Transform.position;
                    await UniTask.Delay(2000, DelayType.Realtime, cancellationToken: ct);
                    float moved = (status.Transform.position - from).magnitude;
                    bool paused = player.InputController && player.InputController.InputStatus != null && player.InputController.InputStatus.Paused;
                    Log($"in the voyage: {status.VesselType}, local user {player.IsLocalUser}, autopilot {status.AutoPilotEnabled}, " +
                        $"input paused {paused}, moved {moved:0} u in 2 s, harbour escorting {VoyageHarbour.Current && VoyageHarbour.Current.IsEscorting}");
                    if (status.VesselType != flown) Fail($"the pilot was put in a {status.VesselType}, not the {flown} they flew");
                    if (status.AutoPilotEnabled) Fail("the local vessel is on autopilot");
                    if (paused) Fail("the local vessel's input is paused");
                    if (!player.IsLocalUser) Fail("the local player is not the local user");
                    if (moved < 1f) Fail("the local vessel is not moving");
                }

                // The HUD's exit: leave the voyage, back to the menu.
                VoyageScene.Current.Leave();
                if (!await WaitAsync(MenuReady, 240f, ct)) { Fail("the menu never came back after leaving the voyage"); return; }
                await UniTask.Delay(5000, DelayType.Realtime, cancellationToken: ct);
                Log($"back in the menu: {ToyShellRegistry.Surfaces.Count} toys in the Toy Box");
                if (ToyShellRegistry.Surfaces.Count == 0) Fail("the Toy Box lists no toys after the voyage");
                ok = _failures.Count == 0;
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Fail($"threw: {e}"); }
            finally
            {
                Log(ok ? "PASS" : $"FAIL ({_failures.Count}): {string.Join("; ", _failures)}");
                Done?.Invoke(ok);
            }
        }

        static bool MenuReady()
        {
            var gameData = VoyageLaunch.ResolveGameData();
            return SceneManager.GetActiveScene().name == MenuScene && !VoyageScene.Current && gameData
                   && gameData.LocalPlayer?.Vessel.IsAlive() == true && !EnvironmentLoadVeil.IsCovering;
        }

        static async UniTask<bool> WaitAsync(Func<bool> condition, float seconds, CancellationToken ct)
        {
            float deadline = Time.unscaledTime + seconds;
            while (Time.unscaledTime < deadline)
            {
                if (condition()) return true;
                await UniTask.Delay(500, DelayType.Realtime, cancellationToken: ct);
            }
            return condition();
        }

        void Fail(string why)
        {
            _failures.Add(why);
            Log("fail: " + why);
        }

        static void Log(string line) => CSDebug.LogWarning($"[VoyageSmoke] {line}");
    }
}
