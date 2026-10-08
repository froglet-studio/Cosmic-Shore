using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Plays a crystal's flip wave procedurally: every plate turns 180° about one of its own diagonals, ring
    /// by ring, from one vertex of the crystal to the opposite one, and each wave starts from a different
    /// vertex than the last. It replaces the Time crystal's Animator and its take - the take was one wave
    /// from one fixed vertex, and was only made to wander by snapping the whole model to a random
    /// symmetry of the shell each loop.
    ///
    /// The math is <see cref="FlipWave"/>, the bone writes are <see cref="FlipWaveRig"/>, and the motion is a
    /// <see cref="FlipWaveProfileSO"/>; <c>TimeCrystalFlipWaveProfile.asset</c> is generated from the take and
    /// the edit-mode parity test holds this component to it. Design record: <c>Docs/TIME_CRYSTAL.md</c>.
    ///
    /// Cost: one profile sample per ring and one bone write per plate whose ring is mid-flip, only while the
    /// renderer is visible - no Animator, no clip evaluation. The skinned mesh renders as before.
    /// </summary>
    public class CrystalFlipWave : MonoBehaviour
    {
        static readonly ProfilerMarker UpdateMarker = new("CrystalFlipWave.LateUpdate");

        [Header("Model")]
        [Tooltip("The imported model's root (the TimeCrystalExport instance). Its local space is the frame the plates are measured in, and its one SkinnedMeshRenderer carries a plate per bone (the mesh must be Read/Write enabled). Never the crystal root, whose rotation belongs to the capture flourish.")]
        [SerializeField] Transform model;

        [Header("Motion")]
        [Tooltip("When each ring turns and how. TimeCrystalFlipWaveProfile is generated from the artist's take.")]
        [SerializeField] FlipWaveProfileSO profile;

        SkinnedMeshRenderer plates;
        FlipWaveRig rig;
        FlipSample[] ringSamples;
        System.Random rng;
        float clockStart;
        int start;
        int cycle;

        void Awake()
        {
            if (!model || !profile)
            {
                CSDebug.LogError($"CrystalFlipWave on '{name}': model and profile must both be assigned. The crystal will hold still.", this);
                enabled = false;
                return;
            }

            // Resolved, not serialized: an FBX sub-object's fileID cannot be authored offline, and the model
            // carries exactly one skinned mesh.
            var skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinned.Length != 1)
            {
                CSDebug.LogError($"CrystalFlipWave on '{name}': '{model.name}' has {skinned.Length} SkinnedMeshRenderers, expected exactly one. The crystal will hold still.", this);
                enabled = false;
                return;
            }
            plates = skinned[0];

            if (!FlipWaveRig.TryBuild(plates, model, out rig, out string problem))
            {
                CSDebug.LogError($"CrystalFlipWave on '{name}': {problem}. Was the model re-exported? Re-run Tools/Build/author_time_crystal_flip_wave.py. The crystal will hold still.", this);
                enabled = false;
                return;
            }

            ringSamples = new FlipSample[rig.RingCount];
            // A per-instance stream: the global UnityEngine.Random is seeded for deterministic content, and a
            // per-frame visual must not consume from it.
            rng = new System.Random(GetInstanceID());
        }

        /// <summary>A fresh (or re-pooled) crystal starts at rest, on a new wave, from a random vertex.</summary>
        void OnEnable()
        {
            if (rig == null) return;
            rig.ResetToRest();
            clockStart = Time.time;
            cycle = 0;
            start = rng.Next(rig.StartCount);
        }

        void LateUpdate()
        {
            using var _ = UpdateMarker.Auto();

            float elapsed = Time.time - clockStart;
            float loop = profile.LoopSeconds;
            int now = Mathf.FloorToInt(elapsed / loop);
            if (now != cycle)
            {
                cycle = now;
                // Any vertex but the one just used, each equally likely.
                int next = rng.Next(rig.StartCount - 1);
                start = next >= start ? next + 1 : next;
            }

            // A crystal nobody can see does no work. Its pose is a pure function of the clock, so it is right
            // again the frame after it comes back into view (the same one-frame lag as Animator culling).
            if (!plates.isVisible) return;

            float seconds = elapsed - now * loop;
            for (int ring = 0; ring < ringSamples.Length; ring++) ringSamples[ring] = profile.Sample(ring, seconds);
            rig.Apply(start, ringSamples);
        }
    }
}
