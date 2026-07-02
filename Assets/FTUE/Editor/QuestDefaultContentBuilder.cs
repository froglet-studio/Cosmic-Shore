#if !LINUX_BUILD
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click generator for the canonical Main Quest (the FTUE): six phases matching the
    /// design map — onboarding + Crystal Capture, then one unlock phase per mode (HexRace,
    /// Joust, Maelstrom), the vessel tour, and the episodes finale. Produces a fully-wired
    /// <see cref="QuestSO"/> + per-phase <see cref="QuestPhaseGraphSO"/> assets (nodes as
    /// sub-assets) with designer notes baked in, runnable end-to-end once DialogueSets are
    /// assigned and the new CTA targets exist in the scene.
    /// </summary>
    public static class QuestDefaultContentBuilder
    {
        const string RootFolder = "Assets/FTUE/DataContainer";
        const string QuestsFolder = RootFolder + "/Quests";
        const string PhasesFolder = RootFolder + "/Phases";

        static int _nodeCursor;

        [MenuItem("FrogletTools/Quest Graph/Create Main Quest (Default Content)")]
        public static void CreateMainQuest()
        {
            EnsureFolders();

            var quest = ScriptableObject.CreateInstance<QuestSO>();
            quest.questId = "MainQuest";
            quest.designerNotes =
                "THE Main Quest (FTUE). Phase pattern after onboarding: gate on the previous mode's " +
                "intensity-4 milestone → guide to the profile screen → player CLAIMS the next mode " +
                "(unlocks to intensity 3) → reward dialogue → guide back to the arcade → play it.\n\n" +
                "Reference map: see QUEST_GRAPH_TOOL.md. Dialogue nodes need DialogueSets assigned. " +
                "New CTA targets (ProfileMenu / EpisodeMenu / PlayGameMaelstrom) and user actions " +
                "(ViewProfileMenu / ViewEpisodeMenu / UnlockVessel) need scene UI wiring.";
            string questPath = AssetDatabase.GenerateUniqueAssetPath($"{QuestsFolder}/MainQuest.asset");
            AssetDatabase.CreateAsset(quest, questPath);

            quest.phases.Add(BuildPhase0());
            quest.phases.Add(BuildUnlockPhase(1, "Unlock HexRace",
                gateMode: GameModes.MultiplayerCrystalCapture,
                claimMode: GameModes.HexRace,
                playCta: CallToActionTargetType.PlayGameHexRace,
                nextLabel: "Phase 2 (Joust)"));
            quest.phases.Add(BuildUnlockPhase(2, "Unlock Joust",
                gateMode: GameModes.HexRace,
                claimMode: GameModes.MultiplayerJoust,
                playCta: CallToActionTargetType.PlayGameMultiplayerJoust,
                nextLabel: "Phase 3 (Maelstrom)"));
            quest.phases.Add(BuildUnlockPhase(3, "Unlock Maelstrom",
                gateMode: GameModes.MultiplayerJoust,
                claimMode: GameModes.Tournament,
                playCta: CallToActionTargetType.PlayGameMaelstrom,
                nextLabel: "Phase 4 (Vessel Tour)"));
            quest.phases.Add(BuildPhase4());
            quest.phases.Add(BuildPhase5());

            EditorUtility.SetDirty(quest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = quest;
            EditorGUIUtility.PingObject(quest);
            Debug.Log($"[Quest] Created MainQuest at {questPath} with {quest.phases.Count} phases. " +
                      "Assign DialogueSets on the Dialogue nodes, then open FrogletTools ▸ Quest Graph Editor.");
        }

        // ── Phase builders ─────────────────────────────────────────────

        static QuestPhaseGraphSO BuildPhase0()
        {
            var g = NewPhase(0, "Onboarding & Crystal Capture",
                "Entry: first launch. FLIGHT SCHOOL — forced into freestyle, then: thumbsticks OUTWARD " +
                "= speed up, INWARD = slow down, movement stick = look around, LT+RT = drift, skim 10 " +
                "prisms (live counter; skims also boost), press B to exit — the exit happens ONLY on B, " +
                "never forced. Every correct action pulses a success haptic; every completed step is " +
                "recorded to UGS. Instruction beats reference keyed UI panels on QuestInstructionView " +
                "(speed_up, slow_down, look_around, drift, skim, exit_freestyle) — build each as a " +
                "CanvasGroup with the ControlIcons sprites. Then: Crystal Capture at intensity 2, " +
                "profile/maps tour + the intensity-4 rule, CC at intensity 3, social UI tour.");

            var intro = Add<QuestPlayIntroNode>(g, "Intro Cinematic");
            var enter = Add<QuestEnterFreestyleNode>(g, "Enter Freestyle (forced into flight)");

            var speedUpPrompt = Add<QuestShowInstructionNode>(g, "Prompt: Speed Up");
            speedUpPrompt.text = "Pull both thumbsticks OUTWARD to speed up!";
            speedUpPrompt.panelKey = "speed_up";
            speedUpPrompt.haptic = HapticType.ButtonPress;

            var waitSpeedUp = Add<QuestWaitForInputNode>(g, "Wait: Full Speed");
            waitSpeedUp.acceptedInputs = new List<InputEvents> { InputEvents.FullSpeedStraightAction };

            var slowDownPrompt = Add<QuestShowInstructionNode>(g, "Prompt: Slow Down");
            slowDownPrompt.text = "Pull both thumbsticks INWARD to slow down.";
            slowDownPrompt.panelKey = "slow_down";

            var waitSlowDown = Add<QuestWaitForInputNode>(g, "Wait: Minimum Speed");
            waitSlowDown.acceptedInputs = new List<InputEvents> { InputEvents.MinimumSpeedStraightAction };

            var lookPrompt = Add<QuestShowInstructionNode>(g, "Prompt: Look Around");
            lookPrompt.text = "Use the movement thumbstick to look around.";
            lookPrompt.panelKey = "look_around";

            var waitLook = Add<QuestWaitForInputNode>(g, "Wait: Look Around");
            waitLook.acceptedInputs = new List<InputEvents>
            {
                InputEvents.LeftStickAction, InputEvents.RightStickAction,
                InputEvents.OnlyLeftStickAction, InputEvents.OnlyRightStickAction,
                InputEvents.BothSticksAction,
            };

            var driftPrompt = Add<QuestShowInstructionNode>(g, "Prompt: Drift");
            driftPrompt.text = "Squeeze LT + RT together to drift!";
            driftPrompt.panelKey = "drift";

            var waitDrift = Add<QuestWaitForDriftNode>(g, "Wait: Drift");

            var skimPrompt = Add<QuestShowInstructionNode>(g, "Prompt: Skim");
            skimPrompt.text = "Skim close over prisms to charge your boost — skim 10!";
            skimPrompt.panelKey = "skim";

            var waitSkim = Add<QuestWaitForSkimNode>(g, "Wait: Skim 10 Prisms");
            waitSkim.targetSkims = 10;

            var exitPrompt = Add<QuestShowInstructionNode>(g, "Prompt: Exit Freestyle");
            exitPrompt.text = "Press B to head back to the menu.";
            exitPrompt.panelKey = "exit_freestyle";

            var waitB = Add<QuestWaitForInputNode>(g, "Wait: B Pressed");
            waitB.acceptedInputs = new List<InputEvents> { InputEvents.Button3Action };

            var exit = Add<QuestExitFreestyleNode>(g, "Force Exit Freestyle");
            exit.forceExit = true;

            var ctaArcade = Add<QuestHighlightCTANode>(g, "CTA: Arcade");
            ctaArcade.target = CallToActionTargetType.ArcadeMenu;
            ctaArcade.completionAction = UserActionType.ViewArcadeMenu;

            var lockModes = Add<QuestLockModesNode>(g, "Lock To Crystal Capture");
            lockModes.tutorialGame = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;

            var ctaCC2 = Add<QuestHighlightCTANode>(g, "CTA: Play CC (Intensity 2)");
            ctaCC2.target = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;
            ctaCC2.completionAction = UserActionType.PlayGame;

            var playedCC2 = Add<QuestWaitForGamePlayedNode>(g, "Wait: CC Played @2");
            playedCC2.filterByMode = true;
            playedCC2.expectedMode = GameModes.MultiplayerCrystalCapture;
            playedCC2.minIntensity = 2;

            var ctaProfile = Add<QuestHighlightCTANode>(g, "CTA: Profile Screen");
            ctaProfile.target = CallToActionTargetType.ProfileMenu;
            ctaProfile.completionAction = UserActionType.ViewProfileMenu;

            var mapsDialogue = Add<QuestDialogueNode>(g, "Maps Tour + Intensity-4 Rule (assign DialogueSet)");

            var ctaCC3 = Add<QuestHighlightCTANode>(g, "CTA: Play CC (Intensity 3)");
            ctaCC3.target = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;
            ctaCC3.completionAction = UserActionType.PlayGame;
            ctaCC3.dependencies = new List<CallToActionTargetType> { CallToActionTargetType.ArcadeMenu };

            var playedCC3 = Add<QuestWaitForGamePlayedNode>(g, "Wait: CC Played @3");
            playedCC3.filterByMode = true;
            playedCC3.expectedMode = GameModes.MultiplayerCrystalCapture;
            playedCC3.minIntensity = 3;

            var socialTour = Add<QuestShowInstructionNode>(g, "Social UI Tour (placeholder)");
            socialTour.text = "This is your crew hub — invite friends and fly together!";
            socialTour.minDisplaySeconds = 4f;
            socialTour.hideOnAdvance = true;

            var end = Add<QuestPhaseEndNode>(g, "Phase 0 Complete");

            Chain(g, intro, enter,
                speedUpPrompt, waitSpeedUp, slowDownPrompt, waitSlowDown, lookPrompt, waitLook,
                driftPrompt, waitDrift, skimPrompt, waitSkim, exitPrompt, waitB, exit,
                ctaArcade, lockModes, ctaCC2, playedCC2, ctaProfile, mapsDialogue,
                ctaCC3, playedCC3, socialTour, end);
            return Finish(g, intro);
        }

        static QuestPhaseGraphSO BuildUnlockPhase(int index, string phaseName,
            GameModes gateMode, GameModes claimMode, CallToActionTargetType playCta, string nextLabel)
        {
            var g = NewPhase(index, phaseName,
                $"Entry gate: {gateMode} reaches intensity tier 4 (its quest milestone). Guide to the " +
                $"profile screen, explain, wait for the player to CLAIM {claimMode} on the quest track " +
                "(unlocks to intensity 3), reward dialogue, then guide back to the arcade and play it. " +
                $"Ends → {nextLabel}.");

            var gate = Add<QuestWaitForIntensityNode>(g, $"Gate: {gateMode} Tier 4");
            gate.mode = gateMode;
            gate.intensityTier = 4;

            var ctaProfile = Add<QuestHighlightCTANode>(g, "CTA: Profile Screen");
            ctaProfile.target = CallToActionTargetType.ProfileMenu;
            ctaProfile.completionAction = UserActionType.ViewProfileMenu;

            var explain = Add<QuestDialogueNode>(g, "Profile Explainer (assign DialogueSet)");

            var claim = Add<QuestWaitForModeUnlockedNode>(g, $"Wait: Claim {claimMode}");
            claim.mode = claimMode;

            var reward = Add<QuestDialogueNode>(g, "Reward Reveal (assign Reward DialogueSet)");

            var ctaPlay = Add<QuestHighlightCTANode>(g, $"CTA: Play {claimMode}");
            ctaPlay.target = playCta;
            ctaPlay.completionAction = UserActionType.PlayGame;
            ctaPlay.dependencies = new List<CallToActionTargetType> { CallToActionTargetType.ArcadeMenu };

            var played = Add<QuestWaitForGamePlayedNode>(g, $"Wait: {claimMode} Played");
            played.filterByMode = true;
            played.expectedMode = claimMode;

            var end = Add<QuestPhaseEndNode>(g, $"Phase {index} Complete");

            Chain(g, gate, ctaProfile, explain, claim, reward, ctaPlay, played, end);
            return Finish(g, gate);
        }

        static QuestPhaseGraphSO BuildPhase4()
        {
            var g = NewPhase(4, "Vessel Unlock Tour",
                "Entry: after Maelstrom is played. Guide to the hangar, tour the vessel-unlock flow, " +
                "wait until the player unlocks a vessel (the hangar UI fires UserActionType.UnlockVessel), " +
                "reward dialogue. Ends → Phase 5 (finale).");

            var ctaHangar = Add<QuestHighlightCTANode>(g, "CTA: Hangar");
            ctaHangar.target = CallToActionTargetType.HangarMenu;
            ctaHangar.completionAction = UserActionType.ViewHangarMenu;

            var tour = Add<QuestDialogueNode>(g, "Vessel Unlock Tour (assign DialogueSet)");

            var unlocked = Add<QuestWaitForUserActionNode>(g, "Wait: Vessel Unlocked");
            unlocked.action = UserActionType.UnlockVessel;

            var reward = Add<QuestDialogueNode>(g, "Vessel Reward Reveal (assign Reward DialogueSet)");

            var end = Add<QuestPhaseEndNode>(g, "Phase 4 Complete");

            Chain(g, ctaHangar, tour, unlocked, reward, end);
            return Finish(g, ctaHangar);
        }

        static QuestPhaseGraphSO BuildPhase5()
        {
            var g = NewPhase(5, "Episodes & Beyond — Finale",
                "Entry: vessel unlocked. Guide to the episodes screen, closing dialogue ('you can also " +
                "buy new game modes'), then Quest End — the FTUE is complete and persisted to UGS.");

            var ctaEpisodes = Add<QuestHighlightCTANode>(g, "CTA: Episodes");
            ctaEpisodes.target = CallToActionTargetType.EpisodeMenu;
            ctaEpisodes.completionAction = UserActionType.ViewEpisodeMenu;

            var finale = Add<QuestDialogueNode>(g, "Finale Dialogue (assign DialogueSet)");

            var end = Add<QuestEndNode>(g, "FTUE Complete");

            Chain(g, ctaEpisodes, finale, end);
            return Finish(g, ctaEpisodes);
        }

        // ── Helpers ────────────────────────────────────────────────────

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets/FTUE", "DataContainer");
            if (!AssetDatabase.IsValidFolder(QuestsFolder))
                AssetDatabase.CreateFolder(RootFolder, "Quests");
            if (!AssetDatabase.IsValidFolder(PhasesFolder))
                AssetDatabase.CreateFolder(RootFolder, "Phases");
        }

        static QuestPhaseGraphSO NewPhase(int index, string phaseName, string notes)
        {
            var g = ScriptableObject.CreateInstance<QuestPhaseGraphSO>();
            g.graphId = $"MainQuest_Phase{index}";
            g.phaseName = phaseName;
            g.designerNotes = notes;
            string path = AssetDatabase.GenerateUniqueAssetPath($"{PhasesFolder}/MainQuest_Phase{index}.asset");
            AssetDatabase.CreateAsset(g, path);
            _nodeCursor = 0;
            return g;
        }

        static T Add<T>(QuestPhaseGraphSO graph, string name) where T : QuestNodeSO
        {
            var node = ScriptableObject.CreateInstance<T>();
            node.nodeId = Guid.NewGuid().ToString("N");
            node.name = name;
            node.displayName = name;
            node.graphPosition = new Vector2(60 + (_nodeCursor / 5) * 260, 40 + (_nodeCursor % 5) * 120);
            _nodeCursor++;
            AssetDatabase.AddObjectToAsset(node, graph);
            graph.nodes.Add(node);
            return node;
        }

        static void Chain(QuestPhaseGraphSO graph, params QuestNodeSO[] order)
        {
            for (int i = 0; i < order.Length - 1; i++)
                order[i].Outputs.Add(new QuestEdge(QuestPorts.Next, order[i + 1].nodeId));
        }

        static QuestPhaseGraphSO Finish(QuestPhaseGraphSO graph, QuestNodeSO entry)
        {
            graph.entryNode = entry;
            EditorUtility.SetDirty(graph);
            return graph;
        }
    }
}
#endif
