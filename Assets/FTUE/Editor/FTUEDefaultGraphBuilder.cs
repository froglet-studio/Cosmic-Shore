#if !LINUX_BUILD
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click generator for the canonical FTUE flow described by design:
    /// drop the player into menu freestyle, teach throttle + steering, deliver a captain
    /// line, prompt them back to the menu, then funnel them into the first arcade game and
    /// wait until they've played it. Produces a fully-wired <see cref="FTUEGraphSO"/> asset
    /// (nodes as sub-assets) so it is runnable end-to-end without hand-authoring YAML.
    ///
    /// Runs in the Unity editor (can't be done headless), matching the project's other
    /// generator menus (e.g. "Create Party Prefabs").
    /// </summary>
    public static class FTUEDefaultGraphBuilder
    {
        const string Folder = "Assets/FTUE/DataContainer";

        [MenuItem("FrogletTools/FTUE/Create Default FTUE Graph")]
        public static void CreateDefaultGraph()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/FTUE", "DataContainer");

            string path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/FTUEGraph_Default.asset");

            var graph = ScriptableObject.CreateInstance<FTUEGraphSO>();
            graph.graphId = "FTUE_Default";
            AssetDatabase.CreateAsset(graph, path);

            int i = 0;
            Vector2 Pos() { var p = new Vector2(60 + (i / 6) * 230, 40 + (i % 6) * 100); i++; return p; }

            var intro          = Make<FTUEPlayIntroNode>(graph, "Intro Cinematic", Pos());
            var enterFreestyle = Make<FTUEEnterFreestyleNode>(graph, "Enter Freestyle", Pos());

            var throttlePrompt = Make<FTUEShowInstructionNode>(graph, "Prompt: Throttle", Pos());
            throttlePrompt.text = "Push forward to fly at full speed!";
            throttlePrompt.advanceOnNextPress = false;

            var waitThrottle   = Make<FTUEWaitForInputNode>(graph, "Wait: Full Throttle", Pos());
            waitThrottle.acceptedInputs = new List<InputEvents> { InputEvents.FullSpeedStraightAction };

            var steerPrompt    = Make<FTUEShowInstructionNode>(graph, "Prompt: Steer", Pos());
            steerPrompt.text = "Nice! Now tilt to steer left and right.";
            steerPrompt.advanceOnNextPress = false;

            var waitSteer      = Make<FTUEWaitForInputNode>(graph, "Wait: Steer", Pos());
            waitSteer.acceptedInputs = new List<InputEvents>
            {
                InputEvents.LeftStickAction, InputEvents.RightStickAction,
                InputEvents.OnlyLeftStickAction, InputEvents.OnlyRightStickAction,
                InputEvents.BothSticksAction,
            };

            var captain        = Make<FTUEDialogueNode>(graph, "Captain Intro (assign a DialogueSet)", Pos());

            var backPrompt     = Make<FTUEShowInstructionNode>(graph, "Prompt: Head Back", Pos());
            backPrompt.text = "Great flying! Tap to head back to the menu.";
            backPrompt.advanceOnNextPress = true;

            var exitFreestyle  = Make<FTUEExitFreestyleNode>(graph, "Wait: Return To Menu", Pos());

            var arcadePrompt   = Make<FTUEShowInstructionNode>(graph, "Prompt: Open Arcade", Pos());
            arcadePrompt.text = "Let's play your first game — open the Arcade.";
            arcadePrompt.advanceOnNextPress = true;

            var highlightArcade = Make<FTUEHighlightCTANode>(graph, "CTA: Arcade", Pos());
            highlightArcade.target = CallToActionTargetType.ArcadeMenu;
            highlightArcade.completionAction = UserActionType.ViewArcadeMenu;

            var lockModes      = Make<FTUELockModesNode>(graph, "Lock To Tutorial Game", Pos());
            lockModes.tutorialGame = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;

            var highlightGame  = Make<FTUEHighlightCTANode>(graph, "CTA: Play Tutorial Game", Pos());
            highlightGame.target = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;
            highlightGame.completionAction = UserActionType.PlayGame;

            var waitLaunch     = Make<FTUEWaitForGameLaunchNode>(graph, "Wait: Game Launched", Pos());

            var setPhase       = Make<FTUESetPhaseNode>(graph, "Advance To Phase 2", Pos());
            setPhase.phase = TutorialPhase.Phase2_GameplayTimer;

            var waitPlayed     = Make<FTUEWaitForGamePlayedNode>(graph, "Wait: Game Finished", Pos());
            var end            = Make<FTUEEndNode>(graph, "FTUE Complete", Pos());

            // Wire the linear spine.
            var order = new FTUENodeSO[]
            {
                intro, enterFreestyle, throttlePrompt, waitThrottle, steerPrompt, waitSteer,
                captain, backPrompt, exitFreestyle, arcadePrompt, highlightArcade, lockModes,
                highlightGame, waitLaunch, setPhase, waitPlayed, end,
            };
            for (int k = 0; k < order.Length - 1; k++)
                order[k].Outputs.Add(new FTUEEdge(FTUEPorts.Next, order[k + 1].nodeId));

            graph.entryNode = intro;

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
            Debug.Log($"[FTUE] Created default graph at {path} with {graph.nodes.Count} nodes. " +
                      $"Assign a DialogueSet to the 'Captain Intro' node, then open it in FrogletTools ▸ FTUE ▸ Graph Editor.");
        }

        static T Make<T>(FTUEGraphSO graph, string name, Vector2 pos) where T : FTUENodeSO
        {
            var node = ScriptableObject.CreateInstance<T>();
            node.nodeId = Guid.NewGuid().ToString("N");
            node.name = name;
            node.displayName = name;
            node.graphPosition = pos;
            AssetDatabase.AddObjectToAsset(node, graph);
            graph.nodes.Add(node);
            return node;
        }
    }
}
#endif
