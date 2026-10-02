using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Boots with the app and, when <see cref="TrainingControlSO.DeployArchiveInNormalPlay"/>
    /// is on, installs <see cref="TrainingPilot"/> on every AI vessel for the current game mode —
    /// so a human immediately plays against trained opponents without waiting for a Learn session.
    ///
    /// Idle while a training session is running (<see cref="GameDataSO.IsTraining"/>) so it never
    /// fights the session runner for ownership of an AI's brain. Uses the same
    /// <see cref="ArchiveDeployment.TryInstall"/> path as the training bridge and editor tools.
    /// </summary>
    public sealed class TrainingDeploymentService : MonoBehaviour
    {
        const int EnsureBurstFrames = 12;
        const float KeepReadyIntervalSeconds = 1f;

        static TrainingControlSO s_Control;
        static TrainingArchiveSO s_Archive;
        static GameDataSO s_GameData;
        static CellRuntimeDataSO s_CellData;
        static bool s_Booted;
        static bool s_WatchingGameData;
        static TrainingDeploymentService s_Instance;
        static readonly HashSet<string> s_Installed = new();
        static readonly HashSet<IPlayer> s_MissingVesselWarned = new();

        Coroutine _ensureRoutine;
        Coroutine _keepReadyRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (s_Booted) return;
            s_Booted = true;

            ResolveAssets();
            if (s_Control == null || s_GameData == null)
            {
                Debug.LogWarning("[AITraining] TrainingDeploymentService: control or GameDataSO missing — idle.");
                return;
            }

            // Always subscribe. DeployArchiveInNormalPlay is gated at the handlers so an inspector
            // flip mid-session still takes effect on the next turn / pair-init without an OnChanged
            // event on TrainingControlSO (that asset has none).
            HookGameData();

            var go = new GameObject("[AITraining.Deployment]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            s_Instance = go.AddComponent<TrainingDeploymentService>();
        }

        static void ResolveAssets()
        {
            if (s_Control == null) s_Control = FirstAsset<TrainingControlSO>();
            if (s_GameData == null) s_GameData = FirstAsset<GameDataSO>();
            if (s_CellData == null) s_CellData = FirstAsset<CellRuntimeDataSO>();
            // Prefer the archive wired on the control asset (what the Learn button writes into).
            if (s_Control != null && s_Control.Archive != null)
                s_Archive = s_Control.Archive;
            else if (s_Archive == null)
                s_Archive = FirstAsset<TrainingArchiveSO>();
        }

        static T FirstAsset<T>() where T : Object
        {
#if UNITY_EDITOR
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids == null || guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            return null;
#endif
        }

        static void HookGameData()
        {
            if (s_WatchingGameData || s_GameData == null) return;
            s_WatchingGameData = true;
            if (s_GameData.OnMiniGameTurnStarted != null)
                s_GameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            if (s_GameData.OnMiniGameTurnEnd != null)
                s_GameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
            // VesselStatus has no OnVesselInitialized. Pair-init is the platform event that fires
            // once a player+vessel exist — covers mid-match AI swaps / late AI seats.
            if (s_GameData.OnPlayerPairInitialized != null)
                s_GameData.OnPlayerPairInitialized.OnRaised += HandlePlayerPairInitialized;
        }

        static void HandleTurnStarted()
        {
            if (!ShouldDeploy()) return;
            if (s_Instance == null) return;

            s_Instance.StopEnsureAndKeep();
            s_Instance._ensureRoutine = s_Instance.StartCoroutine(s_Instance.EnsureAiPilotsReady());
            s_Instance._keepReadyRoutine = s_Instance.StartCoroutine(s_Instance.KeepAiPilotsReady());
        }

        static void HandleTurnEnded()
        {
            if (s_Instance != null)
                s_Instance.StopEnsureAndKeep();
            if (!ShouldDeploy()) return;
            EndEpisodes();
        }

        static void HandlePlayerPairInitialized(ulong _)
        {
            if (!ShouldDeploy()) return;
            InstallOnAllAi();
            ReadyAllAiSeats();
        }

        static bool ShouldDeploy()
        {
            if (s_Control == null || !s_Control.DeployArchiveInNormalPlay) return false;
            // Session runner owns genomes while a Learn match is live.
            if (s_GameData != null && s_GameData.IsTraining) return false;
            return true;
        }

        void StopEnsureAndKeep()
        {
            if (_ensureRoutine != null)
            {
                StopCoroutine(_ensureRoutine);
                _ensureRoutine = null;
            }
            if (_keepReadyRoutine != null)
            {
                StopCoroutine(_keepReadyRoutine);
                _keepReadyRoutine = null;
            }
        }

        /// <summary>
        /// Burst of readiness assertions at turn start. Archive BeginEpisode never unstations —
        /// a Vessel-null StartPlayer leaves IsStationary true forever (Ruby at 0), so we retry
        /// across a few frames until Vessel wiring catches up.
        /// </summary>
        IEnumerator EnsureAiPilotsReady()
        {
            for (int i = 0; i < EnsureBurstFrames; i++)
            {
                if (!ShouldDeploy()) yield break;
                ReadyAllAiSeats();
                yield return null;
            }
            _ensureRoutine = null;
        }

        /// <summary>
        /// 1 Hz heartbeat for the life of the turn. Mid-match re-freeze / late Vessel wiring
        /// can re-park a seat; keep StartPlayer + IsStationary + BeginEpisode asserted.
        /// </summary>
        IEnumerator KeepAiPilotsReady()
        {
            var wait = new WaitForSeconds(KeepReadyIntervalSeconds);
            while (true)
            {
                if (!ShouldDeploy()) yield break;
                ReadyAllAiSeats();
                yield return wait;
            }
        }

        static void ReadyAllAiSeats()
        {
            if (s_GameData?.Players == null) return;
            ResolveAssets();
            for (int i = 0; i < s_GameData.Players.Count; i++)
            {
                var player = s_GameData.Players[i];
                if (player == null || !player.IsInitializedAsAI) continue;

                // Avoid repeating StartPlayer's null-vessel warning throughout the burst
                // and heartbeat. Keep retrying this seat until its pair is wired.
                if (player.Vessel == null)
                {
                    if (s_MissingVesselWarned.Add(player))
                        CSDebug.LogWarning($"[AITraining] AI seat '{player.Name}' has no Vessel at StartPlayer; " +
                                           "IsStationary cannot be cleared. Retrying vessel readiness.");
                    continue;
                }

                player.StartPlayer();

                var vessel = player.Vessel;
                if (vessel == null) continue;

                var status = vessel.VesselStatus as VesselStatus;
                if (status != null)
                    status.IsStationary = false;

                InstallOn(vessel);

                if (status == null) continue;
                var pilot = status.GetComponent<TrainingPilot>();
                if (pilot != null && !pilot.EpisodeActive)
                    pilot.BeginEpisode();
            }
        }

        void Update()
        {
            if (!ShouldDeploy()) return;
            // Slow heartbeat so late-spawning AI (post-countdown backfill) still get a pilot
            // even if the turn-started event already fired.
            if (Time.frameCount % 30 != 0) return;
            InstallOnAllAi();
        }

        static void InstallOnAllAi()
        {
            if (s_GameData?.Players == null) return;
            ResolveAssets();
            for (int i = 0; i < s_GameData.Players.Count; i++)
            {
                var player = s_GameData.Players[i];
                if (player == null || !player.IsInitializedAsAI) continue;
                var vessel = player.Vessel;
                if (vessel == null) continue;
                InstallOn(vessel);
            }
        }

        static void EndEpisodes()
        {
            if (s_GameData?.Players == null) return;
            for (int i = 0; i < s_GameData.Players.Count; i++)
            {
                var player = s_GameData.Players[i];
                if (player?.Vessel == null) continue;
                var status = player.Vessel.VesselStatus as VesselStatus;
                if (status == null) continue;
                var pilot = status.GetComponent<TrainingPilot>();
                if (pilot != null && pilot.EpisodeActive)
                    pilot.EndEpisode();
            }
            s_Installed.Clear();
            s_MissingVesselWarned.Clear();
        }

        static void InstallOn(IVessel vessel)
        {
            if (vessel == null) return;
            var go = (vessel as Component)?.gameObject
                     ?? (vessel.VesselStatus as Component)?.gameObject;
            if (go == null) return;

            // Dedup by vessel GameObject instance id — player Name can collide across respawns.
            string key = go.GetInstanceID().ToString();
            if (s_Installed.Contains(key) && go.GetComponent<TrainingPilot>() != null)
                return;

            var archive = s_Control != null && s_Control.Archive != null
                ? s_Control.Archive
                : s_Archive;
            if (archive == null)
            {
                Debug.LogWarning("[AITraining] TrainingDeploymentService: no archive — cannot install.");
                return;
            }

            var mode = s_GameData != null ? s_GameData.GameMode : GameModes.Random;
            int intensity = s_GameData != null && s_GameData.SelectedIntensity != null
                ? Mathf.Max(1, s_GameData.SelectedIntensity.Value)
                : 1;
            bool useStored = s_Control != null && s_Control.UseStoredGenomeForLowerIntensity;
            VesselClassType liveVessel = vessel.VesselStatus?.VesselType ?? VesselClassType.Any;

            // Same Install path as TrainingAIDeploymentBridge / editor tools.
            // BeginEpisode is called inside ArchiveDeployment.Install when the vessel is ready.
            if (!ArchiveDeployment.TryInstall(
                    go,
                    vessel,
                    s_GameData,
                    s_CellData,
                    archive,
                    mode,
                    liveVessel,
                    playIntensity: intensity,
                    useStoredGenomeForLowerIntensity: useStored))
            {
                return;
            }

            s_Installed.Add(key);
        }
    }
}
