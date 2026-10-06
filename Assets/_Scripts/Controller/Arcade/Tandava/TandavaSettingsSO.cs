using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One feeding ground on Tandava's route (world, the cell's frame).</summary>
    [System.Serializable]
    public struct TandavaOasisSpec
    {
        [Tooltip("The oasis centre. Its flora is planted here by the cell's own spawner (the Tandava flora configs' planting pens).")]
        public Vector3 Centre;
        [Tooltip("How far from the centre the oasis's flora grows. The swarm is AT the oasis within this plus the arrive margin.")]
        [Min(1f)] public float Radius;
    }

    /// <summary>One form the swarm takes, in order (Tandava's director names them; the swarm's scripted plan list holds them).</summary>
    [System.Serializable]
    public struct TandavaFormSpec
    {
        [Tooltip("What the HUD and the narrator call it.")]
        public string DisplayName;
        [Tooltip("Index of this form's body plan in the swarm config's ScriptedPlans.")]
        [Min(0)] public int PlanIndex;
        [Tooltip("The body must be this share of its full plan alive to evolve on.")]
        [Range(0.1f, 1f)] public float FillToEvolve;
        [Tooltip("Banked stomach volume the swarm must hold to evolve on, per element (x Charge, y Mass, z Space, w Time). " +
                 "The design's starting value: 40% of the form's own body in banked volume; the last EATING form's (the " +
                 "Bull's) is the ascension's offering, a whole body's worth. Zero in the dance and final forms.")]
        public Vector4 BankToEvolve;
        [Tooltip("What one oasis stop eats in this form: the stage it must eat to evolve, over the design's 'two to three " +
                 "oases a stage'. The swarm moves on once it has eaten this much at the oasis it is at.")]
        [Min(1f)] public float MealVolume;
        [Tooltip("The narrator's line when the swarm takes this form.")]
        [TextArea] public string Line;
    }

    /// <summary>
    /// Every number Tandava runs on except its end-condition target (the final form's break threshold lives in
    /// <see cref="EndConditionOverridesSO"/>, FrogletTools > Game Modes > End Game Conditions). Authored by
    /// Tools/Build/author_tandava_assets.py, whose model of the route (Tools/Build/swarm_core_harness, mode `tandava`)
    /// these numbers came from; hand edits are drift. Assets/_Scripts/Controller/Arcade/TANDAVA.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Game Modes/Tandava Settings", fileName = "TandavaSettings")]
    public class TandavaSettingsSO : ScriptableObject
    {
        [Header("The swarm")]
        [Tooltip("The swarm config the mode directs. Its ScriptedPlans are the forms below; the cell's spawn profile hatches it.")]
        public SwarmFaunaConfigSO SwarmConfig;

        [Header("The route (world, the cell's frame)")]
        [Tooltip("Where the swarm hatches - one end of the course.")]
        public Vector3 StartPoint = new(-2000f, 0f, 0f);
        [Tooltip("Which way it first faces (toward the exit).")]
        public Vector3 StartHeading = Vector3.right;
        [Tooltip("The feeding grounds, in the order the swarm visits them.")]
        public List<TandavaOasisSpec> Oases = new();
        [Tooltip("A point on the exit membrane. Crossing it is the pilots' loss.")]
        public Vector3 ExitPoint = new(2000f, 0f, 0f);
        [Tooltip("The exit membrane's normal, pointing OUT of the course.")]
        public Vector3 ExitNormal = Vector3.right;

        [Header("Director")]
        [Tooltip("The swarm is AT an oasis when its anchor is within the oasis radius plus this.")]
        [Min(0f)] public float ArriveMargin = 60f;
        [Tooltip("It leaves an oasis once its stomach is this full, whatever its meal.")]
        [Range(0.1f, 1f)] public float LeaveWhenStomachFill = 0.98f;
        [Tooltip("It leaves an oasis it has taken no bite from for this long (grazed bare or burned around it).")]
        [Min(1f)] public float GiveUpSeconds = 8f;
        [Tooltip("It never sits at one oasis longer than this - the race keeps moving.")]
        [Min(1f)] public float MaxFeedSeconds = 40f;
        [Tooltip("The final form's break threshold arms once it has held this share of its full plan (so the commit itself " +
                 "can never read as broken).")]
        [Range(0f, 1f)] public float BreakArmFraction = 0.6f;
        [Tooltip("Seconds between the server's checks that an oasis still has edible flora (an oasis with none is denied).")]
        [Min(0.2f)] public float DenialCheckSeconds = 1f;

        [Header("Forms")]
        [Tooltip("In order. The last two are the ascension: the DANCE form (the Lord of the Dance, taken at the dance " +
                 "ground) and the FINAL form (the Winged Lion, taken when the drum stops) - the only one the exit lets through.")]
        public List<TandavaFormSpec> Forms = new();

        [Header("The sealed exit")]
        [Tooltip("A form that is not the final one is 'at the membrane' within this of the exit plane: the membrane holds, " +
                 "and the narrator says so.")]
        [Min(0f)] public float SealMargin = 60f;
        [Tooltip("Every peer holds a non-final form's body at least this far inside the exit plane (a small rigid nudge " +
                 "back - the membrane holding, never a kill).")]
        [Min(0f)] public float SealHold = 60f;

        [Header("Foraging and starvation")]
        [Tooltip("With nothing left to eat anywhere, the swarm waits this far inside the exit plane, pressing on the " +
                 "membrane.")]
        [Min(0f)] public float StarveStandoff = 140f;
        [Tooltip("Pressing on the membrane with nothing to eat this long, it has starved (the pilots win). Its body sheds " +
                 "by its own metabolism meanwhile (the swarm config's StarvationSeconds / ShedIntervalSeconds).")]
        [Min(1f)] public float StarvingSeconds = 40f;
        [Tooltip("A starving swarm whose body falls below this share of its form's full plan has starved, whatever the clock.")]
        [Range(0f, 1f)] public float StarvedBelowFraction = 0.15f;

        [Header("The ascension (the dance ground, the drum, the ring of fire)")]
        [Tooltip("Where the banked Bull goes to take the dance form (world, the cell's frame). Beside the course, clear of " +
                 "every oasis by the ring's reach, so the dance never stands in the flora.")]
        public Vector3 DancePoint = new(1075f, 0f, 400f);
        [Tooltip("The swarm is at the dance ground within this of it.")]
        [Min(1f)] public float DanceArrive = 70f;
        [Tooltip("How long the drum runs: the pilots' window to break the ring of fire before the final form is taken.")]
        [Min(1f)] public float DrumSeconds = 30f;
        [Tooltip("Flames in the ring of fire. How many must go out lives in EndConditionOverrides (tandavaFlamesToBreak).")]
        [Range(1, 31)] public int FlameCount = 12;
        [Tooltip("The ring's centre from the dance ground, in the swarm's body axes at the moment the dance begins (x along " +
                 "its heading, y up, z side), world units. Authored from the dance plan's baked ring (tandava_plans.ring_of).")]
        public Vector3 FlameRingCentreLocal;
        [Tooltip("The ring of fire's radius (world): the dance plan's ring at this swarm's density and unit scale.")]
        [Min(1f)] public float FlameRingRadius = 133f;
        [Tooltip("Each flame's mouth (world). The drawn ring and the crossing test share it.")]
        [Min(1f)] public float FlameMouthRadius = 24f;
        [Tooltip("The radius the attendant packs patrol at (world): each flame's GUARD POST sits there, straight out from it.")]
        [Min(1f)] public float GuardPostRadius = 167f;
        [Tooltip("A flame is GUARDED while at least GuardMembers of the swarm's GuardElement members are within this of its " +
                 "guard post (world). A guarded flame does not go out when threaded.")]
        [Min(1f)] public float GuardRadius = 40f;
        [Min(1)] public int GuardMembers = 6;
        [Tooltip("The attendants' element: the dance plan's packs are its Time units.")]
        public Element GuardElement = Element.Time;
        [Tooltip("A guarded flame's drawn ring gutters to this share of its mouth (its crossing test is off meanwhile).")]
        [Range(0.05f, 1f)] public float FlameGutter = 0.35f;
        [Tooltip("Seconds a flame takes to bloom in, and to wither when it goes out.")]
        [Min(0.05f)] public float FlameBloomSeconds = 1.2f;
        [Min(0.05f)] public float FlameOutSeconds = 0.6f;
        [Tooltip("Fastest a pilot moves (world units/s): a step longer than this in a frame is a respawn or a teleport, " +
                 "and threads no flame.")]
        [Min(1f)] public float MaxPlausibleSpeed = 1500f;

        [Header("The cell changes for the dance alone")]
        [Tooltip("The cell's colours from the moment the Bull rises into the Lord of the Dance until the dance ends. Every " +
                 "other form, and every other moment, the cell keeps its own colours.")]
        public CellPalette AscensionPalette;
        [Tooltip("Seconds the cell eases into the dance's palette.")]
        [Min(0.1f)] public float TransitionSeconds = 3f;
        [Tooltip("How bright the cell blooms at the moment of the rise (0 = no bloom; 1 = doubles its brightness).")]
        [Min(0f)] public float TransitionFlash = 1.5f;
        [Tooltip("Seconds the cell eases back to its own colours when the dance ends (the drum stops, or it is broken).")]
        [Min(0.1f)] public float RestoreSeconds = 5f;

        [Header("Narration")]
        [TextArea] public string StartLine = "Something in the reef remembers the old shapes.";
        [Tooltip("The route is eaten: it forages what the pilots left.")]
        [TextArea] public string HeadingForExitLine = "The route is eaten. Now it forages what you left.";
        [TextArea] public string StarvingLine = "Nothing left to eat. It presses on the sealed membrane, starving.";
        [TextArea] public string ReadyToDanceLine = "The Bull is full. It turns for the dance ground.";
        [TextArea] public string SealedLine = "The membrane holds. Only the last form may pass.";
        [Tooltip("{0} = flames out, {1} = flames to break the dance.")]
        [TextArea] public string FirstFlameLine = "A flame is out. Put out {1} and the dance is broken.";
        [Tooltip("{0} = flames out, {1} = flames to break the dance.")]
        [TextArea] public string LastFlameLine = "{0} of {1}. One flame more!";
        [TextArea] public string EscapedLine = "The cycle ends. Too soon.";
        [TextArea] public string WonLine = "The swarm is gone. The reef keeps its turn.";
        [TextArea] public string DanceBrokenLine = "The dance is broken. The reef keeps its turn.";

        [Header("Clients follow the server's swarm")]
        [Tooltip("Every peer runs its own swarm (fauna are client-local); a client nudges its body toward the server's when " +
                 "they are this far apart (world units).")]
        [Min(0f)] public float NudgeThreshold = 25f;
        [Tooltip("Share of the gap a client closes per published tick.")]
        [Range(0f, 1f)] public float NudgeFraction = 0.2f;
        [Tooltip("The most a client moves its body in one tick (keep it small: a nudge is a rigid move).")]
        [Min(0f)] public float MaxNudge = 6f;
    }
}
