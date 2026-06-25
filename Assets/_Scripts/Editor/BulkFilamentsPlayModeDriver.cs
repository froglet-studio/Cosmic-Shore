#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.EditorTools
{
    [InitializeOnLoad]
    public static class BulkFilamentsPlayModeDriver
    {
        const string MenuRoot = "FrogletTools/Bulk Filaments/";
        const string MenuScenePath = "Assets/_Scenes/Menu_Main.unity";
        const string BulkScenePath = "Assets/_Scenes/Singleplayer Scenes/MinigameBulkFilaments.unity";
        const string BulkGamePath = "Assets/_SO_Assets/Games/ArcadeGameTheBulkFilaments.asset";
        const string BatchQaActiveSessionKey = "CosmicShore.BulkFilaments.BatchQaActive";
        const string BatchQaActiveEditorKey = "CosmicShore.BulkFilaments.BatchQaActiveEditor";
        const string BatchQaProcessEditorKey = "CosmicShore.BulkFilaments.BatchQaProcess";
        const int QaTransfersToVerify = 3;
        const double QaTimeoutSeconds = 95d;
        const double BatchQaTimeoutSeconds = 180d;
        static int step;
        static double nextStepTime;
        static bool selectedBulk;
        static bool qaPressedGo;
        static int qaStartTransfers;
        static double qaStartTime;
        static double qaNextLatchTime;
        static bool qaCompleted;
        static bool qaSucceeded;
        static string qaResult;
        static int batchStep;
        static double batchStartTime;
        static double batchNextStepTime;
        static double batchNextControllerLogTime;

        static BulkFilamentsPlayModeDriver()
        {
            EditorApplication.delayCall -= ResumeBatchQaAfterReload;
            EditorApplication.delayCall += ResumeBatchQaAfterReload;
        }

        static void ResumeBatchQaAfterReload()
        {
            if (!IsBatchQaArmedForThisProcess())
                return;

            batchStep = 0;
            batchStartTime = EditorApplication.timeSinceStartup;
            batchNextStepTime = batchStartTime + 0.5d;
            batchNextControllerLogTime = batchStartTime;
            qaCompleted = false;
            qaSucceeded = false;
            qaResult = string.Empty;

            ArmBatchQaDriver();
            Debug.Log("[BulkFilamentsBatchQA] Resumed batch QA after Play Mode reload.");
        }

        [MenuItem(MenuRoot + "Open Arcade Panel", priority = 10)]
        public static void OpenArcadePanel()
        {
            if (!EnsurePlaying())
                return;

            var screenSwitcher = FindSceneComponent<ScreenSwitcher>();
            if (!screenSwitcher)
            {
                Debug.LogWarning("[BulkFilamentsDriver] ScreenSwitcher not found.");
                return;
            }

            screenSwitcher.OnClickArcadeNav();
            Debug.Log("[BulkFilamentsDriver] Opened Arcade panel.");
        }

        [MenuItem(MenuRoot + "Launch Bulk Filaments", priority = 11)]
        public static void LaunchBulkFilaments()
        {
            if (!EnsurePlaying())
                return;

            step = 0;
            selectedBulk = false;
            nextStepTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= DriveLaunch;
            EditorApplication.update += DriveLaunch;
        }

        [MenuItem(MenuRoot + "Press GO", priority = 12)]
        public static void PressGo()
        {
            if (!EnsurePlaying())
                return;

            var controller = FindSceneComponent<MiniGameControllerBase>();
            if (!controller)
            {
                Debug.LogWarning("[BulkFilamentsDriver] MiniGameControllerBase not found.");
                return;
            }

            controller.OnReadyClicked();
            Debug.Log("[BulkFilamentsDriver] Pressed GO.");
        }

        [MenuItem(MenuRoot + "Run Control QA", priority = 13)]
        public static void RunControlQa()
        {
            if (!EnsurePlaying())
                return;

            StopControlQa();
            qaPressedGo = false;
            qaStartTransfers = -1;
            qaStartTime = EditorApplication.timeSinceStartup;
            qaNextLatchTime = qaStartTime;
            qaCompleted = false;
            qaSucceeded = false;
            qaResult = string.Empty;
            EditorApplication.update += DriveControlQa;
            Debug.Log("[BulkFilamentsQA] Armed control QA.");
        }

        public static void RunBatchLaunchAndControlQa()
        {
            StopDriving();
            StopControlQa();
            EditorApplication.update -= DriveBatchLaunchAndControlQa;

            batchStep = 0;
            selectedBulk = false;
            qaCompleted = false;
            qaSucceeded = false;
            qaResult = string.Empty;
            batchStartTime = EditorApplication.timeSinceStartup;
            batchNextStepTime = batchStartTime;
            batchNextControllerLogTime = batchStartTime;

            SessionState.SetBool(BatchQaActiveSessionKey, true);
            EditorPrefs.SetBool(BatchQaActiveEditorKey, true);
            EditorPrefs.SetInt(BatchQaProcessEditorKey, System.Diagnostics.Process.GetCurrentProcess().Id);
            ArmBatchQaDriver();
            EditorApplication.EnterPlaymode();
            Debug.Log("[BulkFilamentsBatchQA] Starting direct scene/control QA.");
        }

        static void ArmBatchQaDriver()
        {
            if (batchStartTime <= 0d)
                batchStartTime = EditorApplication.timeSinceStartup;

            if (batchNextStepTime <= 0d)
                batchNextStepTime = batchStartTime;

            EditorApplication.update -= DriveBatchLaunchAndControlQa;
            EditorApplication.update += DriveBatchLaunchAndControlQa;
        }

        static void DriveBatchLaunchAndControlQa()
        {
            if (EditorApplication.timeSinceStartup - batchStartTime > BatchQaTimeoutSeconds)
            {
                CompleteBatchQa(false, $"timed out in batch step {batchStep}");
                return;
            }

            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < batchNextStepTime)
                return;

            switch (batchStep)
            {
                case 0:
                    if (SceneManager.GetActiveScene().path != BulkScenePath)
                    {
                        Debug.Log($"[BulkFilamentsBatchQA] Loading Bulk scene from '{SceneManager.GetActiveScene().path}'.");
                        EditorSceneManager.LoadSceneInPlayMode(BulkScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                        batchStep = 1;
                        batchNextStepTime = EditorApplication.timeSinceStartup + 1d;
                        return;
                    }

                    batchStep = 1;
                    break;
                case 1:
                    if (!FindSceneComponent<BulkFilamentsController>())
                    {
                        if (EditorApplication.timeSinceStartup >= batchNextControllerLogTime)
                        {
                            Debug.Log("[BulkFilamentsBatchQA] Waiting for BulkFilamentsController.");
                            batchNextControllerLogTime = EditorApplication.timeSinceStartup + 5d;
                        }

                        return;
                    }

                    RunControlQa();
                    batchStep = 2;
                    batchNextStepTime = EditorApplication.timeSinceStartup + 0.25d;
                    break;
                case 2:
                    if (qaCompleted)
                        CompleteBatchQa(qaSucceeded, qaResult);
                    break;
            }
        }

        static void DriveLaunch()
        {
            if (!EditorApplication.isPlaying)
            {
                StopDriving();
                return;
            }

            if (EditorApplication.timeSinceStartup < nextStepTime)
                return;

            switch (step)
            {
                case 0:
                    OpenArcadePanel();
                    Advance();
                    break;
                case 1:
                    if (SelectBulkFilaments())
                        Advance();
                    else
                        RetrySoon();
                    break;
                case 2:
                    if (selectedBulk)
                        StartConfiguredGame();
                    else
                        Debug.LogWarning("[BulkFilamentsDriver] Refusing to start without Bulk selected.");

                    StopDriving();
                    break;
            }
        }

        static void DriveControlQa()
        {
            if (!EditorApplication.isPlaying)
            {
                StopControlQa();
                return;
            }

            var controller = FindSceneComponent<BulkFilamentsController>();
            if (!controller)
            {
                if (TimedOut())
                    FailControlQa("BulkFilamentsController not found.");
                return;
            }

            if (!qaPressedGo)
            {
                controller.OnReadyClicked();
                qaPressedGo = true;
                Debug.Log("[BulkFilamentsQA] Pressed GO.");
                return;
            }

            if (!controller.EditorQaIsRunning)
            {
                if (TimedOut())
                    FailControlQa("turn never started.");
                return;
            }

            if (qaStartTransfers < 0)
                qaStartTransfers = controller.EditorQaSuccessfulTransfers;

            float time = Time.realtimeSinceStartup;
            bool shouldLatch =
                Mathf.Abs(controller.EditorQaDistanceToTransfer) <= controller.EditorQaLatchWindow * 0.72f &&
                EditorApplication.timeSinceStartup >= qaNextLatchTime;
            controller.SetEditorQaInput(Mathf.Sin(time * 2.4f), 0.68f, shouldLatch);

            if (shouldLatch)
                qaNextLatchTime = EditorApplication.timeSinceStartup + 0.55d;

            int completed = controller.EditorQaSuccessfulTransfers - qaStartTransfers;
            if (completed >= QaTransfersToVerify)
            {
                Debug.Log($"[BulkFilamentsQA] PASS transfers={completed} crystals={controller.EditorQaCrystalsCollected}.");
                qaCompleted = true;
                qaSucceeded = true;
                qaResult = $"transfers={completed} crystals={controller.EditorQaCrystalsCollected}";
                StopControlQa();
                return;
            }

            if (TimedOut())
                FailControlQa($"timed out after {completed} transfers.");
        }

        static bool SelectBulkFilaments()
        {
            var exploreView = FindSceneComponent<ArcadeExploreView>();
            var game = FindBulkGame();

            if (!exploreView || !game)
            {
                Debug.LogWarning("[BulkFilamentsDriver] Arcade view or Bulk Filaments asset not ready.");
                return false;
            }

            exploreView.PopulateGameSelectionList();
            exploreView.SelectGame(game);
            selectedBulk = true;
            Debug.Log("[BulkFilamentsDriver] Selected The Bulk Filaments.");
            return true;
        }

        static void StartConfiguredGame()
        {
            var modal = ArcadeGameConfigureModal.Instance ?? FindSceneComponent<ArcadeGameConfigureModal>();
            if (!modal)
            {
                Debug.LogWarning("[BulkFilamentsDriver] ArcadeGameConfigureModal not found.");
                return;
            }

            modal.OnStartGameClicked();
            Debug.Log("[BulkFilamentsDriver] Started The Bulk Filaments.");
        }

        static SO_ArcadeGame FindBulkGame()
        {
            var liveGame = Arcade.Instance?.ArcadeGames?.Games
                .FirstOrDefault(candidate => candidate.Mode == GameModes.TheBulkFilaments);

            return liveGame ? liveGame : AssetDatabase.LoadAssetAtPath<SO_ArcadeGame>(BulkGamePath);
        }

        static void Advance()
        {
            step++;
            nextStepTime = EditorApplication.timeSinceStartup + 0.75d;
        }

        static void RetrySoon()
        {
            nextStepTime = EditorApplication.timeSinceStartup + 0.5d;
        }

        static void StopDriving() => EditorApplication.update -= DriveLaunch;

        static void StopControlQa()
        {
            EditorApplication.update -= DriveControlQa;
            var controller = FindSceneComponent<BulkFilamentsController>();
            if (controller)
                controller.ClearEditorQaInput();
        }

        static bool TimedOut()
        {
            return EditorApplication.timeSinceStartup - qaStartTime > QaTimeoutSeconds;
        }

        static void FailControlQa(string reason)
        {
            Debug.LogWarning($"[BulkFilamentsQA] FAIL {reason}");
            qaCompleted = true;
            qaSucceeded = false;
            qaResult = reason;
            StopControlQa();
        }

        static void CompleteBatchQa(bool success, string message)
        {
            SessionState.SetBool(BatchQaActiveSessionKey, false);
            EditorPrefs.SetBool(BatchQaActiveEditorKey, false);
            EditorPrefs.DeleteKey(BatchQaProcessEditorKey);
            EditorApplication.update -= DriveBatchLaunchAndControlQa;
            StopDriving();
            StopControlQa();

            if (success)
                Debug.Log($"[BulkFilamentsBatchQA] PASS {message}");
            else
                Debug.LogWarning($"[BulkFilamentsBatchQA] FAIL {message}");

            EditorApplication.Exit(success ? 0 : 1);
        }

        static bool IsBatchQaArmedForThisProcess()
        {
            if (SessionState.GetBool(BatchQaActiveSessionKey, false))
                return true;

            if (!EditorPrefs.GetBool(BatchQaActiveEditorKey, false))
                return false;

            return EditorPrefs.GetInt(BatchQaProcessEditorKey, -1) == System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        static bool EnsurePlaying()
        {
            if (EditorApplication.isPlaying)
                return true;

            Debug.LogWarning("[BulkFilamentsDriver] Enter Play Mode before using this command.");
            return false;
        }

        static T FindSceneComponent<T>() where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>()
                .FirstOrDefault(item => item && item.gameObject.scene.IsValid());
        }
    }
}
#endif
