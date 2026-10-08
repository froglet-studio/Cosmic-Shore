using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Carries a collected omni crystal's BODY onto the eight shielded prisms of the boost ring the
    /// Squirrel's hit lays — the Squirrel's bespoke replacement for the shared husk spray. Full
    /// record: <c>_Scripts/Controller/Vessel/R_VesselActions/SQUIRREL_CRYSTAL_MORPH.md</c>.
    ///
    /// The cage has 40 triangular and 24 pentagonal panels — 64 — and eight octahedron shields
    /// show 8 × 8 = 64 faces. So every panel of the crystal becomes exactly one face of a prism's
    /// shield, 1:1, and the 660 leftover quads (struts and panel rims) collapse into whichever
    /// octahedron their own solid was assigned to (<see cref="CrystalMorphMeshBuilder"/>'s panel
    /// census). The cage opens, its plates fly out, and they land as the ring it just made.
    ///
    /// ── It ends ON the real prisms, and they are live from frame 0 ──────────────────────────
    /// The targets are read from the prisms <see cref="BoostRingBuilder"/> actually laid
    /// (<see cref="BoostRingBuilder.RingLaid"/>) — their own shield semi-axes, their own final
    /// pose — and the colour pair from the material they actually bound. No second authority:
    /// retune <c>SpawnableRings</c> and the animation follows.
    ///
    /// The ring's MASS is final the instant it is laid — colliders, shield state, spatial index —
    /// so it is skimmable from frame 0 while the crystal is still landing on it. Only its photons
    /// wait: <see cref="Prism.SetOwnerHidden"/> holds nothing but drawing, which is the
    /// clock-material law's own division (Docs/PRISM_ANIMATION.md §4) applied to a hand-off.
    ///
    /// ── The ring's own arrival is REPLACED, not overlapped ───────────────────────────────────
    /// A boost prism blooms in from zero on the clock (k = 2.5/s — about 61% grown at the
    /// hand-off), and a shield that engages on a live prism blooms its faces. Revealing the ring
    /// mid-bloom would show the crystal landing on full-size octahedra and then a ring at 61%
    /// size growing back out — the seam this exists to remove. The morph IS the ring's arrival, so
    /// at the hand-off, while the prisms are still hidden and the world is covered by the crystal,
    /// both blooms are settled (<see cref="Prism.CompleteGrowthImmediately"/>,
    /// <see cref="PrismOctahedronShield.Engage"/> instant) and only then is the ring revealed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquirrelCrystalMorph : CrystalMorphRunner
    {
        Domains _domain;
        Vector3 _collectPosition;
        readonly List<Prism> _held = new();
        Color _targetDark, _targetBright;
        bool _haveTargetColour;
        bool _subscribed;
        int _ringsSeen;

        protected override string Owner => "Squirrel";

        /// <summary>
        /// Stands a morph up in the pose the crystal HAD WHEN IT WAS COLLECTED, wearing its own
        /// renderers, and waits for the ring the same dispatch is about to lay. Runs on every peer
        /// — the vessel's crystal effects are broadcast — and each peer lands on its own ring.
        ///
        /// The pose comes from <paramref name="origin"/>, never the crystal's transform: by now the
        /// crystal has usually been respawned elsewhere with its rotation reset
        /// (<see cref="Crystal.CollectPose"/>). Its APPEARANCE is read live; its pose is not.
        /// </summary>
        public static SquirrelCrystalMorph Begin(in CrystalForgeOrigin origin, Domains domain)
        {
            if (!origin.Valid)
            {
                CSDebug.LogWarning("[SquirrelCrystalMorph] the pickup carried no crystal origin, so " +
                                   "there is no body to morph — this pickup retires with no " +
                                   "animation. CrystalImpactData.FromCrystal stamps it on every collect.");
                return null;
            }

            var crystal = ResolveCrystal(origin);
            if (crystal == null)
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] no crystal with id {origin.CrystalId} on " +
                                   "this peer, so there is no body to carry onto the ring — this " +
                                   "pickup retires with no animation. A peer whose cell has not " +
                                   "finished initialising cannot find it.");
                return null;
            }

            var go = new GameObject($"CrystalMorph_{crystal.name}");
            go.transform.SetPositionAndRotation(origin.Position, origin.Rotation);
            go.transform.localScale = origin.Scale == Vector3.zero ? Vector3.one : origin.Scale;

            var morph = go.AddComponent<SquirrelCrystalMorph>();
            if (!morph.Adopt(crystal))
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] '{crystal.name}' exposed no drawable body " +
                                   "(a model with a MeshFilter AND a MeshRenderer) — nothing to " +
                                   "morph, so this pickup retires with no animation.");
                Destroy(go);
                return null;
            }

            morph._domain = domain;
            morph._collectPosition = origin.Position;
            morph.BeginWaiting();
            BoostRingBuilder.RingLaid += morph.OnRingLaid;
            morph._subscribed = true;

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Squirrel: began on '{crystal.name}' at {origin.Position} (the crystal " +
                $"is now at {crystal.transform.position}), domain {domain}, {morph.Census}; waiting " +
                $"up to {morph.Config.targetGraceSeconds:F2}s for a {PrismKind.Shielded} ring.");
            return morph;
        }

        void OnRingLaid(BoostRingLay lay)
        {
            if (IsStamped) return;
            _ringsSeen++;

            if (lay.Domain != _domain)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] Squirrel: skipped a {lay.Domain} ring — this morph is {_domain}'s.");
                return;
            }
            if (lay.Spec.Kind != PrismKind.Shielded)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] Squirrel: skipped a {lay.Spec.Kind} ring — the morph ends on " +
                    "SHIELD octahedra (a joust's danger ring lays through the same builder).");
                return;
            }
            float distance = Vector3.Distance(lay.Pose.position, _collectPosition);
            if (distance > Config.ringCaptureRadius)
            {
                CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                    $"[CrystalMorph] Squirrel: skipped a shielded ring {distance:F0}u from the " +
                    $"collect (capture radius {Config.ringCaptureRadius:F0}u) — another pickup's.");
                return;
            }

            // Every target is resolved in WORLD space off its prism, then brought into this
            // object's frame — the mesh's targets have to live in the same space as its vertices.
            var targets = new List<CrystalMorphMeshBuilder.OctahedronTarget>(lay.Prisms.Count);
            string firstRefusal = null;
            for (int i = 0; i < lay.Prisms.Count; i++)
            {
                if (TryResolveShield(lay.Prisms[i], out var target, out var refusal)) targets.Add(target);
                else firstRefusal ??= $"prism {i}: {refusal}";
            }
            if (targets.Count != lay.Prisms.Count)
            {
                CSDebug.LogWarning($"[SquirrelCrystalMorph] {targets.Count}/{lay.Prisms.Count} ring " +
                                   $"prisms exposed a shield octahedron ({firstRefusal}) — the morph " +
                                   "cannot land on a ring it cannot measure, so the crystal will fade instead.");
                return;
            }

            var mesh = CrystalMorphMeshBuilder.TryBuild(SourceMesh, targets, Config.fillerPhase,
                                                        Config.panelPhaseStart,
                                                        Mathf.Max(Config.panelPhaseStart, Config.panelPhaseEnd),
                                                        out string diagnosis);
            if (mesh == null)
            {
                CSDebug.LogError($"[SquirrelCrystalMorph] cannot morph this crystal onto the ring: {diagnosis}");
                return;
            }

            Unsubscribe();
            CaptureTargetColour(lay.Prisms);

            // Photons only: the ring is live, skimmable mass from the frame it was laid.
            for (int i = 0; i < lay.Prisms.Count; i++)
            {
                var prism = lay.Prisms[i];
                if (!prism) continue;
                prism.SetOwnerHidden(true);
                _held.Add(prism);
            }

            Stamp(mesh);

            CSDebug.LogVerbose(CSLogChannel.CrystalMorph,
                $"[CrystalMorph] Squirrel: took ring #{_ringsSeen} {distance:F1}u from the collect — " +
                $"{_held.Count} prisms held, {MorphVertexCount} morph vertices, {MorphSeconds:F2}s of " +
                $"morph + {Config.duration - MorphSeconds:F2}s of dissolve (colour target " +
                $"{(_haveTargetColour ? "read" : "NOT FOUND")}).");
        }

        /// <summary>
        /// The eight faces of one laid prism's shield, in this object's frame — built from the
        /// shield's OWN semi-axes (<see cref="PrismOctahedronShield"/> derives them from the
        /// authored BoxCollider and the circumscribing scale) and the prism's FINAL scale. Under the
        /// clock law a prism's transform is final at its stamp, but reading <c>TargetScale</c> makes
        /// that independent of whether this ran before or after its creation completed.
        /// </summary>
        bool TryResolveShield(Prism prism, out CrystalMorphMeshBuilder.OctahedronTarget target,
                              out string refusal)
        {
            target = default;
            refusal = null;
            if (!prism) { refusal = "the prism is gone"; return false; }
            if (!prism.TryGetComponent<PrismOctahedronShield>(out var shield))
            {
                refusal = "no PrismOctahedronShield component";
                return false;
            }
            if (!shield.IsShielded)
            {
                refusal = "its shield had not engaged when the ring was announced";
                return false;
            }

            Vector3 scale = prism.TargetScale;
            if (scale == Vector3.zero) scale = prism.transform.localScale;
            var toLocal = transform.worldToLocalMatrix *
                          Matrix4x4.TRS(prism.transform.position, prism.transform.rotation, scale);

            target = CrystalMorphMeshBuilder.OctahedronTarget.FromSemiAxes(
                toLocal, shield.ShellCenterLocal, shield.ShellSemiAxesLocal);
            return true;
        }

        /// <summary>
        /// The palette the morph has to arrive wearing, read off the material the laid prisms
        /// actually bound (<c>PrismStateManager.ApplyShieldState</c>: the domain's shielded block
        /// material) — the same "read the thing that shipped" rule the geometry follows.
        /// </summary>
        void CaptureTargetColour(IReadOnlyList<Prism> prisms)
        {
            for (int i = 0; i < prisms.Count; i++)
            {
                if (!prisms[i] || !prisms[i].TryGetComponent<MeshRenderer>(out var r)) continue;
                var mat = r.sharedMaterial;
                if (mat == null || !mat.HasProperty(DarkColorId) || !mat.HasProperty(BrightColorId)) continue;
                _targetDark = mat.GetColor(DarkColorId);
                _targetBright = mat.GetColor(BrightColorId);
                _haveTargetColour = true;
                return;
            }
        }

        protected override bool TryReadTargetColours(out Color dark, out Color bright)
        {
            dark = _targetDark;
            bright = _targetBright;
            return _haveTargetColour;
        }

        /// <summary>
        /// The geometry has landed on the octahedra. Settle the ring's own arrival animations while
        /// it is still hidden — the crystal covers it, so the snap is invisible and is exactly the
        /// "world is covered" window <see cref="Prism.CompleteGrowthImmediately"/> exists for —
        /// then reveal it. From here the ring draws itself and the crystal dissolves off it.
        /// </summary>
        protected override void HandOff()
        {
            for (int i = 0; i < _held.Count; i++)
            {
                var prism = _held[i];
                if (!prism || prism.destroyed) continue;

                // Boost prisms finish creation a frame after the lay (waitTime 0); the window here
                // is ~0.37 s, so this only fires if the per-frame creation budget was saturated.
                if (!prism.IsCreationComplete) prism.CompleteCreationImmediately();
                prism.CompleteGrowthImmediately();
                if (prism.TryGetComponent<PrismOctahedronShield>(out var shield) && shield.IsShielded)
                    shield.Engage(instant: true);
            }
            ReleaseHold();
        }

        /// <summary>Gives the ring its photons back. Idempotent and called from teardown — an
        /// unreleased hold would leave the ring invisible, skimmable mass for the rest of its life.
        /// The boost pool serves only rings, and nothing else that draws from it hides a prism, so
        /// a prism recycled under a dead morph is unhidden harmlessly (pool reuse clears it anyway).</summary>
        protected override void ReleaseHold()
        {
            for (int i = 0; i < _held.Count; i++)
                if (_held[i]) _held[i].SetOwnerHidden(false);
            _held.Clear();
        }

        protected override void OnGaveUp()
        {
            Unsubscribe();
            CSDebug.LogWarning(
                $"[SquirrelCrystalMorph] no usable {PrismKind.Shielded} boost ring arrived within " +
                $"{Config.targetGraceSeconds:F2}s ({_ringsSeen} ring(s) seen), so the crystal is " +
                "fading out instead of morphing. The ring is laid by a SIBLING effect " +
                "(SquirrelVesselExplosionByCrystalEffect → AOEShieldedRingSpawner), whose anti-spam " +
                "cooldown skips a second pickup inside 0.15 s — check it is still in the Squirrel's " +
                "VesselCrystalEffects. Trace with FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph.");
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            BoostRingBuilder.RingLaid -= OnRingLaid;
            _subscribed = false;
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }
    }
}
