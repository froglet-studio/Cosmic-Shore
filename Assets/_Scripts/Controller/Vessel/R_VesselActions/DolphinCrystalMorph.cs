using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY onto the blast cone the Dolphin's hit fires (Echo
    /// Obliteration) — the Dolphin's bespoke replacement for the shared husk spray. Full record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_MORPH.md</c>.
    ///
    /// A Dolphin that strikes an omni crystal fires a conic blast down its nose. Until now the
    /// crystal also burst into the shared spray, so the pickup read as two unrelated events:
    /// something shattered, and separately a cone went off. It now reads as one — the cage bursts
    /// open down-range and lands on the cone's surface, and the cone takes the surface over.
    ///
    /// ── The mapping is the Scarab's hull cast, read around the CRYSTAL'S middle ─────────────────
    /// The blast cone (<c>PrimitiveCone</c>, a closed 20-sided cone) is convex, so the cage needs no
    /// census: every vertex lands on the cone and takes that facet's normal
    /// (<see cref="CrystalMorphMeshBuilder.ConvexHullTarget"/>). Unlike the ball, the cone does not
    /// surround the crystal — it opens from the hull's nose hundreds of units down-range — so each
    /// vertex keeps its direction from the crystal's own middle and is cast from the cone's
    /// centroid (the <c>sourceCentre</c> overload). The cage opens out the way it faces: front
    /// plates onto the front of the blast, back plates back toward the apex.
    ///
    /// ── It lands on the cone AS THE CONE WILL BE at the hand-off ───────────────────────────────
    /// The cone grows from nothing over its whole 2.7 s sweep, so there is no single "final" cone to
    /// land on. Its container is static in the world for the blast's life and its growth curve is
    /// deterministic, so the target is read from the blast's own curve at the moment the morph
    /// will hand off (<see cref="AOEConicExplosion.TryGetConeLocalToWorldAt"/>): where the plates
    /// land is where the cone is drawn when it reappears. No second authority — retune the
    /// blast's scales, duration or Space reach and the morph follows.
    ///
    /// ── MECHANICALLY INSTANT, VISUALLY GRADUAL ─────────────────────────────────────────────────
    /// Only the cone's PHOTONS wait (<see cref="AOEConicExplosion.SetMorphStandIn"/>). Its sweep,
    /// trigger and prism damage run from the frame it detonates, exactly as before, so nothing the
    /// blast does to the world changes — the vessel kit stays as designed. The crystal plates race
    /// out over the first ~0.37 s of the sweep and the cone takes the surface where they land.
    ///
    /// Adoption, colour convergence, overlays, hand-off and dissolve are
    /// <see cref="CrystalMorphRunner"/>'s, shared with the Squirrel and the Scarab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DolphinCrystalMorph : CrystalMorphRunner
    {
        IVessel _vessel;
        Vector3 _collectPosition;
        AOEConicExplosion _cone;
        bool _subscribed;
        int _conesSeen;

        protected override string Owner => "Dolphin";

        /// <summary>
        /// Stands a morph up in the pose the crystal HAD WHEN IT WAS COLLECTED, wearing its own
        /// renderers, and waits for the blast the same dispatch is about to fire. Runs on every peer
        /// — the vessel's crystal effects are broadcast — and each peer lands on its own cone.
        /// </summary>
        public static DolphinCrystalMorph Begin(in CrystalForgeOrigin origin, IVessel vessel)
        {
            if (vessel == null) return null;
            if (!origin.Valid)
            {
                CSDebug.LogWarning("[DolphinCrystalMorph] the pickup carried no crystal origin, so " +
                                   "there is no body to morph — this pickup retires with no " +
                                   "animation. CrystalImpactData.FromCrystal stamps it on every collect.");
                return null;
            }

            var crystal = ResolveCrystal(origin);

            // A bodyless crystal is not a failure: there is no cage to carry. The blast still fires
            // from the sibling effect - only this animation stands down.
            if (crystal != null && !crystal.HasBody) return null;

            if (crystal == null)
            {
                CSDebug.LogWarning($"[DolphinCrystalMorph] no crystal with id {origin.CrystalId} on " +
                                   "this peer, so there is no body to carry onto the blast — this " +
                                   "pickup retires with no animation. A peer whose cell has not " +
                                   "finished initialising cannot find it.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(origin.Position, origin.Rotation);
            go.transform.localScale = origin.Scale == Vector3.zero ? Vector3.one : origin.Scale;

            var morph = go.AddComponent<DolphinCrystalMorph>();
            if (!morph.Adopt(crystal))
            {
                CSDebug.LogWarning($"[DolphinCrystalMorph] '{crystal.name}' exposed no drawable body " +
                                   "(a model with a MeshFilter and MeshRenderer, or a SkinnedMeshRenderer) — nothing to " +
                                   "morph, so this pickup retires with no animation.");
                Destroy(go);
                return null;
            }

            morph._vessel = vessel;
            morph._collectPosition = origin.Position;
            morph.BeginWaiting();
            AOEConicExplosion.Initialized += morph.OnConeInitialized;
            morph._subscribed = true;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Dolphin: began on '{crystal.name}' at {origin.Position} (the crystal " +
                $"is now at {crystal.transform.position}), {morph.Census}; waiting up to " +
                $"{morph.Config.TargetGraceSeconds:F2}s for the Echo Obliteration cone.");
            return morph;
        }

        void OnConeInitialized(AOEConicExplosion cone)
        {
            if (IsStamped || cone == null) return;
            _conesSeen++;

            if (!IsThisVessel(cone.Vessel))
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    "[CrystalMorph] Dolphin: skipped a cone another vessel fired.");
                return;
            }

            // A cone this vessel fires is this pickup's only if it went off where the crystal was:
            // a morph still waiting out a swallowed blast (the 0.15 s anti-spam) must not claim the
            // NEXT pickup's cone.
            float distance = Vector3.Distance(cone.ApexPosition, _collectPosition);
            if (distance > Config.RingCaptureRadius)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] Dolphin: skipped a cone {distance:F0}u from the collect " +
                    $"(capture radius {Config.RingCaptureRadius:F0}u) — another pickup's.");
                return;
            }

            if (!TryBuildOntoCone(cone, out var mesh, out string refusal))
            {
                CSDebug.LogWarning($"[DolphinCrystalMorph] cannot land on the blast cone: {refusal}. " +
                                   "The crystal will fade instead; the blast is unaffected.");
                return;
            }

            Unsubscribe();
            _cone = cone;

            // Photons only: the blast sweeps, damages and triggers from this frame, as before.
            _cone.SetMorphStandIn(true);
            Stamp(mesh);

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Dolphin: took cone #{_conesSeen} {distance:F1}u from the collect — " +
                $"{MorphVertexCount} morph vertices, {MorphSeconds:F2}s of morph + " +
                $"{Config.Duration - MorphSeconds:F2}s of dissolve (colour target " +
                $"{(HasTargetColour ? "read" : "NOT FOUND")}).");
        }

        /// <summary>The blast's vessel is this pickup's vessel. Compared by object and by status, so
        /// it holds whichever of the two references (the impactor's component, the status's
        /// inspector reference) each side happened to carry.</summary>
        bool IsThisVessel(IVessel other)
        {
            if (other == null || _vessel == null) return false;
            if (ReferenceEquals(other, _vessel)) return true;
            var a = other.VesselStatus;
            var b = _vessel.VesselStatus;
            return a != null && ReferenceEquals(a, b);
        }

        /// <summary>
        /// The cone as it will be drawn at the hand-off, in the BODY's frame (the crystal's model is
        /// posed under its root, <see cref="CrystalMorphRunner.BodyFrame"/>), cast around the cage's
        /// own middle. The morph is stamped in this same frame, so the hand-off lands
        /// <see cref="CrystalMorphRunner.MorphSeconds"/> after the cone's Initialize.
        /// </summary>
        bool TryBuildOntoCone(AOEConicExplosion cone, out Mesh mesh, out string refusal)
        {
            mesh = null;
            refusal = null;

            var coneMesh = cone.ConeMesh;
            if (coneMesh == null) { refusal = "the blast prefab draws no mesh"; return false; }

            float morphSeconds = Config.Duration * Mathf.Clamp01(Config.MorphFraction);
            if (!cone.TryGetConeLocalToWorldAt(morphSeconds, out var coneToWorld))
            {
                refusal = "the blast has no container posed yet";
                return false;
            }

            var source = SourceMesh;
            if (source == null) { refusal = "the crystal's body exposed no mesh"; return false; }

            Transform frame = BodyFrame;
            Matrix4x4 toLocal = frame.worldToLocalMatrix * coneToWorld;

            // Cast from the middle of the cone's AXIS (the mesh's own origin), strictly inside any
            // non-degenerate cone. Nearer the apex than the solid's centroid on purpose: the backward
            // half of the cage reaches further up the narrow nose of the blast before it runs out of
            // directions, so the plates cover the cone from close to the hull's nose to its base.
            Vector3 centre = toLocal.MultiplyPoint3x4(Vector3.zero);

            if (!CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                    coneMesh, toLocal, centre, out var target, out string hullDiagnosis))
            {
                refusal = $"cannot read the cone: {hullDiagnosis}";
                return false;
            }

            mesh = CrystalMorphMeshBuilder.TryBuild(source, in target, source.bounds.center,
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
            if (_cone != null) return _cone.TryGetConeColours(out dark, out bright);
            dark = bright = default;
            return false;
        }

        protected override void HandOff() => ReleaseHold();

        /// <summary>Gives the cone its photons back. Idempotent and called from teardown — an
        /// unreleased hold would leave the rest of the sweep invisible. A blast that ended or was
        /// blocked mid-morph is simply gone; the crystal lands and dissolves where it was.</summary>
        protected override void ReleaseHold()
        {
            if (_cone != null) _cone.SetMorphStandIn(false);
            _cone = null;
        }

        protected override void OnGaveUp()
        {
            Unsubscribe();
            CSDebug.LogWarning(
                $"[DolphinCrystalMorph] no Echo Obliteration cone from this Dolphin arrived within " +
                $"{Config.TargetGraceSeconds:F2}s ({_conesSeen} cone(s) seen), so the crystal is " +
                "fading out instead of morphing. The cone is fired by a SIBLING effect " +
                "(DolphinVesselExplosionByCrystalEffect), whose anti-spam cooldown skips a second " +
                "pickup inside 0.15 s — check it is still in the Dolphin's VesselCrystalEffects. " +
                "Trace with FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph.");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            AOEConicExplosion.Initialized -= OnConeInitialized;
            _subscribed = false;
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }
    }
}
