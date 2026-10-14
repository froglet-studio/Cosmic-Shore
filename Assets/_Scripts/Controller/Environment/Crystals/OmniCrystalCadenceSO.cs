using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The omni crystal's beats and its square turns (Omni Motion Lab round 5, <c>Docs/Studios/OMNI_MOTION_LAB.md</c>):
    /// the Space squares turn round their pentagons, a Time wave flips the rhombi, the squares turn over their triangles,
    /// another Time wave - never two at once - and each square turn is ONE rigid turn that hops just high enough to clear
    /// its neighbours. The body's squares, rhombi and pentagons are also drawn thicker.
    ///
    /// Read by <see cref="OmniCrystalCadence"/> (the clock), <see cref="CrystalSpaceSquares"/> (the turns and the
    /// thickness) and <see cref="CrystalFlipWave"/> (one wave per wave beat). <c>OmniCrystalCadence.asset</c> is GENERATED
    /// by <c>Tools/Build/author_omni_crystal_triangles.py</c> from the lab's round-5 pick; its <c>--check</c> fails if the
    /// asset drifts. Retune in the lab, then in the generator.
    /// </summary>
    [CreateAssetMenu(fileName = "OmniCrystalCadence", menuName = "ScriptableObjects/Crystals/Omni Crystal Cadence")]
    public class OmniCrystalCadenceSO : ScriptableObject
    {
        [Header("Beats")]
        [Tooltip("Seconds the squares take to turn a fifth round their pentagon. The art file's key took 1.56 s; round 5 slowed it 10%.")]
        [SerializeField, Range(0.3f, 5f)] float pentagonTurnSeconds = 1.733f;

        [Tooltip("Seconds the squares take to turn a third round their triangle, as ONE move (the art file played it as two 1.6 s steps with a stop between).")]
        [SerializeField, Range(0.3f, 6f)] float triangleTurnSeconds = 2.4f;

        [Tooltip("Seconds of stillness between one beat ending and the next starting.")]
        [SerializeField, Range(0f, 1f)] float gapSeconds = 0.15f;

        [Tooltip("Seconds a Time wave's rings move at the profile's own speed (TimeCrystalFlipWaveProfile: from the lead ring's start to the last ring's landing). Generated from the profile.")]
        [SerializeField, Min(0.1f)] float waveActiveSeconds = 1.8f;

        [Tooltip("x the Time wave's speed on the omni (1 = the Time crystal's own).")]
        [SerializeField, Range(0.25f, 3f)] float waveSpeed = 1f;

        [Header("Square turns")]
        [Tooltip("How high a square hops off the shell at the middle of its pentagon turn, x the body's radius (a sine over the turn). 0.14 is the lowest that clears every rhombus it passes (lab round 2 sweep).")]
        [SerializeField, Range(0f, 0.3f)] float pentagonLift = 0.14f;

        [Tooltip("The triangle turn's hop straight out, x the art file's own Step1 lift (0.12 of the radius).")]
        [SerializeField, Range(0f, 3f)] float triangleLift = 1.5f;

        [Tooltip("The triangle turn's sideways step, x the art file's own (it keeps squares on neighbouring triangles, which turn about different axes, out of each other's way).")]
        [SerializeField, Range(0f, 3f)] float triangleSide = 1.5f;

        [Tooltip("Blend-shape frames per turn. Unity interpolates linearly between frames, so a turn is a polygon of this many sides; 24 keeps a 120-degree turn within 0.03% of the radius of a true arc.")]
        [SerializeField, Range(4, 64)] int framesPerTurn = 24;

        [Header("Plates")]
        [Tooltip("x the thickness of the body's squares, rhombi and pentagons (each about its own centre, along its own normal). The triangle prisms are their own mesh and are untouched. Round 5: 1.1.")]
        [SerializeField, Range(0.5f, 2f)] float plateThickness = 1.1f;

        public float PentagonTurnSeconds => pentagonTurnSeconds;
        public float TriangleTurnSeconds => triangleTurnSeconds;
        public float GapSeconds => gapSeconds;
        public float WaveSpeed => waveSpeed;
        public float PentagonLift => pentagonLift;
        public float TriangleLift => triangleLift;
        public float TriangleSide => triangleSide;
        public int FramesPerTurn => framesPerTurn;
        public float PlateThickness => plateThickness;

        /// <summary>The beats, with a wave lasting its active span at <see cref="WaveSpeed"/>.</summary>
        public OmniCadence Cadence => new(pentagonTurnSeconds, waveActiveSeconds / Mathf.Max(waveSpeed, 0.05f), triangleTurnSeconds, gapSeconds);
    }
}
