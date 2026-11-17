using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The voyage's opening (Shombith, 2026-11-16: "entering voyages, I have no clue I just spawn, there should be an intro
    /// cinematic ... introducing the ark and introducing the vessels and the objective"). A short, skippable shot list
    /// played the moment the Ark sets sail: the Ark, each vessel of the squad, then the course and what the squad is
    /// there to do, ending in a blend into the pilot's own camera as the stick comes back.
    ///
    /// <para>Every line is authored here (an empty line shows nothing). The shots are framed around their subject by
    /// angle and distance in multiples of the subject's own size, so the same list fits the Ark and any hull. Played by
    /// <c>VoyageIntro</c>; the escort starts it (<c>ArkEscortRun</c>).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ArkEscortIntro", menuName = "ScriptableObjects/Voyages/Voyage Intro")]
    public sealed class VoyageIntroSO : ScriptableObject
    {
        /// <summary>Under Resources: the escort's intro.</summary>
        public const string EscortResourcePath = "Voyages/ArkEscortIntro";
        /// <summary>Crystal Run's intro (job.raid.crystals): the same reel, its own words.</summary>
        public const string CrystalRunResourcePath = "Voyages/CrystalRunIntro";

        public enum Subject
        {
            /// <summary>The Ark (size: half its hull's length).</summary>
            Ark = 0,
            /// <summary>One shot per vessel in the squad, the player's own last (size: the hull's bounds).</summary>
            Squad = 1,
            /// <summary>The Ark again, aimed down its course at what is ahead (the objective): the title, the goal and the
            /// route drawn in as a line of stops.</summary>
            Course = 2,
            /// <summary>The player's own vessel, with its name card (Rev 114 Departure, shot 3).</summary>
            You = 3,
            /// <summary>One shot per wingmate in seat order, each adding its name card (Rev 114 Departure, shot 4).</summary>
            Wingmates = 4,
        }

        [Serializable]
        public sealed class Shot
        {
            [Tooltip("What the shot frames.")]
            public Subject Subject;

            [Min(0.2f), Tooltip("Seconds on screen (per vessel for a Squad or Wingmates shot).")]
            public float Seconds = 4f;

            [Tooltip("Cut to this shot on the beat instead of easing into it.")]
            public bool HardCut;

            [Tooltip("Launch from the Ark's hangar as this shot starts and form up beside it (one seat off each side, one " +
                     "over the back). On a vessel's shot (You, Wingmates, Squad) that vessel launches, close up; on an " +
                     "Ark shot the whole squad goes. Until then the vessels wait on the hangar's pads; a reel with no " +
                     "launch shot opens with them already in their slots.")]
            public bool Launch;

            [Tooltip("Played as the shot starts (a sting, a whoosh). Empty plays nothing.")]
            public FMODUnity.EventReference Sound;

            [Tooltip("Where the camera starts around the subject, in degrees: 0 dead ahead, 90 to starboard, 180 astern.")]
            public float FromAzimuth;

            [Tooltip("Where the camera ends around the subject, in degrees (the move is an arc from one to the other).")]
            public float ToAzimuth = 60f;

            [Range(-80f, 80f), Tooltip("Degrees above the subject's own plane.")]
            public float Elevation = 12f;

            [Min(0.2f), Tooltip("Starting distance, in subject sizes.")]
            public float FromDistance = 3f;

            [Min(0.2f), Tooltip("Ending distance, in subject sizes (smaller pushes in).")]
            public float ToDistance = 2.4f;

            [Tooltip("Aim this many subject sizes ahead of the subject, along its heading (a Course shot looks down the " +
                     "course with it).")]
            public float LookAhead;

            [Tooltip("The shot's title. Tokens: {voyage} {difficulty} {legs} {worlds} {first} {hull} {objective} {vessel} " +
                     "{seat} {who} and each voyage's own ({goal} {to} {breach} for Crystal Run). On a vessel's shot it is " +
                     "the name card's first line; on an Ark shot a small caption; on a Course shot the big title.")]
            public string Title;

            [TextArea, Tooltip("The line under the title (same tokens).")]
            public string Line;
        }

        [Header("Shots (played in order)")]
        [SerializeField] List<Shot> shots = new();

        [Header("Who is who")]
        [SerializeField, Tooltip("Where a Squad shot reads each vessel's name (SO_Vessel.CardName).")]
        SO_VesselList vessels;

        [SerializeField, Tooltip("Between world names in {worlds}.")]
        string worldSeparator = " · ";

        [SerializeField, Tooltip("A name card's second line. Tokens: {pilot} {element} {seat} {who}.")]
        string cardLine = "{pilot} · {element}";

        [Header("Sound")]
        [SerializeField, Tooltip("Played as the intro starts (the low music). Empty plays nothing.")]
        FMODUnity.EventReference openingSound;

        [SerializeField, Tooltip("Played with the GO word as the camera swings in behind the pilot. Empty plays nothing.")]
        FMODUnity.EventReference goSound;

        [Header("Go")]
        [SerializeField, Tooltip("The word over the hand-over, as the stick comes back. Empty shows nothing.")]
        string goWord = "GO";

        [Header("Skip")]
        [SerializeField, Tooltip("Shown in the corner while the intro may be skipped. Empty shows nothing.")]
        string skipHint = "Hold B to skip";

        [SerializeField, Tooltip("The first time a pilot sees this intro it plays through; after that it can be skipped.")]
        bool skippableAfterFirstRun = true;

        [SerializeField, Min(0.1f), Tooltip("Seconds a button is held to skip.")]
        float holdToSkipSeconds = 0.6f;

        [SerializeField, Min(0f), Tooltip("A press already held when the intro starts does not count; nor does any press " +
                                          "in its first this-many seconds.")]
        float skipGraceSeconds = 0.5f;

        [Header("Look")]
        [SerializeField, Range(0f, 0.25f), Tooltip("Height of each letterbox bar, as a share of the screen.")]
        float letterboxShare = 0.11f;

        [SerializeField, Min(0.05f), Tooltip("Seconds the bars and words take to come in and go out.")]
        float fadeSeconds = 0.45f;

        [SerializeField, Min(0.05f), Tooltip("Seconds a cut between shots is eased (0.05 is a hard cut).")]
        float cutSeconds = 0.6f;

        [Header("Hand-over")]
        [SerializeField, Min(0.1f), Tooltip("Seconds the last shot blends into the pilot's chase camera.")]
        float handOverSeconds = 1.4f;

        [SerializeField, Min(0.5f), Tooltip("The chase pose the hand-over blends into: this many hull sizes behind the " +
                                            "pilot's vessel.")]
        float chaseDistance = 4f;

        [SerializeField, Tooltip("... and this many hull sizes above it.")]
        float chaseHeight = 1.2f;

        public IReadOnlyList<Shot> Shots => shots;
        public SO_VesselList Vessels => vessels;
        public string CardLine => cardLine;
        public FMODUnity.EventReference OpeningSound => openingSound;
        public FMODUnity.EventReference GoSound => goSound;
        public string GoWord => goWord;
        public bool SkippableAfterFirstRun => skippableAfterFirstRun;
        public string WorldSeparator => worldSeparator;
        public string SkipHint => skipHint;
        public float HoldToSkipSeconds => holdToSkipSeconds;
        public float SkipGraceSeconds => skipGraceSeconds;
        public float LetterboxShare => letterboxShare;
        public float FadeSeconds => fadeSeconds;
        public float CutSeconds => cutSeconds;
        public float HandOverSeconds => handOverSeconds;
        public float ChaseDistance => chaseDistance;
        public float ChaseHeight => chaseHeight;

        /// <summary>True when a shot launches the squad from the hangar (<see cref="Shot.Launch"/>).</summary>
        public bool LaunchesSquad
        {
            get
            {
                foreach (var s in shots)
                    if (s is { Launch: true }) return true;
                return false;
            }
        }

        /// <summary>The intro's whole running time for a squad of <paramref name="squadVessels"/> hulls, hand-over
        /// included.</summary>
        public float Duration(int squadVessels)
        {
            float t = handOverSeconds;
            foreach (var s in shots)
                if (s != null) t += s.Subject switch
                {
                    Subject.Squad => s.Seconds * Mathf.Max(0, squadVessels),
                    Subject.Wingmates => s.Seconds * Mathf.Max(0, squadVessels - 1),
                    _ => s.Seconds,
                };
            return t;
        }

        static VoyageIntroSO s_escort, s_crystalRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_escort = s_crystalRun = null;

        /// <summary>The escort's intro, or null when none is shipped (the escort then opens as it always did).</summary>
        public static VoyageIntroSO LoadEscort()
        {
            if (!s_escort) s_escort = Resources.Load<VoyageIntroSO>(EscortResourcePath);
            return s_escort;
        }

        /// <summary>Crystal Run's intro, or null when none is shipped (the run then opens on its first toast).</summary>
        public static VoyageIntroSO LoadCrystalRun()
        {
            if (!s_crystalRun) s_crystalRun = Resources.Load<VoyageIntroSO>(CrystalRunResourcePath);
            return s_crystalRun;
        }
    }
}
