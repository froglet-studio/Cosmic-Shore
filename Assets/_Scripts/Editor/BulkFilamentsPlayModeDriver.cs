#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.EditorTools
{
    public static class BulkFilamentsPlayModeDriver
    {
        const string MenuRoot = "FrogletTools/Bulk Filaments/";
        const string BulkGamePath = "Assets/_SO_Assets/Games/ArcadeGameTheBulkFilaments.asset";
        const int QaTransfersToVerify = 3;
        const double QaTimeoutSeconds = 95d;
        static int step;
        static double nextStepTime;
        static bool selectedBulk;
        static bool qaPressedGo;
        static int qaStartTransfers;
        static double qaStartTime;
        static double qaNextLatchTime;

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
            EditorApplication.update += DriveControlQa;
            Debug.Log("[BulkFilamentsQA] Armed control QA.");
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
            StopControlQa();
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
