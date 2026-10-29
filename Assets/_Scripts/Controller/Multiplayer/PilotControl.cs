#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CosmicShore.Utility;
using CosmicShore.Utility.AITraining;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Who flies which hull, for testing (the Third Eye's per-pilot <b>You / AI</b> toggle, Yash 2026-10-15): hand your
    /// own hull to the AI and watch, take over ANY AI-flown hull on either team, or leave every hull to the AI.
    ///
    /// <para><b>Built from the two paths the game already has, nothing new:</b></para>
    /// <list type="bullet">
    /// <item><b>AI holds your hull</b> is the menu freestyle hand-back (<c>MenuCrystalClickHandler</c>): input paused,
    /// <see cref="IVessel.ToggleAIPilot"/> on; plus the mode's AI steering (<see cref="IAIPilotArming"/>, every gate
    /// race) and the mode's AI configuration (<see cref="ServerPlayerVesselInitializerWithAI.ConfigureAIPilotForMode"/>),
    /// so the hull races like a seat instead of seeking crystals.</item>
    /// <item><b>You fly another hull</b> is the arena pilot swap (<see cref="PilotSwap"/>): the two Players exchange
    /// their live hulls, the AI keeps flying the one you left with its own steering. Score follows the PILOT, so what
    /// you earn in a rival's hull is credited to you.</item>
    /// </list>
    ///
    /// <para><b>Compiled into the editor and development builds only</b> (fair play, Threat Board T6;
    /// Docs/AntiCheat/README.md §2): in a release build a modded host could otherwise call it to sit in any hull.
    /// Its two callers, <c>HumanFlightRecorder</c> and the Third Eye, are guarded the same way.</para>
    ///
    /// <para><b>Host only, and a test tool.</b> The arena swap stays the player-facing feature (same team, AI teammates
    /// only, refused server-side otherwise); this skips those two rules so a tester can sit in any AI hull. Every call
    /// runs on the server, which owns every AI hull, and the swap itself replicates to every peer as the arena swap
    /// does. A hull handed to the AI in a mode without <see cref="IAIPilotArming"/> flies the platform autopilot's own
    /// seeking. A human's own Squirrel handed to the AI flies the platform autopilot on the race line, not the Skim Race
    /// pilot (that pilot stands down on a human's hull by design).</para>
    /// </summary>
    public static class PilotControl
    {
        /// <summary>True when this machine may move pilots: the server (the editor as host, or solo).</summary>
        public static bool CanControl => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        /// <summary>
        /// Is <paramref name="human"/>'s hull being flown by the AI right now? The AI trainer counts: it switches the
        /// hull's AIPilot off and writes the sticks itself, so without it a trainer match read as a person's flight
        /// (HumanFlightRecorder stored seven of Froglet Compute's first run as hand-flown, 2026-10-28).
        /// </summary>
        public static bool IsHeldByAI(Player human) =>
            human && human.Vessel is Component hull && hull &&
            ((hull.TryGetComponent(out SkimRacePilot skim) && skim.FlyingForHuman) ||
             (hull.TryGetComponent(out AIPilot pilot) && pilot.AutoPilotEnabled) ||
             IsHeldByTrainer(human));

        /// <summary>Is the AI trainer (<see cref="TrainingPilot"/>, an AI Training run) flying this hull's episode?</summary>
        public static bool IsHeldByTrainer(Player human) =>
            human && human.Vessel is Component hull && hull &&
            hull.TryGetComponent(out TrainingPilot trainer) && trainer.isActiveAndEnabled && trainer.EpisodeActive;

        const string TrainerRefusal = "the AI trainer is flying this hull (an AI Training run): stop the run first";

        /// <summary>
        /// Let the AI fly <paramref name="human"/>'s hull (<paramref name="aiFlies"/> true), or give it back to them.
        /// </summary>
        public static bool TrySetAIFlies(Player human, bool aiFlies, GameDataSO gameData, out string refusal)
        {
            if (!TryCheckHuman(human, out refusal)) return false;
            // Handing the hull to the AIPilot would start it writing the sticks against the trainer, every frame.
            if (IsHeldByTrainer(human)) { refusal = TrainerRefusal; return false; }
            var hull = human.Vessel;
            if (hull is not Component c || !c.TryGetComponent(out AIPilot pilot))
            {
                refusal = $"{hull.VesselStatus.VesselType} has no AIPilot";
                return false;
            }
            if (aiFlies == IsHeldByAI(human)) return true;

            if (aiFlies)
            {
                // A hull the Skim Race pilot flew before a human took it was left with its AIPilot disabled
                // (SkimRacePilot.SuppressOtherPilots); its steering runs in Update, so turn it back on.
                pilot.enabled = true;
                if (gameData != null) ServerPlayerVesselInitializerWithAI.ConfigureAIPilotForMode(pilot, gameData);
                ArmModeSteering(human);
                human.InputController.SetPause(true);
                hull.ToggleAIPilot(true);
                // A Squirrel in Skim Race or Regatta flies the racing pilot, as its AI seats do (it stands the platform
                // autopilot down itself). Without it the handed-over Squirrel flew ring waypoints straight through the
                // rails (Regatta playtest 2026-10-15).
                if (gameData != null)
                {
                    var skim = SkimRaceAIDeployment.TryInstall(c.gameObject, hull, gameData);
                    if (skim) skim.FlyingForHuman = true;
                }
            }
            else
            {
                if (c.TryGetComponent(out SkimRacePilot skim)) skim.FlyingForHuman = false;
                hull.ToggleAIPilot(false);
                hull.VesselStatus.ActionHandler.ReleaseHeldInputs();
                human.InputController.SetPause(false);
            }

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch,
                $"[PilotControl] {human.Name}'s {hull.VesselStatus.VesselType}: {(aiFlies ? "the AI flies it" : "back to the player")}.");
            return true;
        }

        /// <summary>
        /// <paramref name="human"/> takes over <paramref name="target"/>'s hull, on either team; the AI flies the hull
        /// they leave. <paramref name="target"/> must be an AI pilot (another human's hull is theirs).
        /// </summary>
        public static bool TryTakeOver(Player human, Player target, out string refusal)
        {
            if (!TryCheckHuman(human, out refusal)) return false;
            if (!target || target == human)               { refusal = "pick another pilot's hull"; return false; }
            if (!target.NetIsAI.Value)                    { refusal = $"{target.Name}'s hull is flown by a human"; return false; }
            if (target.Vessel is not Object t || !t)      { refusal = $"{target.Name} has no hull"; return false; }
            if (IsHeldByTrainer(human) || IsHeldByTrainer(target)) { refusal = TrainerRefusal; return false; }

            // If the AI was holding the hull being left, the player's input was paused for it. The swap turns the AI
            // on for that hull again, under its new pilot; the player's own sticks must be live for the new one.
            human.InputController.SetPause(false);
            // Neither hull is a human's hull held for them any more: the left one becomes the AI's own seat, and the
            // taken one is the human's to fly. A Skim Race pilot still flying "for a human" would fight their sticks.
            ReleaseHeldFor(human.Vessel);
            ReleaseHeldFor(target.Vessel);
            human.ExecutePilotSwapServer(target);
            return true;
        }

        static void ReleaseHeldFor(IVessel hull)
        {
            if (hull is Component c && c && c.TryGetComponent(out SkimRacePilot skim)) skim.FlyingForHuman = false;
        }

        static bool TryCheckHuman(Player human, out string refusal)
        {
            refusal = null;
            if (!CanControl)                              { refusal = "only the host can change who flies a hull"; return false; }
            if (!human || human.NetIsAI.Value)            { refusal = "no human pilot"; return false; }
            if (human.Vessel is not Object v || !v)       { refusal = "you have no hull"; return false; }
            return true;
        }

        static void ArmModeSteering(Player human)
        {
            foreach (var controller in Object.FindObjectsByType<MiniGameControllerBase>(FindObjectsSortMode.None))
                if (controller is IAIPilotArming arming && arming.ArmAIPilot(human))
                    return;
        }
    }
}
#endif
