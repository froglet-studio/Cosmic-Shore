using System.Collections.Generic;
using CosmicShore.Utility;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number the Angler's command system reads (design: <c>R_VesselActions/ANGLER.md</c>). The ARTIFACT owns them:
    /// the Angler Command Lab (<c>Docs/Studios/AnglerCommandLab.html</c>) decides them and
    /// <c>Tools/Build/studio_to_unity.py --vessel Angler</c> writes them here (map: <c>Tools/Build/studio_to_unity/angler.json</c>,
    /// <c>--check</c> for drift). Each field is named after the lab's key, its tooltip is the lab's label, and its default is
    /// the lab's value. Tune in the lab, record the decision, clone; never tune these fields in Unity.
    /// </summary>
    [CreateAssetMenu(fileName = "AnglerConfig", menuName = "ScriptableObjects/Vessel Actions/Angler Config")]
    public sealed class AnglerConfigSO : ScriptableObject
    {
        [Header("Lure (the lab's Lure tab)")]
        [Tooltip("Esca distance (u): where the lure light hangs, ahead of the nose.")]
        [SerializeField, Min(0f)] float lureAhead = 14f;
        [Tooltip("Speed while luring (u/s): holding the lure slows the Angler to this; 0 = it stops. Lured fauna swim 16-38 u/s, so a hull at cruise outruns its own catch. Boost still overrides.")]
        [SerializeField, Min(0f)] float lureCruise = 0f;
        [Tooltip("Lure reach (u): wild fauna within this of the esca turn toward it.")]
        [SerializeField, Min(0f)] float lureRadius = 70f;
        [Tooltip("Hold radius (u): inside this the creature is held and taming fills.")]
        [SerializeField, Min(0f)] float tameRadius = 22f;
        [Tooltip("Time to tame (s): seconds a creature must be held.")]
        [SerializeField, Min(0.01f)] float tameTime = 2.5f;
        [Tooltip("Taming lost (/s): how fast a half-tamed creature forgets you outside the hold.")]
        [SerializeField, Min(0f)] float tameDecay = 0.6f;
        [Tooltip("Leash capacity: most creatures you can lead at once.")]
        [SerializeField, Min(0)] int squadCap = 24;
        [Tooltip("Leash tow x: a following creature may swim this many times its own max speed to keep formation. Attacks always run at the creature's own speeds.")]
        [SerializeField, Min(1f)] float leashTow = 1.6f;
        [Tooltip("School ring behind (u): where the squad's ring sits behind the hull.")]
        [SerializeField] float formBack = 6f;
        [Tooltip("Tadpole turn (/s): how sharply a leashed Tadpole turns toward its goal (a Boid steers by a velocity lerp; the lab's tadTurn).")]
        [SerializeField, Min(0f)] float tadTurn = 4f;

        [Header("Command: the Sling painter (the lab's Command tab)")]
        [Tooltip("Never target my prisms: an order's target list skips your own prisms. A mouthful can still eat them where the cell's diet allows it.")]
        [SerializeField] bool spareOwn = true;
        [Tooltip("Brush reach (u): how far the paint brush and the eraser reach.")]
        [SerializeField, Min(1f)] float paintRange = 3200f;
        [Tooltip("Brush half-angle (deg): the paint brush at a full pull of RT. A light squeeze paints a narrower line.")]
        [SerializeField, Range(0.5f, 45f)] float paintCone = 8f;
        [Tooltip("Light-squeeze brush: share of the full brush a just-past-halfway squeeze paints with (and the same for the eraser). 1 = the trigger depth does not matter.")]
        [SerializeField, Range(0.05f, 1f)] float brushMin = 0.35f;
        [Tooltip("Eraser half-angle (deg): the eraser (LT) at a full pull. Narrower than the brush, so it can pick a neighbour out.")]
        [SerializeField, Range(0.5f, 45f)] float eraseCone = 4f;
        [Tooltip("Brush radius at the nose (u): the brush never narrows below this near the hull, so close-up prisms paint and erase too (at a full pull; a light squeeze scales it like the cone).")]
        [SerializeField, Min(0f)] float brushNear = 6f;
        [Tooltip("Ball distance (u): while RT is held the squad balls up this far ahead.")]
        [SerializeField, Min(0f)] float slingAhead = 12f;
        [Tooltip("Throw speed (u/s): how fast the ball flies at the painted prisms.")]
        [SerializeField, Min(1f)] float slingSpeed = 150f;
        [Tooltip("Homing (/s): how sharply each thrown creature turns toward its own painted prism.")]
        [SerializeField, Min(0f)] float homeTurn = 12f;
        [Tooltip("Throw lasts (s): a thrown creature that latches onto nothing returns after this. A homing throw never gives up: it reaches its painted prism at any distance, limited only by speed.")]
        [SerializeField, Min(0.1f)] float slingTime = 3f;
        [Tooltip("Catch radius (u): with nothing painted, a thrown creature that passes within this of an enemy prism latches on.")]
        [SerializeField, Min(0f)] float slingCatch = 8f;
        [Tooltip("Latch cluster (u): it then eats that prism's domain within this of the latch.")]
        [SerializeField, Min(0f)] float slingCluster = 24f;
        [Tooltip("Dash to the school (share): the Time ability. Press it and the hull dashes this share of the way to the centroid of every creature it leads, measured when pressed.")]
        [SerializeField, Range(0f, 1f)] float dashFrac = 0.75f;
        [Tooltip("Dash time (s): how long the dash takes.")]
        [SerializeField, Min(0.05f)] float dashTime = 0.35f;

        [Header("The abilities in the game (game only: the lab models no flora, rival vessels, wild diet or crystals)")]
        [Tooltip("Every creature (wild ones too) ranks a painted prism this many times closer than it is when it picks its next " +
                 "meal (PaintedPrey). Preference only: the creature's own diet still decides what is edible. 1 = no preference.")]
        [SerializeField, Min(1f)] float paintedPreference = 4f;
        [Tooltip("Mass: while the lure is held, every plant within the lure's reach grows this many times as often (Flora.Hasten). " +
                 "1 = no effect.")]
        [SerializeField, Min(1f)] float lureGrowth = 3f;
        [Tooltip("Time: your tamed fauna swim this many times as fast (following, gathering, thrown, eating an order, hunting), " +
                 "from x1 at Time 0 to x2 at Time 10. The element's one parameter (vessel contract: Time = rate / mobility).")]
        [SerializeField] ElementalFloat faunaSpeed = ElementalFloat.Multiplier(1f, 2f, Element.Time, 1f);
        [Tooltip("Charge: a squad locked on to a vessel bites it as a scored Strike (PvP is petals only) at most once per this " +
                 "many seconds, however many creatures are on it (CombatHitDrain's per-pair latch).")]
        [SerializeField, Min(0.1f)] float lockBiteCooldown = 1f;
        [Tooltip("Charge: a locked creature bites when it is within this many units of the vessel, beyond its own mouth's reach.")]
        [SerializeField, Min(0f)] float lockBiteReach = 6f;
        [Tooltip("The omni crystal morphs into one of these, at random (a leashable herbivore species; the lab's O).")]
        [SerializeField] List<FaunaConfigurationSO> omniFauna = new();

        [Header("The keeper (game only: the lab spawns its first species)")]
        [Tooltip("The species the Angler's keeper is born as when the cell offers no leashable herbivore of its own. The " +
                 "Angler always leads at least one creature: with the school empty, one is born behind the hull, already " +
                 "tamed, and the school's first creature never starves (ANGLER.md §1b).")]
        [SerializeField] FaunaConfigurationSO companionFauna;

        [Header("Presentation (game only: the lab draws its own)")]
        [Tooltip("How often the brush re-reads the prisms under it while a trigger is held (s). The lab paints every frame; 0 does the same here.")]
        [SerializeField, Min(0f)] float brushInterval = 0f;

        public float LureAhead => lureAhead;
        public float LureCruise => lureCruise;
        public float LureRadius => lureRadius;
        public float TameRadius => tameRadius;
        public float TameTime => Mathf.Max(0.01f, tameTime);
        public float TameDecay => tameDecay;
        public int SquadCap => squadCap;
        public FaunaConfigurationSO CompanionFauna => companionFauna;
        public float LeashTow => Mathf.Max(1f, leashTow);
        public float FormBack => formBack;
        public float TadTurn => tadTurn;
        public bool SpareOwn => spareOwn;
        public float PaintRange => paintRange;
        public float PaintCone => paintCone;
        public float BrushMin => brushMin;
        public float EraseCone => eraseCone;
        public float BrushNear => brushNear;
        public float SlingAhead => slingAhead;
        public float SlingSpeed => slingSpeed;
        public float HomeTurn => homeTurn;
        public float SlingTime => slingTime;
        public float SlingCatch => slingCatch;
        public float SlingCluster => slingCluster;
        public float DashFrac => dashFrac;
        public float DashTime => Mathf.Max(0.05f, dashTime);
        public float PaintedPreference => Mathf.Max(1f, paintedPreference);
        public float LureGrowth => Mathf.Max(1f, lureGrowth);
        public ElementalFloat FaunaSpeed => faunaSpeed;
        public float LockBiteCooldown => Mathf.Max(0.1f, lockBiteCooldown);
        public float LockBiteReach => lockBiteReach;
        public IReadOnlyList<FaunaConfigurationSO> OmniFauna => omniFauna;
        public float BrushInterval => brushInterval;
    }
}
