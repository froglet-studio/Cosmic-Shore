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
            /// <summary>The Ark again, aimed down its course at what is ahead (the objective).</summary>
            Course = 2,
        }

        [Serializable]
        public sealed class Shot
        {
            [Tooltip("What the shot frames.")]
            public Subject Subject;

            [Min(0.5f), Tooltip("Seconds on screen (per vessel for a Squad shot).")]
            public float Seconds = 4f;

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

            [Tooltip("The shot's title. Tokens: {voyage} {difficulty} {legs} {worlds} {first} {vessel} {seat} {who}.")]
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

        [Header("Skip")]
        [SerializeField, Tooltip("Shown in the corner while the intro plays. Empty shows nothing.")]
        string skipHint = "Hold any button to skip";

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

        /// <summary>The intro's whole running time for a squad of <paramref name="squadVessels"/> hulls, hand-over
        /// included.</summary>
        public float Duration(int squadVessels)
        {
            float t = handOverSeconds;
            foreach (var s in shots)
                if (s != null) t += s.Subject == Subject.Squad ? s.Seconds * Mathf.Max(0, squadVessels) : s.Seconds;
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
