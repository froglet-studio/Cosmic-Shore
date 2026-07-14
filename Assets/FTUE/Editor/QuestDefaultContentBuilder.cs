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
                "Reference map: see QUEST_GRAPH_TOOL.md. Dialogue lines are authored ON the nodes " +
                "(no DialogueSet assets — the panel is driven directly). New CTA targets " +
                "(ProfileMenu / EpisodeMenu / PlayGameMaelstrom) and user actions " +
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
                "Entry: first launch. FIRST BEAT = the camera transitions from the menu orbit to the " +
                "player vessel (the same blend as entering freestyle manually) as soon as the menu is " +
                "ready; instructions only appear after the blend completes. FLIGHT SCHOOL — thumbsticks OUTWARD " +
                "= speed up, INWARD = slow down, movement stick = look around, LT then RT = drift both ways, " +
                "skim 10 prisms (live counter; skims also boost), then tap the VOLUME button to exit — the " +
                "exit happens ONLY on the player's tap, never forced. During training the vessel HUD is " +
                "hidden and the action buttons (A/X/B/flip) are suppressed; every correct action pulses a " +
                "success haptic and every completed step is recorded to UGS. Instruction beats reference " +
                "keyed UI panels on QuestInstructionView (speed_up, slow_down, look_around, drift, skim, " +
                "exit_freestyle) — node text OVERWRITES the panel's TMP text. After exit: nav locks to the " +
                "Arcade button + captain dialogue, the arcade funnels to Crystal Capture @ intensity 1 " +
                "(2 players, 2 domains), then profile/maps tour + the intensity-4 rule, CC @ intensity 3, " +
                "funnel cleared, social UI tour.");

            var enter = Add<QuestEnterFreestyleNode>(g, "Camera → Player Vessel (enter freestyle)");

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
            driftPrompt.text = "Drift LEFT with LT, then RIGHT with RT!";
            driftPrompt.panelKey = "drift";

            var waitDrift = Add<QuestWaitForDriftNode>(g, "Wait: Drift Both Ways");
            waitDrift.requireBothDirections = true;

            var skimPrompt = Add<QuestShowInstructionNode>(g, "Prompt: Skim");
            skimPrompt.text = "Skim close over prisms to charge your boost — skim 10!";
            skimPrompt.panelKey = "skim";

            var waitSkim = Add<QuestWaitForSkimNode>(g, "Wait: Skim 10 Prisms");
            waitSkim.targetSkims = 10;

            var exitPrompt = Add<QuestShowInstructionNode>(g, "Prompt: Exit Via Volume Button");
            exitPrompt.text = "Training complete! Tap the VOLUME button to head back to the menu.";
            exitPrompt.panelKey = "exit_freestyle";

            // Passive: the volume/pause button IS the exit (it calls ToggleTransition) —
            // the node just waits for the menu blend to complete. Never forced.
            var exit = Add<QuestExitFreestyleNode>(g, "Wait: Player Taps Volume Button");
            exit.forceExit = false;

            var lockNav = Add<QuestLockNavigationNode>(g, "Lock Nav (Hangar + Profile)");

            var arcadeDialogue = Add<QuestDialogueNode>(g, "Dialogue: Go To The Arcade");
            arcadeDialogue.lines = new List<string>
            {
                "Nicely flown, pilot — you've got the basics down.",
                "Tap the ARCADE button below. Your first real match is waiting!",
            };

            var constraintsCC1 = Add<QuestSetArcadeConstraintsNode>(g, "Arcade Funnel: CC @ Intensity 1 · 2p · 2 domains");
            constraintsCC1.allowedMode = GameModes.MultiplayerCrystalCapture;
            constraintsCC1.forcedIntensity = 1;
            constraintsCC1.forcedPlayerCount = 2;
            constraintsCC1.forcedDomainCount = 2;

            var ctaArcade = Add<QuestHighlightCTANode>(g, "CTA: Arcade");
            ctaArcade.target = CallToActionTargetType.ArcadeMenu;
            ctaArcade.completionAction = UserActionType.ViewArcadeMenu;

            var ctaCC1 = Add<QuestHighlightCTANode>(g, "CTA: Play CC (Intensity 1)");
            ctaCC1.target = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;
            ctaCC1.completionAction = UserActionType.PlayGame;

            var playedCC1 = Add<QuestWaitForGamePlayedNode>(g, "Wait: CC Played @1");
            playedCC1.filterByMode = true;
            playedCC1.expectedMode = GameModes.MultiplayerCrystalCapture;
            playedCC1.minIntensity = 1;

            // The first game is done — lift the forced intensity IMMEDIATELY so the player
            // can pick any unlocked tier (1–3 by default; tier 4 stays progression-gated).
            // The arcade stays funneled to Crystal Capture until the funnel clears.
            var loosenFunnel = Add<QuestSetArcadeConstraintsNode>(g, "Arcade Funnel: CC only (intensities open)");
            loosenFunnel.allowedMode = GameModes.MultiplayerCrystalCapture;
            loosenFunnel.forcedIntensity = 0;
            loosenFunnel.forcedPlayerCount = 2;
            loosenFunnel.forcedDomainCount = 2;

            var unlockNav = Add<QuestLockNavigationNode>(g, "Unlock Nav (Hangar + Profile)");
            unlockNav.unlock = true;

            var profileDialogue = Add<QuestDialogueNode>(g, "Dialogue: Go To Profile");
            profileDialogue.lines = new List<string>
            {
                "Great flying out there, pilot — your first match is in the books!",
                "Tap the PROFILE button below. Your quest track is waiting for you.",
            };

            var ctaProfile = Add<QuestHighlightCTANode>(g, "CTA: Profile Screen");
            ctaProfile.target = CallToActionTargetType.ProfileMenu;
            ctaProfile.completionAction = UserActionType.ViewProfileMenu;

            var mapsDialogue = Add<QuestDialogueNode>(g, "Maps Tour + Intensity-4 Rule");
            mapsDialogue.lines = new List<string>
            {
                "This is your profile — every game mode and its intensity tiers live here.",
                "Tiers 1 to 3 are open right away. Reach INTENSITY 4 in a mode to unlock the next one on your quest track!",
            };

            var ctaCC3 = Add<QuestHighlightCTANode>(g, "CTA: Play CC (Intensity 3)");
            ctaCC3.target = CallToActionTargetType.PlayGameMultiplayerCrystalCapture;
            ctaCC3.completionAction = UserActionType.PlayGame;
            ctaCC3.dependencies = new List<CallToActionTargetType> { CallToActionTargetType.ArcadeMenu };

            var playedCC3 = Add<QuestWaitForGamePlayedNode>(g, "Wait: CC Played @3");
            playedCC3.filterByMode = true;
            playedCC3.expectedMode = GameModes.MultiplayerCrystalCapture;
            playedCC3.minIntensity = 3;

            var clearFunnel = Add<QuestSetArcadeConstraintsNode>(g, "Clear Arcade Funnel");
            clearFunnel.clearConstraints = true;

            var socialTour = Add<QuestShowInstructionNode>(g, "Social UI Tour (placeholder)");
            socialTour.text = "This is your crew hub — invite friends and fly together!";
            socialTour.minDisplaySeconds = 4f;
            socialTour.hideOnAdvance = true;

            var end = Add<QuestPhaseEndNode>(g, "Phase 0 Complete");

            Chain(g, enter,
                speedUpPrompt, waitSpeedUp, slowDownPrompt, waitSlowDown, lookPrompt, waitLook,
                driftPrompt, waitDrift, skimPrompt, waitSkim, exitPrompt, exit,
                lockNav, arcadeDialogue, constraintsCC1, ctaArcade, ctaCC1, playedCC1,
                loosenFunnel, unlockNav, profileDialogue, ctaProfile, mapsDialogue, ctaCC3, playedCC3,
                clearFunnel, socialTour, end);

            // Pacing: breathing room between beats (editable per arrow in the editor).
            SetDelay(enter, 1.5f);
            SetDelay(waitSpeedUp, 0.8f);
            SetDelay(waitSlowDown, 0.8f);
            SetDelay(waitLook, 0.8f);
            SetDelay(waitDrift, 0.8f);
            SetDelay(waitSkim, 1f);
            SetDelay(exit, 1f);
            SetDelay(playedCC1, 1f);
            SetDelay(mapsDialogue, 0.5f);
            SetDelay(playedCC3, 1f);

            return Finish(g, enter);
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

            var explain = Add<QuestDialogueNode>(g, "Profile Explainer");
            explain.lines = new List<string>
            {
                "Your quest track has something ready — the next game mode is waiting.",
                "Claim it, then show me what you've got out there!",
            };

            var claim = Add<QuestWaitForModeUnlockedNode>(g, $"Wait: Claim {claimMode}");
            claim.mode = claimMode;

            var reward = Add<QuestDialogueNode>(g, "Reward Reveal");
            reward.lines = new List<string>
            {
                $"{claimMode} is yours, pilot! Intensities 1–3 are open — dive in.",
            };

            var ctaPlay = Add<QuestHighlightCTANode>(g, $"CTA: Play {claimMode}");
            ctaPlay.target = playCta;
            ctaPlay.completionAction = UserActionType.PlayGame;
            ctaPlay.dependencies = new List<CallToActionTargetType> { CallToActionTargetType.ArcadeMenu };

            var played = Add<QuestWaitForGamePlayedNode>(g, $"Wait: {claimMode} Played");
            played.filterByMode = true;
            played.expectedMode = claimMode;

            var end = Add<QuestPhaseEndNode>(g, $"Phase {index} Complete");

            Chain(g, gate, ctaProfile, explain, claim, reward, ctaPlay, played, end);
            SetDelay(gate, 1f);
            SetDelay(claim, 0.5f);
            SetDelay(reward, 0.5f);
            SetDelay(played, 1f);
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

            var tour = Add<QuestDialogueNode>(g, "Vessel Unlock Tour");
            tour.lines = new List<string>
            {
                "Welcome to the hangar — every vessel flies a different way.",
                "Unlock a new one with the crystals you've earned!",
            };

            var unlocked = Add<QuestWaitForUserActionNode>(g, "Wait: Vessel Unlocked");
            unlocked.action = UserActionType.UnlockVessel;

            var reward = Add<QuestDialogueNode>(g, "Vessel Reward Reveal");
            reward.lines = new List<string>
            {
                "A fine ship! She'll serve you well out there.",
            };

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

            var finale = Add<QuestDialogueNode>(g, "Finale Dialogue");
            finale.lines = new List<string>
            {
                "One more thing — the Episodes hold new game modes you can pick up anytime.",
                "That's everything, pilot. The HyperSea is yours!",
            };

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

        /// <summary>Set the pacing delay on a node's 'next' edge (no-op if unwired).</summary>
        static void SetDelay(QuestNodeSO from, float seconds)
        {
            var edge = from.EdgeForPort(QuestPorts.Next);
            if (edge != null)
                edge.delaySeconds = seconds;
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
