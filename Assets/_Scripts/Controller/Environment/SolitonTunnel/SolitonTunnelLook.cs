using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How a Soliton Tunnel LOOKS: a plain wormhole (Yash, 2026-11-01: "plain worm hole with wider mouth looking cool from
    /// outside and inside, bending and warping of space time"). Its wall is clear bent space (the contour lens, breathing
    /// slowly); each live mouth is a gravitational lens - the world round it bends into an Einstein ring, the view straight
    /// into it inverts and magnifies (from outside); riding it, the walls stretch and stream toward the way out (inside). No
    /// bark, no trunk/root look, no spindles. One copy per <see cref="SolitonTunnelConfigSO"/>. Drawn by
    /// <see cref="SolitonTunnelLensPass"/> with <c>SolitonTunnel.shader</c> in one full-screen pass.
    /// Every field: <c>Docs/Shaders/WORMHOLE_SHADERS.md</c> §16.
    /// </summary>
    [Serializable]
    public struct SolitonTunnelLook
    {
        [Tooltip("Space bent along the tube where you look through its wall (° per wall crossing). The main wormhole feel. " +
                 "0 = no bending.")]
        [Range(0f, 12f)] public float Bend;
        [Tooltip("The jelly lens at the wall's surface: how much the wall itself refracts (0 = none). Gives the liquid edge.")]
        [Range(0f, 1f)] public float Lens;
        [Tooltip("How far the bending reaches into the hollow, inward from the wall (× the local radius).")]
        [Range(0.02f, 0.5f)] public float WallInside;
        [Tooltip("How far the bending reaches out round the tube, outward from the wall (× the local radius).")]
        [Range(0.02f, 0.6f)] public float WallOutside;
        [Tooltip("The jelly breathing: how much the bend swells and eases along the tube (0 = still, 1 = from none to double).")]
        [Range(0f, 1f)] public float Wobble;
        [Tooltip("...swells along the tube's visible length (fewer = calmer, broader).")]
        [Range(0.5f, 6f)] public float WobbleWaves;
        [Tooltip("...breaths per second (capped by Comfort's Max wobble speed).")]
        [Range(0f, 1f)] public float WobbleSpeed;
        [Tooltip("A clear, faint edge light where your view grazes the wall (white, not a tint).")]
        [Range(0f, 0.5f)] public float Rim;
        [Tooltip("...how tight to the silhouette (higher = thinner).")]
        [Range(1f, 8f)] public float RimTightness;
        [Tooltip("The main light's glint off the jelly wall.")]
        [Range(0f, 1f)] public float Glint;
        [Tooltip("A faint glow of the wall itself, by how much of it you look through (white, additive).")]
        [Range(0f, 0.2f)] public float Glow;

        [Header("Mouth: gravitational lens (from outside)")]
        [Tooltip("How hard each live mouth bends the world round it (0 = off). Around the mouth the scene curls into an " +
                 "Einstein ring; looking straight in, it turns over and magnifies.")]
        [Range(0f, 1f)] public float MouthLens;
        [Tooltip("The lens's reach (× the mouth's radius): the visible, flared size of the mouth. Wider = a bigger mouth.")]
        [Range(1f, 4f)] public float MouthFlare;
        [Tooltip("The thin bright Einstein ring at the lens's edge (0 = none).")]
        [Range(0f, 1f)] public float EinsteinRing;
        [Tooltip("How dark the throat's core reads (0 = clear; a hint makes the mouth read as a hole).")]
        [Range(0f, 1f)] public float MouthShadow;

        [Header("Throat: space-time stretch (from inside)")]
        [Tooltip("Riding it: the walls stretch out from where you are heading (0 = off). Keep low for comfort.")]
        [Range(0f, 0.5f)] public float InsideStretch;
        [Tooltip("...the warp streams toward the way out at this speed (per second; capped by Comfort's Max flow speed).")]
        [Range(0f, 2f)] public float InsideFlow;
        [Tooltip("...and twists round the throat by this much (0 = straight).")]
        [Range(0f, 0.5f)] public float InsideSwirl;

        /// <summary>Anything to draw at <paramref name="intensity"/> (the comfort master): bending, or any light on the wall.</summary>
        public bool Visible(float intensity) =>
            intensity > 0f && (Bend > 0f || Lens > 0f || MouthLens > 0f || InsideStretch > 0f) || Rim > 0f || Glint > 0f ||
            Glow > 0f || EinsteinRing > 0f;

        /// <summary>A formed tunnel's look: the plain wormhole - the clear contour lens on its wall (bend 3°, glass 0.139),
        /// a strong gravitational lens at its flared mouths, and a gentle stretch streaming along the throat inside.</summary>
        public static readonly SolitonTunnelLook TunnelDefault = new()
        {
            Bend = 3f, Lens = 0.139f, WallInside = 0.1f, WallOutside = 0.2f,
            Wobble = 0.15f, WobbleWaves = 1.5f, WobbleSpeed = 0.12f,
            Rim = 0.04f, RimTightness = 4f, Glint = 0.2f, Glow = 0.01f,
            MouthLens = 0.6f, MouthFlare = 2f, EinsteinRing = 0.25f, MouthShadow = 0.35f,
            InsideStretch = 0.12f, InsideFlow = 0.6f, InsideSwirl = 0.08f,
        };

        /// <summary>A Soliton Network channel's look: the same wormhole, its many mouths a little softer.</summary>
        public static readonly SolitonTunnelLook NetworkDefault = new()
        {
            Bend = 3f, Lens = 0.139f, WallInside = 0.1f, WallOutside = 0.2f,
            Wobble = 0.15f, WobbleWaves = 1.5f, WobbleSpeed = 0.12f,
            Rim = 0.04f, RimTightness = 4f, Glint = 0.2f, Glow = 0.01f,
            MouthLens = 0.45f, MouthFlare = 1.8f, EinsteinRing = 0.2f, MouthShadow = 0.3f,
            InsideStretch = 0.1f, InsideFlow = 0.6f, InsideSwirl = 0.06f,
        };
    }

    /// <summary>
    /// How a tube looks while you RIDE it (your camera inside it): the comfort side of the look. One copy per
    /// <see cref="SolitonTunnelConfigSO"/>.
    /// </summary>
    [Serializable]
    public struct SolitonTunnelRideView
    {
        [Tooltip("Inside: the bend and the jelly lens × this (1 = as seen from outside; lower is calmer to ride).")]
        [Range(0f, 1f)] public float InsideBend;
        [Tooltip("Inside: the breathing × this (lower is calmer to ride).")]
        [Range(0f, 1f)] public float InsideWobble;
        [Tooltip("Inside: the middle of the screen is kept still out to this radius (× the screen's height; 0 = off). " +
                 "Keeps where you are heading steady.")]
        [Range(0f, 0.5f)] public float SteadyCentre;

        public static readonly SolitonTunnelRideView Default = new() { InsideBend = 0.5f, InsideWobble = 0.5f, SteadyCentre = 0.2f };
    }

    /// <summary>The comfort master over a Soliton Tunnel's look, so nobody riding one feels sick.</summary>
    [Serializable]
    public struct SolitonTunnelComfort
    {
        [Tooltip("Distortion intensity: scales every bend, lens, breath and stretch. 0 = clear, nothing moves (the edge light " +
                 "and glint stay); 1 = the authored look.")]
        [Range(0f, 1f)] public float DistortionIntensity;
        [Tooltip("No tube breathes faster than this (breaths per second), whatever its look says.")]
        [Range(0f, 0.5f)] public float MaxWobbleSpeed;
        [Tooltip("The throat's warp never streams faster than this (per second), whatever its look says.")]
        [FormerlySerializedAs("MaxPulseSpeed")]
        [Range(0f, 2f)] public float MaxFlowSpeed;

        public static readonly SolitonTunnelComfort Default = new() { DistortionIntensity = 1f, MaxWobbleSpeed = 0.25f, MaxFlowSpeed = 1f };
    }
}
