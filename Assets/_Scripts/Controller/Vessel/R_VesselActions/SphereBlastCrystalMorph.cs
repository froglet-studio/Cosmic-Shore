using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY onto the spherical blast the same pickup fires — the
    /// bespoke replacement for the shared husk spray on every hull whose omni-crystal ability is a
    /// plain <see cref="AOEExplosion"/> sphere: the Butterfly's bloom, the Rhino's slow blast, the
    /// Manta's Kabloom self-blast. Full record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/SPHERE_BLAST_CRYSTAL_MORPH.md</c>.
    ///
    /// Until now the crystal also burst into the shared spray, so the pickup read as two unrelated
    /// events: something shattered, and separately a sphere went off. It now reads as one — the
    /// cage swells outward from where the crystal was and lands on the blast's surface, and the
    /// blast takes the surface over while the crystal dissolves off it.
    ///
    /// ── The mapping is the Scarab's hull cast, from the CRYSTAL'S middle ──────────────────────
    /// A sphere is convex, so every vertex lands on it along a ray and takes that facet's normal
    /// (<see cref="CrystalMorphMeshBuilder.ConvexHullTarget"/>). The blast is centred on the hull,
    /// not on the crystal, so the ray origin is the crystal's own middle wherever it lies well
    /// inside the sphere (<see cref="SphereBlastMorphGeometry.CastCentre"/>): the cage opens the way
    /// it faces instead of piling onto the one patch that faces the crystal.
    ///
    /// ── It lands on the sphere AS IT WILL BE at the hand-off ──────────────────────────────────
    /// The blast grows from nothing; its pose is static and its curve deterministic, so the target
    /// is read from the blast's own curve at the moment the morph hands off
    /// (<see cref="AOEExplosion.BlastLocalToWorldAt"/>). No second authority — retune the blast's
    /// delay, duration or scale and the morph follows.
    ///
    /// ── MECHANICALLY INSTANT, VISUALLY GRADUAL ─────────────────────────────────────────────────
    /// Only the blast's PHOTONS wait (<see cref="AOEExplosion.HoldPhotonsForMorph"/>). Its sweep,
    /// trigger and prism damage run from the frame it detonates, exactly as before, so the vessel
    /// kit is unchanged.
    ///
    /// Adoption, colour convergence, overlays, hand-off and dissolve are
    /// <see cref="CrystalMorphRunner"/>'s, shared with the Squirrel, the Scarab and the Dolphin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SphereBlastCrystalMorph : CrystalMorphRunner
    {
        IVessel _vessel;
        string _owner = "SphereBlast";
        Vector3 _collectPosition;
        AOEExplosion _blast;
        bool _subscribed;
        int _blastsSeen;

        protected override string Owner => _owner;

        /// <summary>
        /// Stands a morph up in the pose the crystal HAD WHEN IT WAS COLLECTED, wearing its own
        /// renderers, and waits for the sphere the same dispatch is about to fire. Runs on every
        /// peer — the vessel's crystal effects are broadcast — and each peer lands on its own blast.
        /// </summary>
        public static SphereBlastCrystalMorph Begin(in CrystalForgeOrigin origin, IVessel vessel, string owner)
        {
            if (vessel == null) return null;
            string tag = string.IsNullOrEmpty(owner) ? "SphereBlast" : owner;
            if (!origin.Valid)
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] the pickup carried no crystal origin, so there " +
                                   "is no body to morph — this pickup retires with no animation. " +
                                   "CrystalImpactData.FromCrystal stamps it on every collect.");
                return null;
            }

            var crystal = ResolveCrystal(origin);

            // A bodyless crystal is not a failure: there is no cage to carry. The blast still fires.
            if (crystal != null && !crystal.HasBody) return null;

            if (crystal == null)
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] no crystal with id {origin.CrystalId} on this " +
                                   "peer, so there is no body to carry onto the blast — this pickup " +
                                   "retires with no animation. A peer whose cell has not finished " +
                                   "initialising cannot find it.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(origin.Position, origin.Rotation);
            go.transform.localScale = origin.Scale == Vector3.zero ? Vector3.one : origin.Scale;

            var morph = go.AddComponent<SphereBlastCrystalMorph>();
            morph._owner = tag;
            if (!morph.Adopt(crystal))
            {
                CSDebug.LogWarning($"[{tag}CrystalMorph] '{crystal.name}' exposed no drawable body " +
                                   "(a model with a MeshFilter and MeshRenderer, or a SkinnedMeshRenderer) " +
                                   "— nothing to morph, so this pickup retires with no animation.");
                Destroy(go);
                return null;
            }

            morph._vessel = vessel;
            morph._collectPosition = origin.Position;
            morph.BeginWaiting();
            AOEExplosion.BlastInitialized += morph.OnBlastInitialized;
            morph._subscribed = true;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] {tag}: began on '{crystal.name}' at {origin.Position}, {morph.Census}; " +
                $"waiting up to {morph.Config.TargetGraceSeconds:F2}s for the blast.");
            return morph;
        }

        void OnBlastInitialized(AOEExplosion blast)
        {
            if (IsStamped || blast == null) return;
            if (!blast.IsPlainSphere) return;   // flowers, prism layers, cones: not this target
            _blastsSeen++;

            if (!IsThisVessel(blast.Vessel))
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] {Owner}: skipped a blast another vessel fired.");
                return;
            }

            // This pickup's blast went off at the hull that touched the crystal. A morph still
            // waiting out a swallowed blast (the effects' anti-spam) must not claim a later one, and
            // a Manta's far-off planted bombs are not its Kabloom self-blast.
            float distance = Vector3.Distance(blast.transform.position, _collectPosition);
            if (distance > Config.RingCaptureRadius)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] {Owner}: skipped a blast {distance:F0}u from the collect " +
                    $"(capture radius {Config.RingCaptureRadius:F0}u).");
                return;
            }

            if (!TryBuildOntoSphere(blast, out var mesh, out string refusal))
            {
                CSDebug.LogWarning($"[{Owner}CrystalMorph] cannot land on the blast: {refusal}. " +
                                   "The crystal will fade instead; the blast is unaffected.");
                return;
            }

            Unsubscribe();
            _blast = blast;

            // Photons only: the blast sweeps, damages and triggers from this frame, as before.
            _blast.HoldPhotonsForMorph(true);
            Stamp(mesh);

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] {Owner}: took blast #{_blastsSeen} {distance:F1}u from the collect — " +
                $"{MorphVertexCount} morph vertices, {MorphSeconds:F2}s of morph + " +
                $"{Config.Duration - MorphSeconds:F2}s of dissolve (colour target " +
                $"{(HasTargetColour ? "read" : "NOT FOUND")}).");
        }

        bool IsThisVessel(IVessel other)
        {
            if (other == null || _vessel == null) return false;
            if (ReferenceEquals(other, _vessel)) return true;
            var a = other.VesselStatus;
            var b = _vessel.VesselStatus;
            return a != null && ReferenceEquals(a, b);
        }

        /// <summary>
        /// The sphere as it will be drawn at the hand-off, in the BODY's frame, cast from the
        /// crystal's own middle (clamped strictly inside the sphere).
        /// </summary>
        bool TryBuildOntoSphere(AOEExplosion blast, out Mesh mesh, out string refusal)
        {
            mesh = null;
            refusal = null;

            var source = SourceMesh;
            if (source == null) { refusal = "the crystal's body exposed no mesh"; return false; }

            float morphSeconds = Config.Duration * Mathf.Clamp01(Config.MorphFraction);
            Matrix4x4 blastToWorld = blast.BlastLocalToWorldAt(morphSeconds);

            Transform frame = BodyFrame;
            Matrix4x4 toLocal = frame.worldToLocalMatrix * blastToWorld;

            // The sphere's centre and radius in the body's frame (the mesh is radius 0.5 at unit
            // scale; measured off the transformed mesh bounds so any authored mesh is honoured).
            var hullMesh = blast.BlastMesh;
            if (hullMesh == null) { refusal = "the blast prefab draws no mesh"; return false; }
            Vector3 centre = toLocal.MultiplyPoint3x4(hullMesh.bounds.center);
            float radius = (toLocal.MultiplyPoint3x4(hullMesh.bounds.center + Vector3.right * hullMesh.bounds.extents.x)
                            - centre).magnitude;
            if (!(radius > 1e-4f))
            {
                refusal = $"the blast has no size {morphSeconds:F2}s after it was fired (its delay " +
                          "runs past the morph window)";
                return false;
            }

            Vector3 castFrom = SphereBlastMorphGeometry.CastCentre(centre, radius, source.bounds.center);

            if (!CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                    hullMesh, toLocal, castFrom, out var target, out string hullDiagnosis))
            {
                // Unity's built-in sphere is readable at runtime; a prefab re-authored with an
                // imported, unreadable mesh lands on the shipped icosphere at the same radius.
                var ico = IcosphereMeshGenerator.Generate(3, 1f, true);
                var icoToLocal = Matrix4x4.TRS(centre, Quaternion.identity, Vector3.one * radius);
                bool ok = CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                    ico, icoToLocal, castFrom, out target, out _);
                Destroy(ico);
                if (!ok)
                {
                    refusal = $"cannot read the blast's sphere: {hullDiagnosis}";
                    return false;
                }
            }

            mesh = CrystalMorphMeshBuilder.TryBuild(source, in target,
                                                    Config.PhaseNear, Config.PhaseFar, out string diagnosis);
            if (mesh == null)
            {
                refusal = diagnosis;
                return false;
            }
            return true;
        }

        protected override bool TryReadTargetColours(out Color dark, out Color bright)
        {
            if (_blast != null) return _blast.TryGetBlastColours(out dark, out bright);
            dark = bright = default;
            return false;
        }

        protected override void HandOff() => ReleaseHold();

        /// <summary>Gives the blast its photons back. Idempotent and called from teardown — an
        /// unreleased hold would leave the rest of the blast invisible.</summary>
        protected override void ReleaseHold()
        {
            if (_blast != null) _blast.HoldPhotonsForMorph(false);
            _blast = null;
        }

        protected override void OnGaveUp()
        {
            Unsubscribe();
            CSDebug.LogWarning(
                $"[{Owner}CrystalMorph] no spherical blast from this vessel arrived within " +
                $"{Config.TargetGraceSeconds:F2}s ({_blastsSeen} sphere(s) seen), so the crystal is " +
                "fading out instead of morphing. The blast is fired by a SIBLING effect in the " +
                "vessel's VesselCrystalEffects, whose anti-spam cooldown can skip a second pickup — " +
                "check it is still wired. Trace with FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph.");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            AOEExplosion.BlastInitialized -= OnBlastInitialized;
            _subscribed = false;
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }
    }
}
