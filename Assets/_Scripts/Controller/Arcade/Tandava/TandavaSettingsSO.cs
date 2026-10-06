using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One VARIANT of a form: a match draws one per form, so no two matches meet the same animal.</summary>
    [System.Serializable]
    public struct TandavaVariantSpec
    {
        [Tooltip("What the HUD calls this variant (\"Seven-Headed Serpent\").")]
        public string DisplayName;
        [Tooltip("Its travel plan's index in the swarm config's ScriptedPlans.")]
        [Min(0)] public int PlanIndex;
        [Tooltip("Its feed twin's index in ScriptedPlans: the same members re-arranged to eat, its plates out round the " +
                 "mouth as danger guards. -1 for a form that does not eat (the dance).")]
        public int FeedPlanIndex;
        [Tooltip("Its mouth in the TRAVEL pose (world units from the body's centre, along its axes: x forward, y up, z " +
                 "side) - how it lines its head up on a plant. Baked by Tools/Build/tandava_plans.py.")]
        public Vector3 Mouth;
        [Tooltip("Its mouth in the FEED pose: where the plant must sit while it eats.")]
        public Vector3 FeedMouth;
        [Tooltip("The dance form only: the halo's centre from the body's centre, in its axes (world).")]
        public Vector3 HaloCentre;
    }

    /// <summary>One form the swarm takes, in order (Tandava's director names them; the swarm's scripted plans hold them).</summary>
    [System.Serializable]
    public struct TandavaFormSpec
    {
        [Tooltip("What the narrator calls the form (its variants carry the names the HUD shows).")]
        public string DisplayName;
        [Tooltip("Eater: eats and banks its way on. Dance: the ascension - stands in its halo. Final: eats, and its bank " +
                 "is the feast that completes the cycle.")]
        public TandavaFormRole Role;
        [Tooltip("The body must be this share of its full plan to move on.")]
        [Range(0.1f, 1f)] public float FillToEvolve;
        [Tooltip("The bank it must hold to move on, as a share of its stomach (every element together). The banks RISE " +
                 "form by form, so what one form carries over never skips the next; all stay under the stomach fill at " +
                 "which a meal ends full (a full stomach stops grazing). 0 for the dance.")]
        [Range(0f, 0.95f)] public float BankShare;
        [Tooltip("What one meal eats (flora volume): it leaves a plant once it has eaten this much there.")]
        [Min(1f)] public float MealVolume;
        [Tooltip("The narrator's line when the swarm takes this form ({0} = the variant's name).")]
        [TextArea] public string Line;
        [Tooltip("The variants a match draws from (one each match).")]
        public List<TandavaVariantSpec> Variants;
    }

    /// <summary>The narrator's lines, sent as an index (TandavaController.Narrate_ClientRpc) - every peer has the text.</summary>
    public enum TandavaLine
    {
        Start = 0,
        /// <summary>A: the form taken.</summary>
        Form = 1,
        Feeding = 2,
        MealBroken = 3,
        Rising = 4,
        HaloLit = 5,
        /// <summary>A: rings broken, B: rings to break.</summary>
        FirstHalo = 6,
        LastHalo = 7,
        Completed = 8,
        Won = 9,
        DanceBroken = 10,
        HeldOff = 11,
    }

    /// <summary>
    /// Every number Tandava runs on except its end-condition targets (the shatter threshold and how many halo rings break
    /// the dance live in <see cref="EndConditionOverridesSO"/>, FrogletTools > Game Modes > End Game Conditions).
    /// Authored by Tools/Build/author_tandava_assets.py, which bakes the plans and asserts these against the harness
    /// (Tools/Build/swarm_core_harness, mode `tandava`); hand edits are drift. Assets/_Scripts/Controller/Arcade/TANDAVA.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/Game Modes/Tandava Settings", fileName = "TandavaSettings")]
    public class TandavaSettingsSO : ScriptableObject
    {
        [Header("The swarm")]
        [Tooltip("The swarm config the mode directs. Its ScriptedPlans are every form's variants and feed twins; the cell's " +
                 "spawn profile hatches it.")]
        public SwarmFaunaConfigSO SwarmConfig;

        [Header("The arena (the cell's frame - a CLOSED cell: the membrane is the creature's wall)")]
        [Tooltip("Where the swarm hatches, whole, as its first form.")]
        public Vector3 HatchPoint = new(-650f, 0f, 0f);
        [Tooltip("Which way it first faces.")]
        public Vector3 HatchHeading = Vector3.right;

        [Header("Forms")]
        [Tooltip("In order: the Great Serpent, the Many-Headed Serpent, the Lord of the Dance (the ascension), the Sea Lion. " +
                 "Exactly four; each draws one of its variants per match.")]
        public List<TandavaFormSpec> Forms = new();

        [Header("The director")]
        [Tooltip("Every dial the director runs on - threat and mood, the speed and turn levers, where to eat, feeding, the " +
                 "shatter, the ascension, the clock. The SAME class the harness runs (TandavaDirectorSettings): each field's " +
                 "doc comment there is its tooltip. The halo counts are overwritten from EndConditionOverrides.")]
        public TandavaDirectorSettings Director = new();

        [Header("Food")]
        [Tooltip("Flora volume one edible plant prism is worth - how the director weighs a plant (its live prisms x this). " +
                 "The Borromean Mass leaf.")]
        [Min(0.01f)] public float FoodPerPrism = 72.9f;
        [Tooltip("Seconds between the server's surveys of the cell's flora.")]
        [Min(0.1f)] public float FoodCheckSeconds = 0.5f;

        [Header("The halo (the ascension's switches)")]
        [Tooltip("The halo's radius (world): the dance plan's baked halo at this swarm's density and unit scale.")]
        [Min(1f)] public float HaloRadius = 167f;
        [Tooltip("Each ring's mouth (world). The drawn ring and the crossing test share it.")]
        [Min(1f)] public float HaloMouthRadius = 24f;
        [Tooltip("The radius the attendant packs patrol at (world): each ring's GUARD POST sits there, straight out from it.")]
        [Min(1f)] public float GuardPostRadius = 202f;
        [Tooltip("A ring is GUARDED while at least GuardMembers of the swarm's GuardElement members are within this of its " +
                 "post (world). A guarded ring does not break when threaded.")]
        [Min(1f)] public float GuardRadius = 40f;
        [Min(1)] public int GuardMembers = 6;
        [Tooltip("The attendants' element: the dance plan's packs are its Time units.")]
        public Element GuardElement = Element.Time;
        [Tooltip("A guarded ring's drawn mouth dims to this share (its crossing test is off meanwhile).")]
        [Range(0.05f, 1f)] public float HaloGuardScale = 0.35f;
        [Tooltip("Seconds a ring takes to bloom in, and to wither when it breaks.")]
        [Min(0.05f)] public float HaloBloomSeconds = 1.2f;
        [Min(0.05f)] public float HaloBreakSeconds = 0.6f;
        [Tooltip("Fastest a pilot moves (world units/s): a step longer than this in a frame is a respawn and threads nothing.")]
        [Min(1f)] public float MaxPlausibleSpeed = 1500f;

        [Header("The gold burst (no fire anywhere: the mode's effect is gold prism debris)")]
        [Tooltip("Gold shards thrown from the creature's members when it takes a form.")]
        [Min(0)] public int FormBurstShards = 64;
        [Tooltip("Gold shards thrown where a halo ring stood when it breaks.")]
        [Min(0)] public int HaloBurstShards = 18;
        [Tooltip("How fast the shards fly (world units/s) and how big they are.")]
        [Min(0f)] public float BurstSpeed = 70f;
        [Min(0.05f)] public float BurstScale = 3f;
        [Tooltip("The gold light over a burst: how long it is reported, how far it reaches, how bright (0..1).")]
        [Min(0f)] public float FlashSeconds = 0.6f;
        [Min(1f)] public float FlashRadius = 260f;
        [Range(0f, 1f)] public float FlashStrength = 0.8f;

        [Header("The cell glows for the dance alone")]
        [Tooltip("The cell's colours from the moment the creature rises into the Lord of the Dance until the dance ends. " +
                 "Every other form, and every other moment, the cell keeps its own colours. Gold, never fire.")]
        public CellPalette AscensionPalette;
        [Tooltip("Seconds the cell eases into the dance's palette.")]
        [Min(0.1f)] public float TransitionSeconds = 3f;
        [Tooltip("How bright the cell blooms at the moment of the rise (0 = no bloom; 1 = doubles its brightness).")]
        [Min(0f)] public float TransitionFlash = 1.2f;
        [Tooltip("Seconds the cell eases back to its own colours when the dance ends.")]
        [Min(0.1f)] public float RestoreSeconds = 5f;

        [Header("Narration")]
        [TextArea] public string StartLine = "Something in the reef remembers the old shapes.";
        [Tooltip("Said the first time it feeds.")]
        [TextArea] public string FeedingLine = "It is feeding - its guards are out round its mouth. Strike the body.";
        [TextArea] public string MealBrokenLine = "Its meal is broken. It bolts.";
        [TextArea] public string RisingLine = "It has eaten enough. It rises into the Lord of the Dance.";
        [TextArea] public string HaloLitLine = "The halo is lit. Break the rings before the drum stops.";
        [Tooltip("{0} = rings broken, {1} = rings to break the dance.")]
        [TextArea] public string FirstHaloLine = "A ring is broken. Break {1} and the dance falls.";
        [Tooltip("{0} = rings broken, {1} = rings to break the dance.")]
        [TextArea] public string LastHaloLine = "{0} of {1}. One more ring!";
        [TextArea] public string CompletedLine = "The Sea Lion has fed. The cycle is complete.";
        [TextArea] public string WonLine = "Its body is broken. The reef keeps its turn.";
        [TextArea] public string DanceBrokenLine = "The dance is broken. The reef keeps its turn.";
        [TextArea] public string HeldOffLine = "Time. You held it off - the cycle is unfinished.";

        [Header("HUD words (the goal rows, top left)")]
        public string RoamingLabel = "Roaming";
        public string WaryLabel = "Wary";
        public string FleeingLabel = "Fleeing";
        public string FeedingLabel = "Feeding - strike the body";
        public string RisingLabel = "Rising";
        public string DancingLabel = "Dancing - break the halo";
        public string FeastLabel = "the last feast";
        public string HaloLabel = "Halo rings broken";
        public string DrumLabel = "Drum";
        public string TimeLabel = "Time left";
        public string BodyFormat = "Body {0}%";

        [Header("Clients follow the server's swarm")]
        [Tooltip("Every peer runs its own swarm (fauna are client-local); a client nudges its body toward the server's when " +
                 "they are this far apart (world units).")]
        [Min(0f)] public float NudgeThreshold = 25f;
        [Tooltip("Share of the gap a client closes per published tick.")]
        [Range(0f, 1f)] public float NudgeFraction = 0.2f;
        [Tooltip("The most a client moves its body in one tick (keep it small: a nudge is a rigid move).")]
        [Min(0f)] public float MaxNudge = 8f;

        [Header("The AI pilots")]
        [Tooltip("While the swarm feeds, bots strike its body this far BEHIND its centre (world), clear of the guards round " +
                 "its mouth.")]
        [Min(0f)] public float AiFlankBack = 70f;

        /// <summary>The narrator's line <paramref name="line"/>, with {0}/{1} filled from <paramref name="a"/>/<paramref name="b"/>
        /// - except a form's line, whose {0} is <paramref name="name"/> (the variant the match drew).</summary>
        public string LineText(TandavaLine line, int a, int b, string name = null)
        {
            string text = line switch
            {
                TandavaLine.Start => StartLine,
                TandavaLine.Form => a >= 0 && a < Forms.Count ? Forms[a].Line : "",
                TandavaLine.Feeding => FeedingLine,
                TandavaLine.MealBroken => MealBrokenLine,
                TandavaLine.Rising => RisingLine,
                TandavaLine.HaloLit => HaloLitLine,
                TandavaLine.FirstHalo => FirstHaloLine,
                TandavaLine.LastHalo => LastHaloLine,
                TandavaLine.Completed => CompletedLine,
                TandavaLine.Won => WonLine,
                TandavaLine.DanceBroken => DanceBrokenLine,
                TandavaLine.HeldOff => HeldOffLine,
                _ => "",
            };
            if (line == TandavaLine.Form) return (text ?? "").Replace("{0}", name ?? "");
            return (text ?? "").Replace("{0}", a.ToString()).Replace("{1}", b.ToString());
        }
    }
}
