using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.UI
{
    // The data a per-mode pre-game intro is made of. Every value here is authored by
    // Tools/Build/author_intro_cameras.py into Resources/IntroCameraLibrary.asset, ported from the
    // Intro Cam Lab artifact (Docs/IntroCameras/README.md). Distances in Arena space are in the
    // artifact's units (a 240-unit cell) and scale with the real cell, so a shot keeps its framing
    // in a small cell and a huge one.

    /// <summary>A scene object a shot is aimed at, resolved when the intro starts.</summary>
    public enum IntroAnchor
    {
        /// <summary>The cell centre.</summary>
        Center = 0,
        /// <summary>The local pilot's vessel.</summary>
        Player = 1,
        /// <summary>The Nth nearest vessel of another domain (falls back to the centre).</summary>
        Rival = 2,
        /// <summary>The Nth nearest vessel of the local pilot's domain, the pilot excluded.</summary>
        Teammate = 3,
        /// <summary>Every vessel, nearest the pilot first (index 0 is the pilot); index -1 is their centroid.</summary>
        Pilots = 4,
        /// <summary>The Nth crystal, nearest the centre first; index -1 is their centroid.</summary>
        Crystal = 5,
        /// <summary>The Nth race gate in course order (wraps); index -1 is their centroid.</summary>
        Gate = 6,
        /// <summary>The Nth biggest piece of arena geometry (a hulk, a cactus, a post); index -1 is their centroid.</summary>
        Landmark = 7,
    }

    /// <summary>How a <see cref="IntroPoint"/>'s offset (or a shot's distances) is measured.</summary>
    public enum IntroOffsetSpace
    {
        /// <summary>World axes, artifact units scaled by the cell's size.</summary>
        Arena = 0,
        /// <summary>World axes, world units.</summary>
        Units = 1,
        /// <summary>The anchor's own axes (a vessel's nose, a gate's flow), artifact units scaled by the cell's size.</summary>
        Local = 2,
        /// <summary>The anchor's own axes, world units.</summary>
        LocalUnits = 3,
    }

    public enum IntroShotKind
    {
        /// <summary>Dolly or crane between two points while the aim moves between two others.</summary>
        Move = 0,
        /// <summary>Circle a point, radius and height easing between two values.</summary>
        Orbit = 1,
        /// <summary>Fly a line through the arena: the gate course, the crystals, or a ring round the centre.</summary>
        Path = 2,
        /// <summary>Dolly zoom: dolly while the field of view changes to hold the subject's size.</summary>
        Vertigo = 3,
        /// <summary>Hard cuts, one per anchor of a kind, each a short push-in.</summary>
        Cuts = 4,
        /// <summary>Hold still.</summary>
        Hold = 5,
    }

    public enum IntroEase
    {
        Smooth = 0,
        Linear = 1,
        InOut = 2,
        Out = 3,
        In = 4,
        ExpoOut = 5,
        ExpoIn = 6,
        Back = 7,
    }

    public enum IntroPathSource
    {
        /// <summary>The race gates in course order (a Catmull-Rom spline through them).</summary>
        Gates = 0,
        /// <summary>The crystals, ordered round the centre.</summary>
        Crystals = 1,
        /// <summary>A horizontal circle round the centre (<see cref="IntroShot.RingRadius"/>, <see cref="IntroShot.RingHeight"/>).</summary>
        Ring = 2,
    }

    /// <summary>Which of the mode's authored lines a caption cue shows.</summary>
    public enum IntroCaptionSlot
    {
        Title = 0,
        Tagline = 1,
        Objective = 2,
    }

    [Serializable]
    public struct IntroPoint
    {
        [SerializeField] IntroAnchor anchor;
        [Tooltip("Which one of the anchor kind (0 = first). -1 = the centroid of all of them.")]
        [SerializeField] int index;
        [SerializeField] Vector3 offset;
        [SerializeField] IntroOffsetSpace space;

        public IntroAnchor Anchor => anchor;
        public int Index => index;
        public Vector3 Offset => offset;
        public IntroOffsetSpace Space => space;

        public IntroPoint(IntroAnchor anchor, int index, Vector3 offset, IntroOffsetSpace space)
        {
            this.anchor = anchor;
            this.index = index;
            this.offset = offset;
            this.space = space;
        }
    }

    [Serializable]
    public struct IntroCue
    {
        [SerializeField] IntroCaptionSlot slot;
        [Tooltip("Seconds after the shot starts.")]
        [SerializeField] float at;
        [SerializeField] float duration;

        public IntroCaptionSlot Slot => slot;
        public float At => at;
        public float Duration => duration;

        public IntroCue(IntroCaptionSlot slot, float at, float duration)
        {
            this.slot = slot;
            this.at = at;
            this.duration = duration;
        }
    }

    [Serializable]
    public struct IntroFlash
    {
        [Tooltip("Seconds after the shot starts.")]
        [SerializeField] float at;
        [SerializeField] float duration;

        public float At => at;
        public float Duration => duration;

        public IntroFlash(float at, float duration)
        {
            this.at = at;
            this.duration = duration;
        }
    }

    /// <summary>One camera move of an intro. Which fields matter depends on <see cref="Kind"/>.</summary>
    [Serializable]
    public class IntroShot
    {
        [SerializeField] string name;
        [SerializeField] IntroShotKind kind;
        [SerializeField] float duration = 2f;
        [SerializeField] IntroEase ease;

        [Header("Move / Hold")]
        [SerializeField] IntroPoint from;
        [SerializeField] IntroPoint to;
        [SerializeField] IntroPoint lookFrom;
        [SerializeField] IntroPoint lookTo;

        [Header("Orbit")]
        [SerializeField] IntroPoint orbitCenter;
        [SerializeField] float radius0;
        [SerializeField] float radius1;
        [SerializeField] float height0;
        [SerializeField] float height1;
        [Tooltip("Degrees round world up; 0 is +Z.")]
        [SerializeField] float angle0;
        [SerializeField] float angle1;

        [Header("Path")]
        [SerializeField] IntroPathSource path;
        [Tooltip("Where on the line the shot starts and ends, 0 to 1 (a closed line wraps).")]
        [SerializeField] float pathStart;
        [SerializeField] float pathEnd;
        [Tooltip("Camera offset from the line: x = right, y = up, z = forward. Arena units.")]
        [SerializeField] Vector3 pathOffset;
        [Tooltip("How far ahead along the line the camera aims, 0 to 1.")]
        [SerializeField] float pathAhead = 0.03f;
        [Tooltip("How much of the camera's height above the line the aim keeps (1 = level, below 0 looks down).")]
        [SerializeField] float pathLookUp = 0.6f;
        [Tooltip("Bank into turns; 0 keeps the horizon level.")]
        [SerializeField] float bank;
        [SerializeField] float ringRadius = 150f;
        [SerializeField] float ringHeight;

        [Header("Vertigo")]
        [SerializeField] IntroPoint vertigoTarget;
        [Tooltip("From the target toward the camera.")]
        [SerializeField] Vector3 vertigoDirection = Vector3.back;
        [SerializeField] float distance0;
        [SerializeField] float distance1;

        [Header("Cuts")]
        [SerializeField] IntroAnchor cutAnchor;
        [SerializeField] int cutCount = 5;
        [SerializeField] float cutDistance = 30f;
        [SerializeField] float cutPush = 6f;
        [Tooltip("Degrees of field of view each cut zooms in by.")]
        [SerializeField] float cutZoom = 4f;

        [Header("Lens")]
        [Tooltip("Orbit radius and height, vertigo distances and cut distances: Arena (scaled) or Units (world).")]
        [SerializeField] IntroOffsetSpace distanceSpace;
        [SerializeField] float fov0 = 60f;
        [SerializeField] float fov1 = 60f;
        [Tooltip("Degrees.")]
        [SerializeField] float roll0;
        [SerializeField] float roll1;
        [Tooltip("Degrees of roll added over the shot (a barrel roll is 360).")]
        [SerializeField] float spin;
        [SerializeField] float fovWaveAmplitude;
        [SerializeField] float fovWaveFrequency;
        [Tooltip("Handheld shake, Arena units.")]
        [SerializeField] float shake;
        [Tooltip("The shake dies away over the shot.")]
        [SerializeField] bool shakeDecay;

        [Header("Captions and flashes")]
        [SerializeField] List<IntroCue> cues = new();
        [SerializeField] List<IntroFlash> flashes = new();

        public string Name => name;
        public IntroShotKind Kind => kind;
        public float Duration => duration;
        public IntroEase Ease => ease;
        public IntroPoint From => from;
        public IntroPoint To => to;
        public IntroPoint LookFrom => lookFrom;
        public IntroPoint LookTo => lookTo;
        public IntroPoint OrbitCenter => orbitCenter;
        public float Radius0 => radius0;
        public float Radius1 => radius1;
        public float Height0 => height0;
        public float Height1 => height1;
        public float Angle0 => angle0;
        public float Angle1 => angle1;
        public IntroPathSource Path => path;
        public float PathStart => pathStart;
        public float PathEnd => pathEnd;
        public Vector3 PathOffset => pathOffset;
        public float PathAhead => pathAhead;
        public float PathLookUp => pathLookUp;
        public float Bank => bank;
        public float RingRadius => ringRadius;
        public float RingHeight => ringHeight;
        public IntroPoint VertigoTarget => vertigoTarget;
        public Vector3 VertigoDirection => vertigoDirection;
        public float Distance0 => distance0;
        public float Distance1 => distance1;
        public IntroAnchor CutAnchor => cutAnchor;
        public int CutCount => cutCount;
        public float CutDistance => cutDistance;
        public float CutPush => cutPush;
        public float CutZoom => cutZoom;
        public IntroOffsetSpace DistanceSpace => distanceSpace;
        public float Fov0 => fov0;
        public float Fov1 => fov1;
        public float Roll0 => roll0;
        public float Roll1 => roll1;
        public float Spin => spin;
        public float FovWaveAmplitude => fovWaveAmplitude;
        public float FovWaveFrequency => fovWaveFrequency;
        public float Shake => shake;
        public bool ShakeDecay => shakeDecay;
        public IReadOnlyList<IntroCue> Cues => cues;
        public IReadOnlyList<IntroFlash> Flashes => flashes;

        /// <summary>Test and tooling constructor; the game reads authored shots.</summary>
        public static IntroShot Create(IntroShotKind kind, float duration, IntroEase ease = IntroEase.Smooth)
            => new() { kind = kind, duration = duration, ease = ease };

        public IntroShot WithMove(IntroPoint from, IntroPoint to, IntroPoint lookFrom, IntroPoint lookTo)
        {
            this.from = from; this.to = to; this.lookFrom = lookFrom; this.lookTo = lookTo;
            return this;
        }

        public IntroShot WithOrbit(IntroPoint center, float r0, float r1, float h0, float h1, float a0, float a1)
        {
            orbitCenter = center; radius0 = r0; radius1 = r1; height0 = h0; height1 = h1; angle0 = a0; angle1 = a1;
            return this;
        }

        public IntroShot WithVertigo(IntroPoint target, Vector3 direction, float d0, float d1)
        {
            vertigoTarget = target; vertigoDirection = direction; distance0 = d0; distance1 = d1;
            return this;
        }

        public IntroShot WithLens(float f0, float f1, IntroOffsetSpace space = IntroOffsetSpace.Arena)
        {
            fov0 = f0; fov1 = f1; distanceSpace = space;
            return this;
        }
    }

    /// <summary>One intro: a run of shots. The hand-off to the pilot follows it, as it always has.</summary>
    [Serializable]
    public class IntroSequence
    {
        [Tooltip("The intro's name in the Intro Cam Lab (the concept, e.g. \"Trail skim\").")]
        [SerializeField] string name;
        [Tooltip("The mode it was designed for. It plays in every mode of its family.")]
        [SerializeField] GameModes designedFor;
        [SerializeField] List<IntroShot> shots = new();

        public string Name => name;
        public GameModes DesignedFor => designedFor;
        public IReadOnlyList<IntroShot> Shots => shots;

        public float Duration
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < shots.Count; i++)
                    total += Mathf.Max(0f, shots[i].Duration);
                return total;
            }
        }

        public static IntroSequence Create(string name, params IntroShot[] shots)
        {
            var s = new IntroSequence { name = name };
            s.shots.AddRange(shots);
            return s;
        }
    }

    /// <summary>Modes that share an arena kind, and the intros any of them may play.</summary>
    [Serializable]
    public class IntroFamily
    {
        [SerializeField] string name;
        [SerializeField] List<GameModes> modes = new();
        [SerializeField] List<IntroSequence> sequences = new();

        public string Name => name;
        public IReadOnlyList<GameModes> Modes => modes;
        public IReadOnlyList<IntroSequence> Sequences => sequences;
    }

    /// <summary>
    /// The words a mode's intro shows, whichever of its family's intros plays. Player-facing text,
    /// so it is authored here and never built in code; an empty line shows nothing.
    /// </summary>
    [Serializable]
    public class IntroModeWords
    {
        [SerializeField] GameModes mode;
        [Tooltip("The big title, e.g. \"Skim Race\".")]
        [SerializeField] string title;
        [Tooltip("One line on how the mode plays.")]
        [SerializeField] string tagline;
        [Tooltip("How the mode is won, shown in the corner tag.")]
        [SerializeField] string objective;
        [Tooltip("The lower-third line under the title at the end of the intro, e.g. the hull.")]
        [SerializeField] string pilots;

        public GameModes Mode => mode;
        public string Title => title;
        public string Tagline => tagline;
        public string Objective => objective;
        public string Pilots => pilots;

        public string For(IntroCaptionSlot slot) => slot switch
        {
            IntroCaptionSlot.Title => title,
            IntroCaptionSlot.Tagline => tagline,
            IntroCaptionSlot.Objective => objective,
            _ => string.Empty,
        };
    }
}
