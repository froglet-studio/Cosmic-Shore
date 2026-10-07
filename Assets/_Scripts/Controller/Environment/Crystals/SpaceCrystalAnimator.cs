using System.Collections;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Idle spin + collect animation for the space crystal (<c>spacecrystalanim.fbx</c>).
    ///
    /// The mesh carries two SPINS, each split into two shape keys that are meant to be STACKED:
    /// the 1st half runs 0→100, then the 2nd half runs 0→100 with the 1st still at 100, which
    /// lands every block on another block's slot. Both keys then snap to 0 on the same frame —
    /// invisible, because that pose is congruent to the rest pose — and the next spin starts.
    ///
    /// The 2nd-half keys' normals are authored against the 1st-half END pose
    /// (<c>Tools/Build/author_space_crystal_mesh.py</c>), so they are only correct while their
    /// 1st-half key is at 100. Everything here drives them that way; do not ramp a 2nd-half key
    /// on its own.
    /// </summary>
    public class SpaceCrystalAnimator : MonoBehaviour
    {
        // Channel names authored by author_space_crystal_mesh.py, as (1st half, 2nd half) per spin.
        static readonly string[,] SpinChannels =
        {
            { "5PointRotate-1stHalfSpin", "5PointRotate-2ndHalfSpin" },
            { "3PointRotate-1stHalfSpin", "3PointRotate-2ndHalfSpin" },
        };

        /// <summary>Phase at which a spin has landed; the rest of the cycle is a pause.</summary>
        const float SpinEnd = 1f;

        [Header("Idle Loop")]
        [Tooltip("Spins per second. One spin = both half-spin keys, then a pause; spins alternate 5-point / 3-point.")]
        [SerializeField] float cycleSpeed = 1f;
        [Tooltip("Pause after each spin lands, in spin-phase units (0.1 at cycleSpeed 1 = 0.1 s).")]
        [SerializeField, Min(0f)] float pauseAfterSpin = 0.1f;
        [Tooltip("Phase within the current spin: 0..1 spinning, 1..1+pause resting. Copied to the spent husk on collect.")]
        [SerializeField] float timer = 0f;

        [Header("Collect Animation")]
        [Tooltip("Seconds to finish the current spin at speed before shrinking.")]
        [SerializeField] float collectDuration = 0.35f;
        [Tooltip("Seconds to shrink to nothing after the spin finishes.")]
        [SerializeField] float shrinkDuration = 0.25f;
        [Tooltip("Scale multiplier over the shrink (x: 0..1 normalized time).")]
        [SerializeField] AnimationCurve shrinkCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        SkinnedMeshRenderer crystalRenderer;
        readonly int[,] spinShapeIndex = new int[2, 2];
        int currentSpin;

        bool isCollecting;
        Vector3 startScale;

        public float TotalCollectTime => collectDuration + shrinkDuration;

        void Awake()
        {
            startScale = transform.localScale;
            if (TryGetComponent(out crystalRenderer) && !ResolveSpinChannels())
                crystalRenderer = null;     // wrong mesh: leave its weights alone rather than guess
        }

        bool ResolveSpinChannels()
        {
            var mesh = crystalRenderer.sharedMesh;
            for (int spin = 0; spin < 2; spin++)
            for (int half = 0; half < 2; half++)
            {
                int index = mesh ? FindShape(mesh, SpinChannels[spin, half]) : -1;
                if (index < 0)
                {
                    CSDebug.LogWarning($"SpaceCrystalAnimator on '{name}': mesh '{(mesh ? mesh.name : "none")}' has no blend shape " +
                                       $"'{SpinChannels[spin, half]}'. Regenerate it with Tools/Build/author_space_crystal_mesh.py. Idle spin disabled.", this);
                    return false;
                }
                spinShapeIndex[spin, half] = index;
            }
            return true;
        }

        // Unity may prefix an imported channel name with its deformer ("Deformer.Channel").
        static int FindShape(Mesh mesh, string channel)
        {
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string shape = mesh.GetBlendShapeName(i);
                if (shape == channel || shape.EndsWith("." + channel)) return i;
            }
            return -1;
        }

        void Update()
        {
            if (isCollecting || !crystalRenderer) return;

            timer += Time.deltaTime * cycleSpeed;
            if (timer >= SpinEnd + pauseAfterSpin)
            {
                timer = 0f;
                currentSpin = (currentSpin + 1) % 2;
            }

            // Past SpinEnd the spin has landed on a pose congruent to the rest pose, so both keys
            // read 0 for the pause - that IS the reset, and it is invisible.
            ApplySpin(currentSpin, timer < SpinEnd ? timer : 0f);
        }

        /// <summary>Weights of the (1st, 2nd) half keys at spin phase 0..1: first half, then second on top.</summary>
        public static Vector2 SpinWeights(float phase)
        {
            float p = Mathf.Clamp01(phase) * 2f;
            return new Vector2(Mathf.Clamp01(p), Mathf.Clamp01(p - 1f));
        }

        void ApplySpin(int spin, float phase)
        {
            Vector2 w = SpinWeights(phase) * 100f;
            crystalRenderer.SetBlendShapeWeight(spinShapeIndex[spin, 0], w.x);
            crystalRenderer.SetBlendShapeWeight(spinShapeIndex[spin, 1], w.y);
        }

        /// <summary>Start from the same spin and phase as <paramref name="source"/> (the spent husk mirrors the live crystal).</summary>
        public void SyncPhaseFrom(SpaceCrystalAnimator source)
        {
            if (!source) return;
            if (crystalRenderer) ApplySpin(currentSpin, 0f);   // clear the spin this one was on
            timer = source.timer;
            currentSpin = source.currentSpin;
            if (crystalRenderer && !isCollecting) ApplySpin(currentSpin, timer < SpinEnd ? timer : 0f);
        }

        /// <summary>
        /// One-shot "collected" animation. Stops the idle loop, whips the current spin to its end, then shrinks out.
        /// </summary>
        public void PlayCollect()
        {
            if (isCollecting) return;
            isCollecting = true;
            StartCoroutine(CollectRoutine());
        }

        IEnumerator CollectRoutine()
        {
            if (crystalRenderer)
            {
                // Finish the spin in progress at speed; when resting between spins, play a whole one.
                float from = timer < SpinEnd ? timer : 0f;
                float t = 0f;
                while (t < collectDuration)
                {
                    t += Time.deltaTime;
                    ApplySpin(currentSpin, Mathf.Lerp(from, SpinEnd, Mathf.Clamp01(t / collectDuration)));
                    yield return null;
                }
                ApplySpin(currentSpin, SpinEnd);
            }

            float s = 0f;
            while (s < shrinkDuration)
            {
                s += Time.deltaTime;
                transform.localScale = startScale * shrinkCurve.Evaluate(Mathf.Clamp01(s / shrinkDuration));
                yield return null;
            }

            transform.localScale = Vector3.zero;
        }
    }
}
