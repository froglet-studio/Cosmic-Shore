using System;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How one kind of wormhole tube LOOKS: a clear, slightly jelly-like hollow in space that bends what you see through its
    /// wall and breathes slowly (Yash, 2026-10-29: "pleasant calm distortion, little jelly like clear liquidy hollow from inside
    /// tree trunk, same will be for the root"). The trunk (a formed tunnel) and the root (an omnicrystal network's channel) use
    /// this same struct, each its own copy on <see cref="MeerkatWormholeConfigSO"/> (Trunk Look, Root Look). Drawn by
    /// <see cref="MeerkatWormholeLensPass"/> with <c>MeerkatWormholeLens.shader</c>. Clear: nothing tints the world behind it.
    /// Every field, default, range and the comfort guidance: <c>Docs/Shaders/WORMHOLE_SHADERS.md</c> §14.
    /// </summary>
    [Serializable]
    public struct MeerkatWormholeLook
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
        [Tooltip("A thin-film shimmer on the edge light and glow (0 = plain white).")]
        [Range(0f, 1f)] public float Shimmer;
        [Tooltip("Soft pulses of light running along the tube (0 = none).")]
        [Range(0f, 0.5f)] public float Pulse;
        [Tooltip("...pulses along the tube's visible length.")]
        [Range(1f, 8f)] public float PulseCount;
        [Tooltip("...how fast they run (per second; capped by Comfort's Max pulse speed).")]
        [Range(0f, 2f)] public float PulseSpeed;

        /// <summary>Anything to draw at <paramref name="intensity"/> (the comfort master): bending, or any light on the wall.</summary>
        public bool Visible(float intensity) =>
            intensity > 0f && (Bend > 0f || Lens > 0f || Pulse > 0f) || Rim > 0f || Glint > 0f || Glow > 0f;

        /// <summary>A formed tunnel's look: Yash's shipped wormhole (Tube Lens Lab bend 6° × drag 0.5, glass 0.139), its
        /// rings replaced by a slow breath, no tint, no dispersion.</summary>
        public static readonly MeerkatWormholeLook TrunkDefault = new()
        {
            Bend = 3f, Lens = 0.139f, WallInside = 0.1f, WallOutside = 0.15f,
            Wobble = 0.2f, WobbleWaves = 1.5f, WobbleSpeed = 0.12f,
            Rim = 0.05f, RimTightness = 4f, Glint = 0.25f, Glow = 0.02f,
            Shimmer = 0f, Pulse = 0f, PulseCount = 3f, PulseSpeed = 0.6f,
        };

        /// <summary>A network channel's look: the trunk's, plus the network's faint shimmer and slow pulses.</summary>
        public static readonly MeerkatWormholeLook RootDefault = new()
        {
            Bend = 3f, Lens = 0.139f, WallInside = 0.1f, WallOutside = 0.15f,
            Wobble = 0.2f, WobbleWaves = 1.5f, WobbleSpeed = 0.12f,
            Rim = 0.05f, RimTightness = 4f, Glint = 0.25f, Glow = 0.04f,
            Shimmer = 0.5f, Pulse = 0.12f, PulseCount = 3f, PulseSpeed = 0.6f,
        };
    }

    /// <summary>
    /// How a tube looks while you RIDE it (your camera inside it): the comfort side of the look. The trunk and the root use
    /// this same struct, each its own copy on <see cref="MeerkatWormholeConfigSO"/> (Trunk Ride, Root Ride).
    /// </summary>
    [Serializable]
    public struct MeerkatWormholeRideView
    {
        [Tooltip("Inside: the bend and the jelly lens × this (1 = as seen from outside; lower is calmer to ride).")]
        [Range(0f, 1f)] public float InsideBend;
        [Tooltip("Inside: the breathing × this (lower is calmer to ride).")]
        [Range(0f, 1f)] public float InsideWobble;
        [Tooltip("Inside: the middle of the screen is kept still out to this radius (× the screen's height; 0 = off). " +
                 "Keeps where you are heading steady.")]
        [Range(0f, 0.5f)] public float SteadyCentre;

        public static readonly MeerkatWormholeRideView Default = new() { InsideBend = 0.5f, InsideWobble = 0.5f, SteadyCentre = 0.2f };
    }

    /// <summary>The comfort master over every wormhole's look (both kinds), so nobody riding one feels sick.</summary>
    [Serializable]
    public struct MeerkatWormholeComfort
    {
        [Tooltip("Distortion intensity: scales every bend, lens, breath and pulse. 0 = clear, nothing moves (the edge light " +
                 "and glint stay); 1 = the authored look.")]
        [Range(0f, 1f)] public float DistortionIntensity;
        [Tooltip("No tube breathes faster than this (breaths per second), whatever its look says.")]
        [Range(0f, 0.5f)] public float MaxWobbleSpeed;
        [Tooltip("No pulse of light runs faster than this (per second), whatever its look says.")]
        [Range(0f, 2f)] public float MaxPulseSpeed;

        public static readonly MeerkatWormholeComfort Default = new() { DistortionIntensity = 1f, MaxWobbleSpeed = 0.25f, MaxPulseSpeed = 1f };
    }

    /// <summary>
    /// The Lavalamp lab's live tuning (<c>LavalampWormholeLab</c>): while <see cref="Active"/>, the lens draws with these copies
    /// instead of the config's, so the asset is never touched in play mode. <see cref="Begin"/> seeds them from a config.
    /// </summary>
    public static class MeerkatWormholeLookTuning
    {
        public static bool Active;
        public static MeerkatWormholeComfort Comfort = MeerkatWormholeComfort.Default;
        public static MeerkatWormholeLook Trunk = MeerkatWormholeLook.TrunkDefault, Root = MeerkatWormholeLook.RootDefault;
        public static MeerkatWormholeRideView TrunkRide = MeerkatWormholeRideView.Default, RootRide = MeerkatWormholeRideView.Default;

        public static void Begin(MeerkatWormholeConfigSO cfg)
        {
            if (!cfg) return;
            Comfort = cfg.Comfort;
            Trunk = cfg.TrunkLook;
            Root = cfg.RootLook;
            TrunkRide = cfg.TrunkRide;
            RootRide = cfg.RootRide;
        }

        /// <summary>The numbers a tube of <paramref name="cfg"/> draws with now: the lab's while tuning, else the config's.</summary>
        public static MeerkatWormholeComfort ComfortOf(MeerkatWormholeConfigSO cfg) => Active ? Comfort : cfg.Comfort;
        public static MeerkatWormholeLook LookOf(MeerkatWormholeConfigSO cfg, bool root) =>
            Active ? (root ? Root : Trunk) : (root ? cfg.RootLook : cfg.TrunkLook);
        public static MeerkatWormholeRideView RideOf(MeerkatWormholeConfigSO cfg, bool root) =>
            Active ? (root ? RootRide : TrunkRide) : (root ? cfg.RootRide : cfg.TrunkRide);

        /// <summary>The numbers as JSON, to paste into the config asset by hand.</summary>
        public static string Dump() =>
            "Comfort " + JsonUtility.ToJson(Comfort) + "\nTrunk Look " + JsonUtility.ToJson(Trunk) + "\nTrunk Ride " +
            JsonUtility.ToJson(TrunkRide) + "\nRoot Look " + JsonUtility.ToJson(Root) + "\nRoot Ride " + JsonUtility.ToJson(RootRide);

        /// <summary>Toward the main light (xyz) and its glint strength (w): the scene's sun, else a soft key from above.</summary>
        public static Vector4 LightDir()
        {
            var sun = RenderSettings.sun;
            if (sun && sun.isActiveAndEnabled)
            {
                var d = -sun.transform.forward;
                return new Vector4(d.x, d.y, d.z, Mathf.Clamp(sun.intensity, 0f, 2f));
            }
            var k = new Vector3(0.3f, 0.8f, 0.4f).normalized;
            return new Vector4(k.x, k.y, k.z, 0.6f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Active = false;
    }
}
