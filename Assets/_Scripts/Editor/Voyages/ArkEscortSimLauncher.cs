using System;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Starts the escort sim (<see cref="ArkEscortSim"/>) from the command line and closes the editor when it is done, so
    /// a tuning session can play escorts with nobody at the keyboard:
    /// <code>Unity.exe -projectPath . -executeMethod CosmicShore.Editor.ArkEscortSimLauncher.Run
    ///   -arkEscortSim "5:Sparrow,Rhino,Manta" -arkEscortOut Logs/escort.jsonl -arkEscortTimeScale 3 -arkEscortQuit</code>
    /// It opens Bootstrap and enters Play; the sim does the rest. A reader of the game, it writes no assets.
    /// </summary>
    [InitializeOnLoad]
    public static class ArkEscortSimLauncher
    {
        const string QuitArg = "-arkEscortQuit";

        static ArkEscortSimLauncher()
        {
            ArkEscortSim.AllDone -= OnDone;
            ArkEscortSim.AllDone += OnDone;
        }

        /// <summary>The <c>-executeMethod</c> entry: Bootstrap, then Play.</summary>
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/_Scenes/Bootstrap.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void OnDone(bool ok)
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), QuitArg) < 0) return;
            EditorApplication.delayCall += () =>
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorApplication.Exit(ok ? 0 : 1);
            };
        }
    }
}
