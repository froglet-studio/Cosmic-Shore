using System.Collections.Generic;
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
                 "The design's starting value: 40% of the form's own body in banked volume. Zero in the final form.")]
        public Vector4 BankToEvolve;
        [Tooltip("What one oasis stop eats in this form: the stage it must eat to evolve, over the design's 'two to three " +
                 "oases a stage'. The swarm moves on once it has eaten this much at the oasis it is at.")]
        [Min(1f)] public float MealVolume;
        [Tooltip("The cell's colours while the swarm wears this form. The cell eases into them at the moment the form is taken.")]
        public CellPalette Palette;
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
        public List<TandavaFormSpec> Forms = new();

        [Header("The cell changes with the swarm")]
        [Tooltip("Seconds the cell eases into a form's palette.")]
        [Min(0.1f)] public float TransitionSeconds = 3f;
        [Tooltip("How bright the cell blooms at the moment of a form change (0 = no bloom; 1 = doubles its brightness).")]
        [Min(0f)] public float TransitionFlash = 1.5f;
        [Tooltip("The palette the cell takes when the swarm escapes (the cycle ending too soon).")]
        public CellPalette EscapedPalette;
        [Tooltip("Seconds the cell eases back to its own colours when the pilots win.")]
        [Min(0.1f)] public float RestoreSeconds = 5f;

        [Header("Narration")]
        [TextArea] public string StartLine = "Something in the reef remembers the old shapes.";
        [TextArea] public string HeadingForExitLine = "Its last meal is behind it. Stop it before the membrane.";
        [TextArea] public string EscapedLine = "The cycle ends. Too soon.";
        [TextArea] public string WonLine = "The dance is broken. The reef keeps its turn.";

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
