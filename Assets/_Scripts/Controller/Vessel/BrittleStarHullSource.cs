using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the Brittle Star's gameplay and puppetry need from its hull, whoever draws it (design:
    /// <c>R_VesselActions/BRITTLE_STAR.md</c> §2): the ring the firing rotation turns and Ring Detonation throws, the
    /// six muzzles the guns and cords leave from, the six flames, the domain material the ring explosion wears.
    /// <para>Two hulls implement it: <see cref="BrittleStarHullModel"/>, the shipped one (<c>BrittleStar.fbx</c>, built by
    /// <c>Tools/Build/vessel_model/specs/brittlestar.py</c>, its parts found by NAME), and
    /// <see cref="BrittleStarHullBuilder"/>, the procedural placeholder it replaced, kept until the model is accepted so
    /// the swap is reversible by swapping one component.</para>
    /// <para><b>Channel discipline.</b> <see cref="BrittleStarAnimation"/> decides the ring's pose and the flames'
    /// lengths and writes them through <see cref="SetRingPose"/> / <see cref="SetFlame"/>; the hull owns how those
    /// land on its own transforms (an Euler on the placeholder's children, a rest-relative turn on the model's bones).
    /// The element morph's vertices are the model's blend shapes (driven by <see cref="VesselAnimation"/>) or the
    /// placeholder's rebuilt meshes; either way the muzzles follow through <see cref="ApplyElementMorphWeights"/>.</para>
    /// </summary>
    public abstract class BrittleStarHullSource : MonoBehaviour
    {
        /// <summary>The support ring (halo, arms, pods, guns): turned about the nose axis by the firing rotation, hidden
        /// while thrown. Null when the hull has none.</summary>
        public abstract Transform Ring { get; }

        /// <summary>Centre to each point of the pentagon (Creation Mode's corners, the ring's contact reach, where the
        /// explosion starts).</summary>
        public abstract float RingRadius { get; }

        /// <summary>Half the ring's thickness across its plane: what Ring Detonation's disk is as thick as.</summary>
        public abstract float RingHalfThickness { get; }

        /// <summary>The ring's plane along the nose axis, in the vessel's frame (the hull sits at the vessel's origin).</summary>
        public abstract float RingPlaneZ { get; }

        /// <summary>The cockpit gun's muzzle (index -1) or point <paramref name="index"/>'s, which rides the ring.
        /// Forward is the vessel's forward.</summary>
        public abstract Transform Muzzle(int index);

        /// <summary>The ring's firing rotation (radians about the nose axis) and its presence (1 mounted, toward 0
        /// thrown, overshooting while it blooms back).</summary>
        public abstract void SetRingPose(float phaseRadians, float presenceScale);

        /// <summary>The ring at rest: unturned, full size.</summary>
        public abstract void ResetRingPose();

        /// <summary>Flame <paramref name="index"/> (-1 the cockpit's, 0..4 the points') at <paramref name="length"/>
        /// world units along the exhaust and <paramref name="girth"/> times its authored width.</summary>
        public abstract void SetFlame(int index, float girth, float length);

        /// <summary>The material the domain colour is painted onto (the ring explosion is drawn in it).</summary>
        public abstract Material DomainMaterial { get; }

        /// <summary>Make <paramref name="body"/> (a MeshFilter + MeshRenderer) a still copy of the mounted ring as it
        /// stands - its spin, its element morph, its materials - posed exactly where the ring is, with its pivot on the
        /// nose axis so a spin about its local Z turns it in place.</summary>
        public abstract void DressThrownRing(GameObject body);

        /// <summary>The element weights (each 0..1: charge, mass, space, time) the hull is drawn at. The muzzles follow
        /// the barrels Space lengthens and the pods Charge lengthens.</summary>
        public abstract void ApplyElementMorphWeights(float charge, float mass, float space, float time);
    }
}
