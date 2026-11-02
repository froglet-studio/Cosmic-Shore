using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY onto the nest flora the Shrike's pickup grows where the
    /// crystal was (Garrett, 2026-10-15: "collecting a crystal should morph it into a nest flora") —
    /// the Shrike's bespoke replacement for the shared husk spray. Full record:
    /// <c>_Scripts/Controller/Vessel/R_VesselActions/SHRIKE_CRYSTAL_MORPH.md</c>.
    ///
    /// ── The cage closes onto the BUD ──────────────────────────────────────────────────────────
    /// A fresh flora is a six-sided stem rising from the collect point and an octahedral bud on top
    /// (<see cref="ShrikeNestFlora"/>); its missiles start at zero size. The bud is convex and about
    /// the crystal's size, so the cage lands on it by the hull cast, every vertex keeping its
    /// direction from the crystal's own middle (<see cref="CrystalMorphRecast"/>): the crystal rises
    /// and folds into the bud. The stem is a thin column the bud stands on; it appears with the rest
    /// of the flora at the hand-off.
    ///
    /// ── MECHANICALLY INSTANT, VISUALLY GRADUAL ─────────────────────────────────────────────────
    /// Only the flora's PHOTONS wait (<see cref="ShrikeNestFlora.SetMorphStandIn"/>). It is visual
    /// only to begin with, and its missile count and growth run from the pickup frame as before.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShrikeCrystalMorph : CrystalMorphRunner
    {
        IVesselStatus _status;
        Vector3 _collectPosition;
        ShrikeNestFlora _flora;
        bool _subscribed;

        protected override string Owner => "Shrike";

        public static ShrikeCrystalMorph Begin(in CrystalForgeOrigin origin, IVesselStatus status)
        {
            if (status == null) return null;
            if (!origin.Valid)
            {
                CSDebug.LogWarning("[ShrikeCrystalMorph] the pickup carried no crystal origin, so there is " +
                                   "no body to morph — this pickup retires with no animation.");
                return null;
            }

            var crystal = ResolveCrystal(origin);
            if (crystal != null && !crystal.HasBody) return null;
            if (crystal == null)
            {
                CSDebug.LogWarning($"[ShrikeCrystalMorph] no crystal with id {origin.CrystalId} on this peer, " +
                                   "so there is no body to carry onto the flora — this pickup retires with " +
                                   "no animation.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(origin.Position, origin.Rotation);
            go.transform.localScale = origin.Scale == Vector3.zero ? Vector3.one : origin.Scale;

            var morph = go.AddComponent<ShrikeCrystalMorph>();
            if (!morph.Adopt(crystal))
            {
                CSDebug.LogWarning($"[ShrikeCrystalMorph] '{crystal.name}' exposed no drawable body — nothing " +
                                   "to morph, so this pickup retires with no animation.");
                Destroy(go);
                return null;
            }

            morph._status = status;
            morph._collectPosition = origin.Position;
            morph.BeginWaiting();
            ShrikeExecutor.FloraGrownFromCrystal += morph.OnFloraGrown;
            morph._subscribed = true;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Shrike: began on '{crystal.name}' at {origin.Position}, {morph.Census}; " +
                $"waiting up to {morph.Config.TargetGraceSeconds:F2}s for the nest flora.");
            return morph;
        }

        void OnFloraGrown(IVesselStatus status, ShrikeNestFlora flora)
        {
            if (IsStamped || flora == null) return;
            if (!ReferenceEquals(status, _status)) return;

            // The flora this pickup grew stands where the crystal was; a morph waiting out a lost
            // flora must not claim the next pickup's.
            float distance = Vector3.Distance(flora.transform.position, _collectPosition);
            if (distance > Config.RingCaptureRadius) return;

            if (!TryBuildOntoBud(flora, out var mesh, out string refusal))
            {
                CSDebug.LogWarning($"[ShrikeCrystalMorph] cannot land on the nest flora: {refusal}. " +
                                   "The crystal will fade instead; the flora is unaffected.");
                return;
            }

            Unsubscribe();
            _flora = flora;
            _flora.SetMorphStandIn(true);
            Stamp(mesh);

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Shrike: took the flora {distance:F1}u from the collect — {MorphVertexCount} " +
                $"morph vertices, colour target {(HasTargetColour ? "read" : "NOT FOUND")}.");
        }

        bool TryBuildOntoBud(ShrikeNestFlora flora, out Mesh mesh, out string refusal)
        {
            mesh = null;
            refusal = null;
            var source = SourceMesh;
            if (source == null) { refusal = "the crystal's body exposed no mesh"; return false; }

            var bud = flora.Bud;
            var budMesh = bud && bud.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            if (budMesh == null) { refusal = "the flora has no bud mesh"; return false; }

            Transform frame = BodyFrame;
            Matrix4x4 toLocal = frame.worldToLocalMatrix * bud.localToWorldMatrix;
            Vector3 centre = toLocal.MultiplyPoint3x4(Vector3.zero);   // the octahedron's own centre

            if (!CrystalMorphMeshBuilder.ConvexHullTarget.TryFromMesh(
                    budMesh, toLocal, centre, out var target, out string hullDiagnosis))
            {
                refusal = $"cannot read the bud: {hullDiagnosis}";
                return false;
            }

            mesh = CrystalMorphRecast.TryBuildAround(source, in target, source.bounds.center,
                                                     Config.PhaseNear, Config.PhaseFar, out string diagnosis);
            if (mesh == null) { refusal = diagnosis; return false; }
            return true;
        }

        protected override bool TryReadTargetColours(out Color dark, out Color bright)
        {
            if (_flora != null) return _flora.TryGetColours(out dark, out bright);
            dark = bright = default;
            return false;
        }

        protected override void HandOff() => ReleaseHold();

        protected override void ReleaseHold()
        {
            if (_flora != null) _flora.SetMorphStandIn(false);
            _flora = null;
        }

        protected override void OnGaveUp()
        {
            Unsubscribe();
            CSDebug.LogWarning(
                $"[ShrikeCrystalMorph] no nest flora from this Shrike arrived within " +
                $"{Config.TargetGraceSeconds:F2}s, so the crystal is fading out instead of morphing. The " +
                "flora is grown by a SIBLING effect (ShrikeCrystalEffect → ShrikeExecutor.CollectCrystal) — " +
                "check it is still in the Shrike's VesselCrystalEffects. Trace with FrogletTools ▸ Toolbox ▸ " +
                "Logging ▸ CrystalMorph.");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            ShrikeExecutor.FloraGrownFromCrystal -= OnFloraGrown;
            _subscribed = false;
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }
    }
}
